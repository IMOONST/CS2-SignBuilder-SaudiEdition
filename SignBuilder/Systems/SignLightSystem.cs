using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Objects;
using Game.Prefabs;
using Game.Rendering;
using Game.Simulation;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace SignBuilderSA.Systems
{
    /// <summary>
    /// A set of structure prefabs and the placed objects using them, collected by <see cref="SignLightTrackingSystem"/>
    /// (every object once after a city loads, then only objects as they are created).
    /// </summary>
    internal sealed class TrackedSigns
    {
        public readonly HashSet<PrefabBase> Prefabs = new();
        /// <summary>The prefabs' entities, once the prefab system has created them.</summary>
        public readonly HashSet<Entity> PrefabEntities = new();
        /// <summary>Placed objects (not previews) whose prefab is one of <see cref="Prefabs"/>.</summary>
        public readonly HashSet<Entity> Signs = new();
        /// <summary>A prefab was tracked whose entity is not known yet.</summary>
        public bool Unresolved;

        public void Track(PrefabBase prefab)
        {
            if (prefab != null && Prefabs.Add(prefab)) Unresolved = true;
        }

        /// <summary>A prefab added during play gets its entity a frame or so later, always before its first object.</summary>
        public void Resolve(PrefabSystem prefabSystem)
        {
            if (!Unresolved) return;
            var resolved = 0;
            foreach (var prefab in Prefabs)
            {
                if (!prefabSystem.TryGetEntity(prefab, out var entity)) continue;
                PrefabEntities.Add(entity);
                resolved++;
            }
            Unresolved = resolved < Prefabs.Count;
        }

        /// <summary>Drops objects that were deleted.</summary>
        public void Prune(EntityManager em, List<Entity> scratch)
        {
            scratch.Clear();
            foreach (var entity in Signs)
            {
                if (!em.Exists(entity) || em.HasComponent<Deleted>(entity)) scratch.Add(entity);
            }
            foreach (var entity in scratch) Signs.Remove(entity);
        }
    }

    /// <summary>
    /// How dark and how murky (fog, rain, snow, dust) it is, smoothed so sign lighting fades rather than jumps. Sampled
    /// from the daylight actually rendered (as street lights are) and the weather of <see cref="ClimateSystem"/>.
    /// </summary>
    internal static class SignAmbience
    {
        /// <summary>0 in daylight, 1 at night.</summary>
        public static float Darkness { get; private set; }
        /// <summary>0 in clear air, 1 in dense fog or heavy precipitation.</summary>
        public static float Murk { get; private set; }
        /// <summary>The rendered daylight (0..1) as last sampled.</summary>
        public static float Daylight { get; private set; } = 1f;

        /// <summary>Seconds for the smoothed values to cover about two thirds of a change.</summary>
        private const float kResponse = 2.5f;
        private static float s_LastTime = -1f;

        public static void Sample(LightingSystem lighting, ClimateSystem climate)
        {
            var now = UnityEngine.Time.realtimeSinceStartup;
            if (now == s_LastTime) return;
            var first = s_LastTime < 0f;
            var dt = first ? 0f : math.min(now - s_LastTime, 1f);
            s_LastTime = now;

            Daylight = math.saturate(lighting.dayLightBrightness);
            var darkness = 1f - math.smoothstep(0.08f, 0.45f, Daylight);
            var murk = 0f;
            if (climate != null)
            {
                float fog = climate.fog;
                float precipitation = climate.precipitation;
                float cloudiness = climate.cloudiness;
                // Fog matters most; rain, snow (and dust-laden storms, which the game models as precipitation) next;
                // a heavy overcast only a little.
                murk = math.saturate(math.max(math.max(fog * 1.25f, precipitation * 0.9f), math.saturate(cloudiness - 0.6f) * 0.5f));
                if (!math.isfinite(murk)) murk = 0f;
            }
            if (first)
            {
                Darkness = darkness;
                Murk = murk;
                return;
            }
            var k = 1f - math.exp(-dt / kResponse);
            Darkness = math.lerp(Darkness, darkness, k);
            Murk = math.lerp(Murk, murk, k);
        }

        /// <summary>Multiplier of LED lettering's glow: 1 on a clear day, up to about 2.9 on a foggy night.</summary>
        public static float LedFactor => 1f + 1.4f * Darkness + 0.9f * Murk * (1f - 0.4f * Darkness);

        /// <summary>Multiplier of the light fixtures' intensity: brighter in fog and rain (they only shine when dark).</summary>
        public static float FixtureFactor => 1f + 0.6f * Murk;

        /// <summary>Daylight as the sign lights see it: fog and rain dim it, so fixtures come on earlier.</summary>
        public static float EffectiveDaylight => Daylight * (1f - 0.55f * Murk);
    }

    /// <summary>
    /// Switches sign light fixtures on when it gets dark, the way the game switches street lights: by the daylight
    /// actually rendered (so the day/night cycle, the debug time of day and photo mode all agree), each sign at its own
    /// threshold so they come on over the dusk rather than all at once. Fog and rain darken that daylight (fixtures come
    /// on earlier) and make the fixtures shine brighter.
    ///
    /// Fixtures are effects forbidden while the object's <see cref="StreetLight"/> state is TurnedOff, as on street
    /// light poles. The game sets that state only on lamps a road or building owns; a sign stands alone, so it is set
    /// here. LED lettering is a neon sign the game keeps lit; how bright it glows follows night and weather
    /// (<see cref="SignLightTrackingSystem"/> applies it before rendering).
    ///
    /// Only the signs themselves are visited: <see cref="SignLightTrackingSystem"/> collects them (every object once
    /// after a city loads, then only objects as they are created), never the whole city on a timer.
    /// </summary>
    public partial class SignLightSystem : GameSystemBase
    {
        /// <summary>Checks for changed daylight this often, in frames.</summary>
        private const int kInterval = 15;
        /// <summary>Fixture brightness changes smaller than this are not applied (each one refreshes the signs' effects).</summary>
        private const float kFixtureStep = 0.04f;

        internal static readonly TrackedSigns Lit = new();
        internal static readonly TrackedSigns Glowing = new();

        /// <summary>Placed signs whose prefab has light fixtures (placement previews keep the state they start with).</summary>
        internal static HashSet<Entity> s_Signs => Lit.Signs;

        /// <summary>A structure prefab with light fixtures.</summary>
        public static void Track(PrefabBase prefab) => Lit.Track(prefab);

        /// <summary>A structure prefab with glowing (LED) lettering.</summary>
        public static void TrackGlow(PrefabBase prefab) => Glowing.Track(prefab);

        internal static IReadOnlyCollection<PrefabBase> Prefabs => Lit.Prefabs;

        private LightingSystem m_Lighting;
        private ClimateSystem m_Climate;
        private PrefabSystem m_Prefabs;
        private int m_Frame;
        private readonly List<Entity> m_Gone = new();
        /// <summary>Each lit prefab entity's fixture intensities as built (Effect.m_Intensity), before scaling.</summary>
        private readonly Dictionary<Entity, float[]> m_BaseIntensity = new();
        private float m_AppliedFixtureFactor = 1f;

#if TESTS
        /// <summary>Test hook: the old way, every object in the city looked at on each pass.</summary>
        public static bool FullScanForTest { get; set; }
        private EntityQuery m_AllObjects;
        public static double LastPassMs { get; private set; }
        public static int LastPassVisited { get; private set; }
        public static int Passes { get; private set; }
#endif

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Lighting = World.GetOrCreateSystemManaged<LightingSystem>();
            m_Climate = World.GetOrCreateSystemManaged<ClimateSystem>();
            m_Prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
#if TESTS
            m_AllObjects = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<PrefabRef>(), ComponentType.ReadOnly<Game.Objects.Object>() },
                None = new[] { ComponentType.ReadOnly<Deleted>() },
            });
