using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using SignBuilderSA.Core;
using SignBuilderSA.Geometry;
using SignBuilderSA.Assemblies;

namespace SignBuilderSA.Tools
{
    /// <summary>
    /// Finds a road edge near a point, reads its geometry and car lanes, and pairs it with a parallel
    /// one-way edge when the highway is divided. Everything here is read-only.
    /// </summary>
    public sealed class RoadProbe
    {
        public struct LaneInfo
        {
            public float Offset;     // meters to the right of the clicked edge's center line (relative to its tangent)
            public int Direction;    // +1 = traffic along the clicked edge's tangent, -1 = against it
            public float Width;
            public int Carriageway;  // index into Snap.Carriageways (0 = the clicked edge)
        }

        /// <summary>A road running alongside the clicked one (the clicked edge itself is number 0).</summary>
        public struct Carriageway
        {
            public Entity Edge;
            public float Offset;     // center, meters along Right
            public float Width;
            /// <summary>A retaining wall along the road's edge on the snap's left (-Right) / right (+Right) side.</summary>
            public bool WallLeft;
            public bool WallRight;
        }

        public sealed class Snap
        {
            public Entity Edge;
            public string PrefabName;
            public float EdgeLength;
            public float T;
            public float3 Point;
            public float3 Tangent;        // flat, normalized
            public float3 Right;          // flat, normalized, right of Tangent
            /// <summary>The clicked edge and every parallel road within reach (other carriageway, parallel roads).</summary>
            public readonly List<Carriageway> Carriageways = new();
            public readonly List<LaneInfo> Lanes = new();

            public float EdgeWidth => Carriageways.Count > 0 ? Carriageways[0].Width : 0f;

            /// <summary>Lateral extent (min, max along Right) of all carriageways.</summary>
            public float2 Extent => Carriageways.Count == 0 ? float2.zero
                : new float2(Carriageways.Min(c => c.Offset - c.Width * 0.5f), Carriageways.Max(c => c.Offset + c.Width * 0.5f));

            /// <summary>Direction most lanes of the clicked edge flow (+1 along Tangent, -1 against).</summary>
            public int MainDirection => Lanes.Where(l => l.Carriageway == 0).Sum(l => l.Direction) >= 0 ? 1 : -1;

            public override string ToString() =>
                $"{PrefabName} len={EdgeLength:F0} width={EdgeWidth:F1} t={T:F2} at {Point} carriageways={Carriageways.Count} extent={Extent}";
        }

        private readonly EntityManager m_EM;
        private readonly PrefabSystem m_PrefabSystem;

        public RoadProbe(EntityManager em, PrefabSystem prefabSystem)
        {
            m_EM = em;
            m_PrefabSystem = prefabSystem;
        }

        private struct EdgeCandidate
        {
            public Entity Entity;
            public Curve Curve;
            public float Width;
            public string Name;
        }

