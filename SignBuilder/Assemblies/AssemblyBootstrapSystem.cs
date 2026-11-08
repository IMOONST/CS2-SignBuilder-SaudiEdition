using System.Collections.Generic;
using Colossal.IO.AssetDatabase;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
using SignBuilderSA.Core;
using SignBuilderSA.Geometry;
using SignBuilderSA.Tools;

namespace SignBuilderSA.Assemblies
{
    /// <summary>
    /// Keeps assembly prefabs alive across sessions: before any save is deserialized it re-registers
    /// every recorded assembly from its stored mesh/material asset ids (so saved instances resolve),
    /// and once prefabs are initialized it clears the Overridable and Brushable flags (a gantry always
    /// overlaps a road, and Overridable props get hidden by roads; Brushable ones are dropped onto the
    /// terrain whenever the ground under them is updated, sinking signs on raised roads and bridges).
    /// </summary>
    public partial class AssemblyBootstrapSystem : GameSystemBase
    {
        private static readonly List<StaticObjectPrefab> s_Pending = new();
        private static bool s_Registered;

        public static void TrackNewPrefab(StaticObjectPrefab prefab)
        {
            if (prefab != null) s_Pending.Add(prefab);
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            if (s_Registered || !mode.IsGameOrEditor()) return;
            s_Registered = true;
            AssemblyCleanup.SweepOrphans();
            var prefabs = new RuntimePrefabs();
            int ok = 0, missing = 0;
            foreach (var record in AssemblyLibrary.All)
            {
                if (!AssetDatabase.global.TryGetAsset(Colossal.Hash128.Parse(record.GeometryId), out GeometryAsset geometry))
                {
                    missing++;
                    continue;
                }
                var surfaces = new List<SurfaceAsset>();
                foreach (var id in record.SurfaceIds)
                {
                    if (AssetDatabase.global.TryGetAsset(Colossal.Hash128.Parse(id), out SurfaceAsset surface)) surfaces.Add(surface);
                }
                if (surfaces.Count != record.SurfaceIds.Count)
                {
                    missing++;
                    continue;
                }
                var b = record.Bounds;
                var bounds = new Bounds3(new float3(b[0], b[1], b[2]), new float3(b[3], b[4], b[5]));
                (GeometryAsset, int, int)? lod = null;
                if (record.LodGeometryId != null && AssetDatabase.global.TryGetAsset(Colossal.Hash128.Parse(record.LodGeometryId), out GeometryAsset lodGeometry))
                {
                    lod = (lodGeometry, record.LodIndexCount, record.LodVertexCount);
                }
                var prefab = prefabs.Register(record.PrefabName, geometry, surfaces.ToArray(), bounds, record.IndexCount, record.VertexCount, lod, record.LightSpots, record.GlowMeshes);
                TrackNewPrefab(prefab);
                // Speed feedback structures (both states) are swapped as traffic passes them.
                if (SpeedFeedback.IsFeedback(record.Designs)) Systems.SpeedFeedbackSystem.TrackPrefab(prefab);
                ok++;
            }
            Mod.log.Info($"Registered {ok} sign assemblies before load ({missing} with missing assets)");
        }

        protected override void OnUpdate()
        {
            if (s_Pending.Count == 0) return;
            for (var i = s_Pending.Count - 1; i >= 0; i--)
            {
                if (ClearOverridableNow(s_Pending[i])) s_Pending.RemoveAt(i);
            }
        }

        /// <summary>
        /// Clears Overridable and Brushable once the prefab is initialized; returns false if it is not yet. The game's
        /// GroundHeightSystem moves every Overridable or Brushable object without an Elevation onto the terrain when
        /// the ground around it is updated (a road changed nearby, as Road Builder does to its roads when a city loads):
        /// a sign standing at road height on an embankment or bridge sank into the ground under it.
        /// </summary>
        public static bool ClearOverridableNow(StaticObjectPrefab prefab)
        {
            if (prefab == null) return true;
            var world = World.DefaultGameObjectInjectionWorld;
            var prefabSystem = world.GetOrCreateSystemManaged<PrefabSystem>();
            var em = world.EntityManager;
            var entity = prefabSystem.GetEntity(prefab);
            if (entity == Entity.Null || !em.HasComponent<ObjectGeometryData>(entity)) return false;
            var data = em.GetComponentData<ObjectGeometryData>(entity);
            data.m_Flags &= ~(Game.Objects.GeometryFlags.Overridable | Game.Objects.GeometryFlags.Brushable);
            em.SetComponentData(entity, data);
            return true;
        }
    }
}