#endif
        }

        protected override void OnUpdate()
        {
            if (Lit.Prefabs.Count == 0 && Glowing.Prefabs.Count == 0) return;
            if (++m_Frame % kInterval != 0) return;
            SignAmbience.Sample(m_Lighting, m_Climate);
            if (Lit.Prefabs.Count == 0) return;
#if TESTS
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Passes++;
            if (FullScanForTest)
            {
                LegacyPass();
                LastPassMs = watch.Elapsed.TotalMilliseconds;
                return;
            }
            LastPassVisited = Lit.Signs.Count;
#endif
            var refresh = ScaleFixtures();
            var daylight = SignAmbience.EffectiveDaylight;
            m_Gone.Clear();
            foreach (var entity in Lit.Signs)
            {
                if (!EntityManager.Exists(entity) || EntityManager.HasComponent<Deleted>(entity))
                {
                    m_Gone.Add(entity);
                    continue;
                }
                Apply(entity, daylight, refresh);
            }
            foreach (var entity in m_Gone) Lit.Signs.Remove(entity);
#if TESTS
            LastPassMs = watch.Elapsed.TotalMilliseconds;
#endif
        }

        /// <summary>
        /// Scales the fixtures of every lit prefab (its Effect buffer, which the game copies into each lit effect when
        /// the effect is refreshed) by the weather. True when it changed enough that lit signs need their effects refreshed.
        /// </summary>
        private bool ScaleFixtures()
        {
            var target = SignAmbience.FixtureFactor;
            var changed = math.abs(target - m_AppliedFixtureFactor) >= kFixtureStep;
            var newPrefabs = false;
            foreach (var prefab in Lit.Prefabs)
            {
                if (!m_Prefabs.TryGetEntity(prefab, out var entity) || !EntityManager.HasBuffer<Effect>(entity)) continue;
                if (m_BaseIntensity.ContainsKey(entity)) continue;
                var effects = EntityManager.GetBuffer<Effect>(entity, true);
                var bases = new float[effects.Length];
                for (var i = 0; i < bases.Length; i++) bases[i] = effects[i].m_Intensity;
                m_BaseIntensity[entity] = bases;
                newPrefabs = true;
            }
            if (!changed && !newPrefabs) return false;
            var factor = changed ? target : m_AppliedFixtureFactor;
            foreach (var pair in m_BaseIntensity)
            {
                if (!EntityManager.Exists(pair.Key) || !EntityManager.HasBuffer<Effect>(pair.Key)) continue;
                var effects = EntityManager.GetBuffer<Effect>(pair.Key);
                var n = math.min(effects.Length, pair.Value.Length);
                for (var i = 0; i < n; i++)
                {
                    var effect = effects[i];
                    effect.m_Intensity = pair.Value[i] * factor;
                    effects[i] = effect;
                }
            }
            m_AppliedFixtureFactor = factor;
            return changed;
        }