        private List<EdgeCandidate> RoadEdges()
        {
            using var query = m_EM.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Edge>(), ComponentType.ReadOnly<Curve>(), ComponentType.ReadOnly<Composition>(), ComponentType.ReadOnly<PrefabRef>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() },
            });
            using var entities = query.ToEntityArray(Allocator.Temp);
            var list = new List<EdgeCandidate>();
            foreach (var e in entities)
            {
                if (Candidate(e) is EdgeCandidate c) list.Add(c);
            }
            return list;
        }

        /// <summary>The edge as a road to snap to; null when it is not a road (a path, a track, a pipe).</summary>
        private EdgeCandidate? Candidate(Entity e)
        {
            if (!m_EM.HasComponent<Edge>(e) || !m_EM.HasComponent<Curve>(e) || !m_EM.HasComponent<Composition>(e) || !m_EM.HasComponent<PrefabRef>(e)) return null;
            var prefab = m_EM.GetComponentData<PrefabRef>(e).m_Prefab;
            if (!m_EM.HasComponent<RoadData>(prefab)) return null;
            var composition = m_EM.GetComponentData<Composition>(e).m_Edge;
            if (!m_EM.HasComponent<NetCompositionData>(composition)) return null;
            return new EdgeCandidate
            {
                Entity = e,
                Curve = m_EM.GetComponentData<Curve>(e),
                Width = m_EM.GetComponentData<NetCompositionData>(composition).m_Width,
                Name = m_PrefabSystem.TryGetPrefab(prefab, out PrefabBase p) ? p.name : prefab.ToString(),
            };
        }

        /// <summary>Picks the best road edge near <paramref name="near"/>: highways first, then the widest/longest.</summary>
        public Snap FindNear(float3 near, Action<string> log)
        {
            var edges = RoadEdges();
            log($"roadEdges={edges.Count}");
            if (edges.Count == 0) return null;

            var scored = edges
                .Select(e => (e, dist: MathUtils.Distance(e.Curve.m_Bezier.xz, near.xz, out _)))
                .OrderByDescending(x => x.e.Name.IndexOf("Highway", StringComparison.OrdinalIgnoreCase) >= 0 && x.e.Curve.m_Length > 60f)
                .ThenBy(x => x.dist)
                .ToList();
            log("nearestEdges=" + string.Join(" | ", scored.Take(6).Select(x => $"{x.e.Name} d={x.dist:F0} len={x.e.Curve.m_Length:F0} w={x.e.Width:F1}")));
            return SnapAt(scored[0].e, 0.5f);
        }

        /// <summary>Snaps to road <paramref name="edge"/> at curve parameter <paramref name="t"/> and finds its lanes; null if it is not a road.</summary>
        public Snap SnapAt(Entity edge, float t) => Candidate(edge) is EdgeCandidate road ? SnapAt(road, t) : null;

        private Snap SnapAt(EdgeCandidate edge, float t)
        {
            var bezier = edge.Curve.m_Bezier;
            var point = MathUtils.Position(bezier, t);
            var tangent = MathUtils.Tangent(bezier, t);
            tangent.y = 0;
            tangent = math.normalizesafe(tangent, new float3(0, 0, 1));
            var right = new float3(tangent.z, 0, -tangent.x);

            var snap = new Snap
            {
                Edge = edge.Entity,
                PrefabName = edge.Name,
                EdgeLength = edge.Curve.m_Length,
                T = t,
                Point = point,
                Tangent = tangent,
                Right = right,
            };
            // Only the clicked road: roads alongside join a gantry's span when the player clicks them (AddRoad).
            var walls = Walls(edge.Entity, true);
            snap.Carriageways.Add(new Carriageway { Edge = edge.Entity, Offset = 0f, Width = edge.Width, WallLeft = walls.x, WallRight = walls.y });
            AddLanes(snap, edge.Entity, 0);
            return snap;
        }

        /// <summary>
        /// Adds <paramref name="edge"/> to the snap's roads if the gantry line (through Point along Right) crosses it
        /// while it runs alongside (parallel). Returns its index (an existing one if already known), or -1.
        /// </summary>
        public int AddRoad(Snap snap, Entity edge, float maxDistance = 250f)
        {
            if (!m_EM.HasComponent<Curve>(edge) || !m_EM.HasComponent<Composition>(edge)) return -1;
            var curve = m_EM.GetComponentData<Curve>(edge).m_Bezier;
            // Where the curve crosses the gantry line: the point with no offset along the gantry's tangent.
            float Along(float t) => math.dot(MathUtils.Position(curve, t) - snap.Point, snap.Tangent);
            float best = -1f, bestAbs = float.MaxValue;
            for (var i = 0; i <= 32; i++)
            {
                var t = i / 32f;
                var a = math.abs(Along(t));
                if (a < bestAbs) { bestAbs = a; best = t; }
            }
            for (var step = 1f / 64f; step > 1e-4f; step *= 0.5f)
            {
                var lo = math.max(0f, best - step);
                var hi = math.min(1f, best + step);
                if (math.abs(Along(lo)) < bestAbs) { best = lo; bestAbs = math.abs(Along(lo)); }
                if (math.abs(Along(hi)) < bestAbs) { best = hi; bestAbs = math.abs(Along(hi)); }
            }
            if (bestAbs > 3f) return -1; // the gantry line does not cross it
            var point = MathUtils.Position(curve, best);
            var tangent = MathUtils.Tangent(curve, best);
            tangent.y = 0;
            if (math.abs(math.dot(math.normalizesafe(tangent), snap.Tangent)) < 0.9f) return -1; // not alongside
            var offset = math.dot(point - snap.Point, snap.Right);
            if (math.abs(offset) > maxDistance) return -1;
            for (var i = 0; i < snap.Carriageways.Count; i++)
            {
                var c = snap.Carriageways[i];
                if (math.abs(offset - c.Offset) <= math.max(1.5f, c.Width * 0.5f)) return i;
            }
            var composition = m_EM.GetComponentData<Composition>(edge).m_Edge;
            var width = m_EM.HasComponent<NetCompositionData>(composition) ? m_EM.GetComponentData<NetCompositionData>(composition).m_Width : 8f;
            var sideWalls = Walls(edge, math.dot(math.normalizesafe(tangent), snap.Tangent) > 0f);
            snap.Carriageways.Add(new Carriageway { Edge = edge, Offset = offset, Width = width, WallLeft = sideWalls.x, WallRight = sideWalls.y });
            AddLanes(snap, edge, snap.Carriageways.Count - 1);
            return snap.Carriageways.Count - 1;
        }

        /// <summary>
        /// Retaining walls along the edge (a sunken road, or the walled ramp down to a tunnel), as (left, right) of the
        /// snap: the edge's own sides when it runs the snap's way, swapped when it runs against it.
        /// </summary>
        private bool2 Walls(Entity edge, bool sameWay)
        {
            if (!m_EM.HasComponent<Composition>(edge)) return false;
            var composition = m_EM.GetComponentData<Composition>(edge).m_Edge;
            if (!m_EM.HasComponent<NetCompositionData>(composition)) return false;
            var flags = m_EM.GetComponentData<NetCompositionData>(composition).m_Flags;
            const CompositionFlags.Side wall = CompositionFlags.Side.Lowered | CompositionFlags.Side.LowTransition;
            var left = (flags.m_Left & wall) != 0;
            var right = (flags.m_Right & wall) != 0;
            return sameWay ? new bool2(left, right) : new bool2(right, left);
        }

        // ------------------------------------------------------------------ road information (street, district, class, speed)

        /// <summary>What a sign may say about the road it stands on: names, class, speed limit and lanes.</summary>
        public sealed class RoadInfo
        {
            public Entity Edge;
            /// <summary>The street (the edge's aggregate) as the game labels it; "" when unnamed.</summary>
            public string Street = "";
            /// <summary>The district on the chosen side of the road (or the other side); "" when outside districts.</summary>
            public string District = "";
            /// <summary>"highway", "major", "medium", "local" or "other" (no car lanes).</summary>
            public string RoadClass = "other";
            /// <summary>Speed limit in km/h (as the game's own info displays convert it: m/s × 3.6); 0 when unknown.</summary>
            public int SpeedLimit;
            /// <summary>Driving lanes in the road's main direction.</summary>
            public int Lanes;

            /// <summary>The speed limit as a sign shows it: the nearest multiple of 10 km/h (0 when unknown).</summary>
            public int SignSpeed => SpeedLimit <= 0 ? 0 : Math.Max(10, (int)Math.Round(SpeedLimit / 10.0, MidpointRounding.AwayFromZero) * 10);

            /// <summary>Everything the UI shows, without the nonce: equal keys mean nothing changed.</summary>
            public string Key => $"{Street}\u0001{District}\u0001{RoadClass}\u0001{SpeedLimit}\u0001{Lanes}";
        }

        private static readonly System.Text.RegularExpressions.Regex s_LocaleId =
            new(@"^[A-Za-z0-9_]+\.[A-Za-z0-9_.:\[\]\-]+$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        /// <summary>
        /// A name as the game draws it on the map. The game returns the localization id itself when the active
        /// language has no entry for it; such ids are not names, so they read as unknown ("").
        /// </summary>
        private string LabelOf(Entity entity, Game.UI.NameSystem names)
        {
            if (entity == Entity.Null || names == null || !m_EM.Exists(entity)) return "";
            string name;
            try { name = names.GetRenderedLabelName(entity); }
            catch (Exception e)
            {
                Mod.log.Warn(e, "Reading a road or district name failed");
                return "";
            }
            name = (name ?? "").Trim();
            return name.Length == 0 || s_LocaleId.IsMatch(name) ? "" : name;
        }

        /// <summary>
        /// Reads the road of <paramref name="snap"/> (a real edge): its street (the aggregate the game names), the
        /// district on the side of <paramref name="side"/> (the other side when that one has none), class, speed and
        /// lanes. Null when the snap is not on a road edge.
        /// </summary>
        public RoadInfo ReadInfo(Snap snap, float3 side, Game.UI.NameSystem names)
        {
            if (snap == null || snap.Edge == Entity.Null || !m_EM.Exists(snap.Edge) || !m_EM.HasComponent<Edge>(snap.Edge)) return null;
            var edge = snap.Edge;
            var info = new RoadInfo { Edge = edge };

            if (m_EM.HasComponent<Aggregated>(edge)) info.Street = LabelOf(m_EM.GetComponentData<Aggregated>(edge).m_Aggregate, names);

            if (m_EM.HasComponent<Game.Areas.BorderDistrict>(edge))
            {
                // BorderDistrict sides are those of the edge's own direction, as is Snap.Right (the curve's tangent).
                var border = m_EM.GetComponentData<Game.Areas.BorderDistrict>(edge);
                var right = math.dot(side - snap.Point, snap.Right) >= 0f;
                var first = right ? border.m_Right : border.m_Left;
                var second = right ? border.m_Left : border.m_Right;
                info.District = LabelOf(first, names);
                if (info.District.Length == 0) info.District = LabelOf(second, names);
            }

            var own = snap.Lanes.Where(l => l.Carriageway == 0).ToList();
            var main = snap.MainDirection;
            info.Lanes = own.Count(l => l.Direction == main);

            var prefab = m_EM.HasComponent<PrefabRef>(edge) ? m_EM.GetComponentData<PrefabRef>(edge).m_Prefab : Entity.Null;
            var road = prefab != Entity.Null && m_EM.HasComponent<RoadData>(prefab) ? m_EM.GetComponentData<RoadData>(prefab) : default;
            info.SpeedLimit = (int)math.round(LaneSpeed(edge, road.m_SpeedLimit) * 3.6f);
            info.RoadClass = ClassOf(own.Count, info.Lanes, (road.m_Flags & Game.Prefabs.RoadFlags.UseHighwayRules) != 0, snap.PrefabName ?? "");
            return info;
        }

        /// <summary>
        /// The fastest car lane's current limit in m/s (policies change lanes' limits), or the road prefab's limit when
        /// the edge has no car lanes yet.
        /// </summary>
        private float LaneSpeed(Entity edge, float prefabSpeed)
        {
            var best = 0f;
            if (m_EM.HasBuffer<Game.Net.SubLane>(edge))
            {
                var subLanes = m_EM.GetBuffer<Game.Net.SubLane>(edge, true);
                for (var i = 0; i < subLanes.Length; i++)
                {
                    var lane = subLanes[i].m_SubLane;
                    if (!m_EM.HasComponent<Game.Net.CarLane>(lane)) continue;
                    var car = m_EM.GetComponentData<Game.Net.CarLane>(lane);
                    if ((car.m_Flags & CarLaneFlags.Unsafe) != 0) continue;
                    best = math.max(best, car.m_SpeedLimit);
                }
            }
            return best > 0f ? best : math.max(prefabSpeed, 0f);
        }

        /// <summary>
        /// The road's class: highway rules make a highway; otherwise small/alley/gravel roads are local and the rest
        /// go by their lanes per direction (3+ major, 2 medium, 1 local). No car lanes at all: "other".
        /// </summary>
        private static string ClassOf(int carLanes, int mainLanes, bool highwayRules, string prefabName)
        {
            if (carLanes == 0) return "other";
            if (highwayRules) return "highway";
            bool Has(string word) => prefabName.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;
            if (Has("Highway")) return "highway";
            if (Has("Small") || Has("Alley") || Has("Gravel") || Has("Dirt")) return "local";
            if (Has("Large")) return "major";
            if (Has("Medium")) return "medium";
            return mainLanes >= 3 ? "major" : mainLanes == 2 ? "medium" : "local";
        }

        /// <summary>The road edge nearest <paramref name="point"/> (horizontally) within <paramref name="maxDistance"/>, snapped there; null if none.</summary>
        public Snap NearestRoad(float3 point, float maxDistance)
        {
            EdgeCandidate? best = null;
            var bestDistance = maxDistance;
            var bestT = 0.5f;
            foreach (var e in RoadEdges())
            {
                // Bounds check first: most of the city's edges are far away.
                var bounds = MathUtils.Bounds(e.Curve.m_Bezier.xz);
                if (point.x < bounds.min.x - maxDistance - e.Width || point.x > bounds.max.x + maxDistance + e.Width
                    || point.z < bounds.min.y - maxDistance - e.Width || point.z > bounds.max.y + maxDistance + e.Width) continue;
                var distance = MathUtils.Distance(e.Curve.m_Bezier.xz, point.xz, out var t) - e.Width * 0.5f;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestT = t;
                best = e;
            }
            return best is EdgeCandidate road ? SnapAt(road, math.clamp(bestT, 0.02f, 0.98f)) : null;
        }

        /// <summary>The edges of the street <paramref name="edge"/> belongs to (just the edge when it has none), at most <paramref name="max"/>.</summary>
        public List<Entity> StreetEdges(Entity edge, int max = 256)
        {
            var list = new List<Entity>();
            if (m_EM.HasComponent<Aggregated>(edge))
            {
                var aggregate = m_EM.GetComponentData<Aggregated>(edge).m_Aggregate;
                if (aggregate != Entity.Null && m_EM.HasBuffer<AggregateElement>(aggregate))
                {
                    var elements = m_EM.GetBuffer<AggregateElement>(aggregate, true);
                    for (var i = 0; i < elements.Length && list.Count < max; i++)
                    {
                        if (m_EM.HasComponent<Curve>(elements[i].m_Edge)) list.Add(elements[i].m_Edge);
                    }
                }
            }
            if (!list.Contains(edge)) list.Add(edge);
            return list;
        }

        /// <summary>A road edge's width (its composition's), or 8 m when unknown.</summary>
        public float WidthOf(Entity edge)
        {
            if (!m_EM.HasComponent<Composition>(edge)) return 8f;
            var composition = m_EM.GetComponentData<Composition>(edge).m_Edge;
            return m_EM.HasComponent<NetCompositionData>(composition) ? m_EM.GetComponentData<NetCompositionData>(composition).m_Width : 8f;
        }

        private void AddLanes(Snap snap, Entity edge, int carriageway)
        {
            if (!m_EM.HasBuffer<Game.Net.SubLane>(edge)) return;
            var subLanes = m_EM.GetBuffer<Game.Net.SubLane>(edge, true);
            for (var i = 0; i < subLanes.Length; i++)
            {
                var lane = subLanes[i].m_SubLane;
                // Master lanes are virtual groupings; the real drivable lanes are their slaves.
                if (!m_EM.HasComponent<Game.Net.CarLane>(lane) || m_EM.HasComponent<MasterLane>(lane) || !m_EM.HasComponent<Curve>(lane)) continue;
                var curve = m_EM.GetComponentData<Curve>(lane).m_Bezier;
                MathUtils.Distance(curve.xz, snap.Point.xz, out var lt);
                var pos = MathUtils.Position(curve, lt);
                var laneTangent = MathUtils.Tangent(curve, lt);
                var prefab = m_EM.GetComponentData<PrefabRef>(lane).m_Prefab;
                var offset = math.dot(pos - snap.Point, snap.Right);
                var direction = math.dot(laneTangent, snap.Tangent) >= 0 ? 1 : -1;
                // Some roads carry more than one car lane entity per physical lane (e.g. per vehicle class);
                // keep one per position so lane choices step through real lanes.
                if (snap.Lanes.Any(l => l.Direction == direction && math.abs(l.Offset - offset) < 0.5f)) continue;
                snap.Lanes.Add(new LaneInfo
                {
                    Offset = offset,
                    Direction = direction,
                    Width = m_EM.HasComponent<NetLaneData>(prefab) ? m_EM.GetComponentData<NetLaneData>(prefab).m_Width : 0f,
                    Carriageway = carriageway,
                });
            }
            snap.Lanes.Sort((a, b) => a.Offset.CompareTo(b.Offset));
        }
    }
}
