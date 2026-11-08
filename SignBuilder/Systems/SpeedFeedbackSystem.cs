using System.Collections.Generic;
using Colossal.Collections;
using Colossal.Entities;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Prefabs;
using Game.Simulation;
using SignBuilderSA.Assemblies;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace SignBuilderSA.Systems
{
    /// <summary>
    /// Speed feedback signs ("Your Speed"): each placed one shows its "ok" state (happy face, THANK YOU) while the cars
    /// passing it keep to its limit, and its "slow" state (sad face, SLOW DOWN) once one drives faster than the limit
    /// plus a margin; it goes back to "ok" after traffic has kept to the limit for a few seconds. Both states are
    /// prefabs of their own (<see cref="SpeedFeedback"/>, built together by AssemblyBuilder); a sign switches by having
    /// its prefab swapped in place, as the game does when it replaces an object's prefab (new PrefabRef, Updated), so it
    /// keeps its entity, position, elevation and everything attached to it.
    ///
    /// Cheap: runs every 32 simulation frames (about half a game second), only while feedback signs are placed, and
    /// looks only at the moving objects near each sign through the game's moving object search tree. Saves always
    /// hold the main state (<see cref="RestoreMainStates"/> runs just before saving).
    /// </summary>
    public partial class SpeedFeedbackSystem : GameSystemBase
    {
        /// <summary>Feedback structure prefabs (both states) and the placed signs using them.</summary>
        internal static readonly TrackedSigns Tracked = new();

        /// <summary>A prefab of a speed feedback structure, either state.</summary>
        public static void TrackPrefab(PrefabBase prefab) => Tracked.Track(prefab);

        /// <summary>Simulation frames between checks (60 frames are one game second at normal speed).</summary>
        private const int kInterval = 32;
        /// <summary>Cars this close (meters, horizontally) are measured.</summary>
        private const float kRadius = 60f;
        /// <summary>
        /// Switch to "slow" above the limit plus this (km/h). Small: the game's drivers keep to their road's limit, so a
        /// sign only turns sad where its own number is lower than the road's (a 40 sign on a 60 road, a 60 sign on a
        /// highway); a large margin hid that.
        /// </summary>
        private const float kSlowMargin = 1f;
        /// <summary>Back to "ok" after this many simulation frames (3 s) with every car at or below the limit.</summary>
        private const uint kCalmFrames = 180;
        /// <summary>Cars slower than this (km/h) are waiting, not passing, and are not measured.</summary>
        private const float kMinMeasuredKmh = 5f;
        /// <summary>A car counts as heading the way the sign faces within about 45 degrees.</summary>
        private const float kHeadingCos = 0.7f;
        /// <summary>Cars up to this far beyond the sign (meters along its traffic) are still passing it.</summary>
        private const float kPastSign = 15f;
        /// <summary>m/s to km/h, as the game's own speed readouts (DeveloperInfoUISystem: |m_Velocity| * 3.6).</summary>
        private const float kToKmh = 3.6f;

        /// <summary>The two state prefabs of one structure.</summary>
        private struct Pair
        {
            public Entity Main;
            public Entity Alt;
            public Entity Ok;
            public Entity Slow;
            public float Limit;
            /// <summary>Surface-mounted signs have no road direction: cars heading either way are measured.</summary>
            public bool AnyDirection;
        }

        private sealed class SignState
        {
            public bool Slow;
            /// <summary>Simulation frame since which traffic has kept to the limit (0: it has not).</summary>
            public uint CalmSince;
        }

        private struct NearbyIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>
        {
            public Bounds2 m_Area;
            public NativeList<Entity> m_Found;

            public bool Intersect(QuadTreeBoundsXZ bounds) => MathUtils.Intersect(bounds.m_Bounds.xz, m_Area);

            public void Iterate(QuadTreeBoundsXZ bounds, Entity item)
            {
                if (MathUtils.Intersect(bounds.m_Bounds.xz, m_Area)) m_Found.Add(item);
            }
        }

        private PrefabSystem m_PrefabSystem;
        private SimulationSystem m_Simulation;
        private Game.Objects.SearchSystem m_Search;
        /// <summary>Per prefab entity: its structure's pair, or null if it cannot switch (other state missing).</summary>
        private readonly Dictionary<Entity, Pair?> m_Pairs = new();
        private readonly Dictionary<Entity, SignState> m_States = new();
        private readonly List<Entity> m_Scratch = new();
        private readonly List<Entity> m_Signs = new();
        private int m_Updates;

        // Diagnostics: a summary in the log every 20 s (real time) while speed signs exist, when it changed.
        private const float kReportSeconds = 20f;
        private float m_NextReport;
        private string m_LastReport;
        private int m_CarsNear;
        private int m_CarsMeasured;
        private float m_FastestSeen;
        private readonly HashSet<string> m_Explained = new();

#if TESTS
        public static int Swaps { get; private set; }
        public static float LastMaxKmh { get; private set; }
#endif

        public override int GetUpdateInterval(SystemUpdatePhase phase) => kInterval;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_Simulation = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_Search = World.GetOrCreateSystemManaged<Game.Objects.SearchSystem>();
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            m_Pairs.Clear();
            m_States.Clear();
        }

        protected override void OnUpdate()
        {
            if (Tracked.Signs.Count == 0)
            {
                if (Tracked.Prefabs.Count > 0) Report(0, 0, 0, "");
                return;
            }
            // Structures built or rebuilt meanwhile may have completed a pair that was missing its other state.
            if (++m_Updates % 20 == 0) DropUnresolvedPairs();
            Tracked.Prune(EntityManager, m_Scratch);
            foreach (var gone in m_Scratch) m_States.Remove(gone);
            if (Tracked.Signs.Count == 0) return;

            var frame = m_Simulation.frameIndex;
            var tree = m_Search.GetMovingSearchTree(readOnly: true, out var dependencies);
            dependencies.Complete();
            var found = new NativeList<Entity>(32, Allocator.Temp);
            int switchable = 0, unresolved = 0, slowNow = 0;
            var limits = new SortedSet<float>();
            try
            {
                m_Signs.Clear();
                m_Signs.AddRange(Tracked.Signs);
                foreach (var sign in m_Signs)
                {
                    if (!EntityManager.TryGetComponent(sign, out PrefabRef prefabRef) || !EntityManager.TryGetComponent(sign, out Game.Objects.Transform transform)) continue;
                    var pair = PairOf(prefabRef.m_Prefab);
                    if (pair == null)
                    {
                        unresolved++;
                        continue;
                    }
                    var p = pair.Value;
                    switchable++;
                    limits.Add(p.Limit);
                    if (!m_States.TryGetValue(sign, out var state))
                    {
                        state = new SignState { Slow = prefabRef.m_Prefab == p.Slow };
                        m_States[sign] = state;
                    }

                    var fastest = Fastest(tree, found, transform, p.AnyDirection);
#if TESTS
                    LastMaxKmh = fastest;
#endif
                    if (!state.Slow)
                    {
                        if (fastest > p.Limit + kSlowMargin)
                        {
                            state.Slow = true;
                            state.CalmSince = 0;
                        }
                    }
                    else if (fastest <= p.Limit)
                    {
                        if (state.CalmSince == 0) state.CalmSince = math.max(frame, 1u);
                        else if (frame - state.CalmSince >= kCalmFrames)
                        {
                            state.Slow = false;
                            state.CalmSince = 0;
                        }
                    }
                    else
                    {
                        state.CalmSince = 0;
                    }

                    if (state.Slow) slowNow++;
                    var wanted = state.Slow ? p.Slow : p.Ok;
                    if (wanted != prefabRef.m_Prefab) Swap(sign, wanted);
                }
            }
            finally
            {
                found.Dispose();
            }
            Report(switchable, unresolved, slowNow, string.Join("/", limits));
        }

        /// <summary>Writes the diagnostics summary (throttled; only when it changed).</summary>
        private void Report(int switchable, int unresolved, int slowNow, string limits)
        {
            var now = UnityEngine.Time.realtimeSinceStartup;
            if (now < m_NextReport) return;
            m_NextReport = now + kReportSeconds;
            var report = Tracked.Signs.Count == 0
                ? $"Speed feedback: {Tracked.Prefabs.Count} speed sign structure(s) known, none placed in this city."
                : $"Speed feedback: {Tracked.Signs.Count} placed, {switchable} can switch (limit {limits} km/h), {unresolved} cannot (other state missing), {slowNow} showing SLOW. " +
                  $"Last {kReportSeconds:0} s: {m_CarsNear} cars near them, {m_CarsMeasured} heading their way, fastest {m_FastestSeen:0} km/h.";
            m_CarsNear = 0;
            m_CarsMeasured = 0;
            m_FastestSeen = 0f;
            if (report == m_LastReport) return;
            m_LastReport = report;
            Mod.log.Info(report);
        }

        /// <summary>
        /// The speed in km/h of the fastest car passing the sign: within <see cref="kRadius"/>, moving the way the sign
        /// faces (towards it, or just past it). 0 if none is passing.
        /// </summary>
        private float Fastest(NativeQuadTree<Entity, QuadTreeBoundsXZ> tree, NativeList<Entity> found, Game.Objects.Transform sign, bool anyDirection)
        {
            found.Clear();
            var center = sign.m_Position.xz;
            var iterator = new NearbyIterator { m_Area = new Bounds2(center - kRadius, center + kRadius), m_Found = found };
            tree.Iterate(ref iterator);
            // Signs face the traffic of their road: its cars travel along the structure's +Z (see AssemblyBuilder.Layout).
            var forward = math.forward(sign.m_Rotation).xz;
            forward = math.lengthsq(forward) > 1e-6f ? math.normalize(forward) : new float2(0f, 1f);
            var fastest = 0f;
            for (var i = 0; i < found.Length; i++)
            {
                var car = found[i];
                if (!EntityManager.HasComponent<Game.Vehicles.Car>(car)) continue;
                if (!EntityManager.TryGetComponent(car, out Game.Objects.Moving moving) || !EntityManager.TryGetComponent(car, out Game.Objects.Transform transform)) continue;
                var offset = transform.m_Position.xz - center;
                if (math.lengthsq(offset) > kRadius * kRadius) continue;
                m_CarsNear++;
                var velocity = moving.m_Velocity.xz;
                var speed = math.length(moving.m_Velocity);
                var kmh = speed * kToKmh;
                if (kmh < kMinMeasuredKmh || !math.isfinite(kmh)) continue;
                if (!anyDirection)
                {
                    var flat = math.length(velocity);
                    if (flat < 1e-3f) continue;
                    var heading = math.dot(velocity / flat, forward);
                    if (heading < kHeadingCos) continue;
                    // Approaching, or no further past the sign than a car length or two.
                    if (math.dot(offset, forward) > kPastSign) continue;
                }
                m_CarsMeasured++;
                m_FastestSeen = math.max(m_FastestSeen, kmh);
                fastest = math.max(fastest, kmh);
            }
            return fastest;
        }

        /// <summary>Shows the other state: the object's prefab replaced in place, flagged Updated so it is redrawn.</summary>
        private void Swap(Entity sign, Entity prefab)
        {
            EntityManager.SetComponentData(sign, new PrefabRef { m_Prefab = prefab });
            if (!EntityManager.HasComponent<Updated>(sign)) EntityManager.AddComponent<Updated>(sign);
#if TESTS
            Swaps++;
#endif
        }

        private Pair? PairOf(Entity prefabEntity)
        {
            if (m_Pairs.TryGetValue(prefabEntity, out var known)) return known;
            var pair = Resolve(prefabEntity);
            m_Pairs[prefabEntity] = pair;
            return pair;
        }

        private Pair? Resolve(Entity prefabEntity)
        {
            if (!m_PrefabSystem.TryGetPrefab(new PrefabRef { m_Prefab = prefabEntity }, out PrefabBase prefab)) return null;
            var record = AssemblyLibrary.ExactFromPrefabName(prefab.name) ?? AssemblyLibrary.Retired(prefab.name);
            if (record == null) return Unresolved(prefab.name, "no library record");
            var main = AssemblyLibrary.Primary(record);
            var altName = main.FeedbackAltPrefabName;
            if (altName == null) return Unresolved(prefab.name, "its other state was never built");
            if (!TryPrefabEntity(main.PrefabName, out var mainEntity)) return Unresolved(prefab.name, "main prefab not registered");
            if (!TryPrefabEntity(altName, out var altEntity)) return Unresolved(prefab.name, $"other state {altName} not registered yet");
            var mainSlow = SpeedFeedback.StateOf(main.Designs) == SpeedFeedback.Slow;
            return new Pair
            {
                Main = mainEntity,
                Alt = altEntity,
                Ok = mainSlow ? altEntity : mainEntity,
                Slow = mainSlow ? mainEntity : altEntity,
                Limit = SpeedFeedback.LimitOf(main.Designs),
                AnyDirection = main.Context?.Options?.Type == "surface",
            };
        }

        /// <summary>Logs once why a speed sign cannot switch; null.</summary>
        private Pair? Unresolved(string prefabName, string why)
        {
            if (m_Explained.Add(prefabName + why)) Mod.log.Info($"Speed feedback: {prefabName} cannot switch yet: {why}.");
            return null;
        }

        private bool TryPrefabEntity(string name, out Entity entity)
        {
            entity = Entity.Null;
            if (!m_PrefabSystem.TryGetPrefab(new PrefabID(nameof(StaticObjectPrefab), name), out var prefab)) return false;
            if (!m_PrefabSystem.TryGetEntity(prefab, out entity)) return false;
            return entity != Entity.Null;
        }

        private void DropUnresolvedPairs()
        {
            m_Scratch.Clear();
            foreach (var pair in m_Pairs) if (pair.Value == null) m_Scratch.Add(pair.Key);
            foreach (var key in m_Scratch) m_Pairs.Remove(key);
        }

        /// <summary>
        /// Puts every feedback sign back on its main structure's prefab, so a save refers only to main structures (which
        /// rebuild their other state wherever the save is loaded). Called just before saving; the signs switch again
        /// with the next traffic check.
        /// </summary>
        public static void RestoreMainStates(EntityManager em)
        {
            var system = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<SpeedFeedbackSystem>();
            system?.RestoreMain(em);
        }

        private void RestoreMain(EntityManager em)
        {
            if (Tracked.Signs.Count == 0) return;
            foreach (var sign in Tracked.Signs)
            {
                if (!em.Exists(sign) || em.HasComponent<Deleted>(sign) || !em.TryGetComponent(sign, out PrefabRef prefabRef)) continue;
                var pair = PairOf(prefabRef.m_Prefab);
                if (pair == null) continue;
                var p = pair.Value;
                if (prefabRef.m_Prefab != p.Main)
                {
                    em.SetComponentData(sign, new PrefabRef { m_Prefab = p.Main });
                    if (!em.HasComponent<Updated>(sign)) em.AddComponent<Updated>(sign);
                }
                if (m_States.TryGetValue(sign, out var state))
                {
                    state.Slow = p.Main == p.Slow;
                    state.CalmSince = 0;
                }
            }
        }
    }
}