#if TESTS
        /// <summary>The pass as it was before 1.0.1: every object, its prefab read one at a time.</summary>
        private void LegacyPass()
        {
            var prefabs = new HashSet<Entity>();
            foreach (var prefab in Lit.Prefabs)
            {
                if (m_Prefabs.TryGetEntity(prefab, out var entity)) prefabs.Add(entity);
            }
            var daylight = SignAmbience.EffectiveDaylight;
            using var entities = m_AllObjects.ToEntityArray(Allocator.Temp);
            LastPassVisited = entities.Length;
            foreach (var entity in entities)
            {
                if (!prefabs.Contains(EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab)) continue;
                Apply(entity, daylight, false);
            }
        }
#endif

        private void Apply(Entity entity, float daylight, bool refresh)
        {
            // On below 20-30 % daylight, the same per-object spread as street lights.
            var threshold = 0.2f + 0.1f * (math.hash(new int2(entity.Index, entity.Version)) % 1000u) / 1000f;
            var on = daylight < threshold;
            var state = on ? StreetLightState.None : StreetLightState.TurnedOff;
            if (!EntityManager.HasComponent<StreetLight>(entity))
            {
                EntityManager.AddComponentData(entity, new StreetLight { m_State = state });
            }
            else if (EntityManager.GetComponentData<StreetLight>(entity).m_State == state)
            {
                // Same state: only lit fixtures need refreshing when their brightness changed.
                if (!(refresh && on)) return;
            }
            else
            {
                EntityManager.SetComponentData(entity, new StreetLight { m_State = state });
            }
            if (!EntityManager.HasComponent<EffectsUpdated>(entity)) EntityManager.AddComponent<EffectsUpdated>(entity);
        }
    }

    /// <summary>
    /// Collects the objects <see cref="SignLightSystem"/> switches, the LED signs whose glow follows night and weather and
    /// the speed feedback signs <see cref="SpeedFeedbackSystem"/> swaps: every object once after a city loads, then only
    /// objects created this frame (they carry the game's Created tag until the frame's modifications end).
    ///
    /// It also applies the LED glow: runs at the end of the frame's modifications, before rendering, so an emissive
    /// refresh flagged here is counted by the game's upload preparation and uploaded the same frame.
    /// </summary>
    public partial class SignLightTrackingSystem : GameSystemBase
    {
        /// <summary>Kept for the tests: whether any tracked prefab still lacks its entity.</summary>
        internal static bool PrefabsChanged
        {
            get => SignLightSystem.Lit.Unresolved || SignLightSystem.Glowing.Unresolved || SpeedFeedbackSystem.Tracked.Unresolved;
            set
            {
                SignLightSystem.Lit.Unresolved = value;
                SignLightSystem.Glowing.Unresolved = value;
                SpeedFeedbackSystem.Tracked.Unresolved = value;
            }
        }

        /// <summary>Glow changes smaller than this are not applied (each one re-uploads the LED signs' light data).</summary>
        private const float kGlowStep = 0.03f;
        /// <summary>Applies the glow at most this often, in frames.</summary>
        private const int kGlowInterval = 10;

        private PrefabSystem m_PrefabSystem;
        private LightingSystem m_Lighting;
        private ClimateSystem m_Climate;
        private EntityQuery m_Objects;
        private EntityQuery m_Created;
        private bool m_FullPass;
        private int m_Frame;
        private float m_AppliedGlow = 1f;
        private readonly List<Entity> m_Scratch = new();
        /// <summary>Per LED mesh prefab entity: its neon lights' intensities as built (m_Color.w, m_Color2.w), -1 for other lights.</summary>
        private readonly Dictionary<Entity, float2[]> m_GlowBase = new();
        private readonly HashSet<PrefabBase> m_GlowResolved = new();

#if TESTS
        public static int FullPasses { get; private set; }
        public static double LastFullPassMs { get; private set; }
        public static int LastFullPassObjects { get; private set; }
#endif

        protected override void OnCreate()
        {
            base.OnCreate();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_Lighting = World.GetOrCreateSystemManaged<LightingSystem>();
            m_Climate = World.GetOrCreateSystemManaged<ClimateSystem>();
            m_Objects = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<PrefabRef>(), ComponentType.ReadOnly<Game.Objects.Object>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() },
            });
            m_Created = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<PrefabRef>(), ComponentType.ReadOnly<Game.Objects.Object>(), ComponentType.ReadOnly<Created>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() },
            });
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            SignLightSystem.Lit.Signs.Clear();
            SignLightSystem.Glowing.Signs.Clear();
            SpeedFeedbackSystem.Tracked.Signs.Clear();
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            // The loaded city's signs (their prefabs were registered before it loaded).
            m_FullPass = true;
            PrefabsChanged = true;
        }

        protected override void OnUpdate()
        {
            var lit = SignLightSystem.Lit;
            var glowing = SignLightSystem.Glowing;
            var feedback = SpeedFeedbackSystem.Tracked;
            if (lit.Prefabs.Count == 0 && glowing.Prefabs.Count == 0 && feedback.Prefabs.Count == 0) return;
            lit.Resolve(m_PrefabSystem);
            glowing.Resolve(m_PrefabSystem);
            feedback.Resolve(m_PrefabSystem);
            if (m_FullPass)
            {
                m_FullPass = false;
#if TESTS
                var watch = System.Diagnostics.Stopwatch.StartNew();
                LastFullPassObjects = Collect(m_Objects);
                FullPasses++;
                LastFullPassMs = watch.Elapsed.TotalMilliseconds;
#else
                Collect(m_Objects);
#endif
            }
            else if (!m_Created.IsEmptyIgnoreFilter)
            {
                Collect(m_Created);
            }
            if (glowing.Prefabs.Count > 0 && ++m_Frame % kGlowInterval == 0) ApplyGlow(glowing);
        }

        private int Collect(EntityQuery query)
        {
            var lit = SignLightSystem.Lit;
            var glowing = SignLightSystem.Glowing;
            var feedback = SpeedFeedbackSystem.Tracked;
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var refs = query.ToComponentDataArray<PrefabRef>(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var prefab = refs[i].m_Prefab;
                if (lit.PrefabEntities.Contains(prefab)) lit.Signs.Add(entities[i]);
                if (glowing.PrefabEntities.Contains(prefab)) glowing.Signs.Add(entities[i]);
                if (feedback.PrefabEntities.Contains(prefab)) feedback.Signs.Add(entities[i]);
            }
            return entities.Length;
        }

        /// <summary>
        /// Scales the neon lights of every LED structure's meshes (their ProceduralLight intensity, which the game multiplies
        /// into each instance's light data when it uploads it) and flags the placed LED signs for a re-upload. Applied only
        /// when the brightness moved noticeably, so most frames do nothing.
        /// </summary>
        private void ApplyGlow(TrackedSigns glowing)
        {
            SignAmbience.Sample(m_Lighting, m_Climate);
            var newMeshes = ResolveGlowMeshes(glowing);
            var target = SignAmbience.LedFactor;
            if (!newMeshes && math.abs(target - m_AppliedGlow) < kGlowStep) return;
            foreach (var pair in m_GlowBase)
            {
                if (!EntityManager.Exists(pair.Key) || !EntityManager.HasBuffer<ProceduralLight>(pair.Key)) continue;
                var lights = EntityManager.GetBuffer<ProceduralLight>(pair.Key);
                var n = math.min(lights.Length, pair.Value.Length);
                for (var i = 0; i < n; i++)
                {
                    var b = pair.Value[i];
                    if (b.x < 0f) continue;
                    var light = lights[i];
                    light.m_Color.w = b.x * target;
                    light.m_Color2.w = b.y * target;
                    lights[i] = light;
                }
            }
            m_AppliedGlow = target;
            glowing.Prune(EntityManager, m_Scratch);
            foreach (var sign in glowing.Signs)
            {
                if (!EntityManager.HasBuffer<Emissive>(sign)) continue;
                var emissives = EntityManager.GetBuffer<Emissive>(sign);
                for (var i = 0; i < emissives.Length; i++)
                {
                    var emissive = emissives[i];
                    if (emissive.m_Updated) continue;
                    emissive.m_Updated = true;
                    emissives[i] = emissive;
                }
            }
        }

        /// <summary>Records the built intensities of LED meshes not seen yet; true if any were added.</summary>
        private bool ResolveGlowMeshes(TrackedSigns glowing)
        {
            if (m_GlowResolved.Count == glowing.Prefabs.Count) return false;
            var added = false;
            foreach (var prefab in glowing.Prefabs)
            {
                if (m_GlowResolved.Contains(prefab) || !(prefab is ObjectGeometryPrefab objectPrefab) || objectPrefab.m_Meshes == null) continue;
                var meshes = new List<RenderPrefabBase>();
                foreach (var info in objectPrefab.m_Meshes)
                {
                    if (info.m_Mesh == null) continue;
                    meshes.Add(info.m_Mesh);
                    if (info.m_Mesh.TryGet<LodProperties>(out var lods) && lods.m_LodMeshes != null) meshes.AddRange(lods.m_LodMeshes);
                }
                var ready = true;
                foreach (var mesh in meshes)
                {
                    if (mesh == null) continue;
                    if (!m_PrefabSystem.TryGetEntity(mesh, out var entity) || !EntityManager.HasBuffer<ProceduralLight>(entity))
                    {
                        ready = false;
                        continue;
                    }
                    if (m_GlowBase.ContainsKey(entity)) continue;
                    var lights = EntityManager.GetBuffer<ProceduralLight>(entity, true);
                    if (lights.Length == 0)
                    {
                        // Filled in by the game's mesh initialization a frame or so after the prefab is added.
                        ready = false;
                        continue;
                    }
                    var bases = new float2[lights.Length];
                    for (var i = 0; i < bases.Length; i++)
                    {
                        bases[i] = lights[i].m_Purpose == EmissiveProperties.Purpose.NeonSign
                            ? new float2(lights[i].m_Color.w, lights[i].m_Color2.w)
                            : new float2(-1f, -1f);
                    }
                    m_GlowBase[entity] = bases;
                    added = true;
                }
                if (ready) m_GlowResolved.Add(prefab);
            }
            return added;
        }
    }
}
