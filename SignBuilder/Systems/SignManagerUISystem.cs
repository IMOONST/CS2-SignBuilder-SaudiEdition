using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Colossal.Entities;
using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Game.UI;
using Game.UI.InGame;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SignBuilderSA.Assemblies;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace SignBuilderSA.Systems
{
    /// <summary>
    /// The sign manager panel (UI: SignManager.tsx): every Sign Builder structure placed in the city, with
    /// "Go to" (select it and bring the camera to it) and "Delete". Binding group "SignBuilderSA".
    /// Values: managerOpen, managerSigns (JSON array). Triggers: setManagerOpen, managerFocus, managerDelete.
    /// The list is only rebuilt while the panel is open, and at most once a second (sooner after a delete).
    /// </summary>
    public partial class SignManagerUISystem : UISystemBase
    {
        private const float kRefreshInterval = 1f;
        /// <summary>Even with no structural change, refresh now and then (library edits, renamed districts).</summary>
        private const float kIdleRefreshInterval = 5f;
        private const string kPrefix = "SignBuilderSA_A_";

        private ValueBinding<bool> m_Open;
        private ValueBinding<string> m_Signs;

        private PrefabSystem m_PrefabSystem;
        private SelectedInfoUISystem m_InfoUISystem;
        private NameSystem m_NameSystem;
        private EntityQuery m_Objects;
        private EntityQuery m_Districts;

        /// <summary>Prefab entity → prefab name if it is a Sign Builder structure, null otherwise (names never change).</summary>
        private readonly Dictionary<Entity, string> m_PrefabNames = new();
        private float m_LastRefresh = float.NegativeInfinity;
        private int m_LastOrderVersion = int.MinValue;
        private bool m_Dirty = true;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_InfoUISystem = World.GetOrCreateSystemManaged<SelectedInfoUISystem>();
            m_NameSystem = World.GetOrCreateSystemManaged<NameSystem>();
            // Placed props, without the kinds of objects a sign never is (keeps the scan short in big cities).
            m_Objects = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Game.Objects.Object>(), ComponentType.ReadOnly<PrefabRef>() },
                None = new[]
                {
                    ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Game.Vehicles.Vehicle>(), ComponentType.ReadOnly<Game.Creatures.Creature>(),
                    ComponentType.ReadOnly<Game.Objects.Tree>(), ComponentType.ReadOnly<Game.Buildings.Building>(),
                },
            });
            m_Districts = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Game.Areas.District>(), ComponentType.ReadOnly<Game.Areas.Node>() },
                None = new[] { ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Deleted>() },
            });

            AddBinding(m_Open = new ValueBinding<bool>(SignBuilderUISystem.Group, "managerOpen", false));
            AddBinding(m_Signs = new ValueBinding<string>(SignBuilderUISystem.Group, "managerSigns", "[]"));
            AddBinding(new TriggerBinding<bool>(SignBuilderUISystem.Group, "setManagerOpen", SetOpen));
            AddBinding(new TriggerBinding<string>(SignBuilderUISystem.Group, "managerFocus", id => Guard(() => FocusOn(id))));
            AddBinding(new TriggerBinding<string>(SignBuilderUISystem.Group, "managerDelete", id => Guard(() => DeleteSign(id))));
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            // A new city (or the main menu): nothing from the last one applies.
            m_PrefabNames.Clear();
            if (m_Open.value) m_Open.Update(false);
            m_Signs.Update("[]");
            m_Dirty = true;
        }

        public void SetOpen(bool open)
        {
            if (open == m_Open.value) return;
            m_Open.Update(open);
            if (open)
            {
                m_Dirty = true;
                Refresh();
            }
            else
            {
                // Nothing to keep up to date while closed; drop the list rather than hold a stale one.
                m_Signs.Update("[]");
            }
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (!m_Open.value) return;
            var now = UnityEngine.Time.realtimeSinceStartup;
            var elapsed = now - m_LastRefresh;
            if (m_Dirty)
            {
                Refresh();
                return;
            }
            if (elapsed < kRefreshInterval) return;
            // Placing or removing objects bumps the query's order version; without that, refresh only now and then.
            if (m_Objects.GetCombinedComponentOrderVersion(false) != m_LastOrderVersion || elapsed >= kIdleRefreshInterval) Refresh();
        }

        private void Refresh()
        {
            m_Dirty = false;
            m_LastRefresh = UnityEngine.Time.realtimeSinceStartup;
            m_LastOrderVersion = m_Objects.GetCombinedComponentOrderVersion(false);
            try
            {
                var json = BuildList();
                if (json != m_Signs.value) m_Signs.Update(json);
            }
            catch (Exception e)
            {
                Mod.log.Warn(e, "Sign manager: could not list the placed signs");
            }
        }

        private string BuildList()
        {
            var result = new JArray();
            var districts = LoadDistricts();
            using var entities = m_Objects.ToEntityArray(Allocator.Temp);
            using var refs = m_Objects.ToComponentDataArray<PrefabRef>(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var name = SignPrefabName(refs[i].m_Prefab);
                if (name == null) continue;
                var record = RecordFor(name);
                if (record == null) continue;
                var entity = entities[i];
                var designs = record.Designs ?? new List<JObject>();
                var legend = string.Join(" / ", designs.Where(d => d != null).Select(FirstLines).Where(t => t.Length > 0));
                var themeToken = designs.Count > 0 ? designs[0]?["theme"] : null;
                var theme = themeToken != null && themeToken.Type == JTokenType.String ? (string)themeToken : "";
                result.Add(new JObject
                {
                    ["entity"] = Format(entity),
                    ["kind"] = KindOf(record.Support),
                    ["legend"] = legend,
                    ["theme"] = theme,
                    ["signs"] = designs.Count,
                    ["district"] = DistrictOf(entity, districts),
                });
            }
            return result.ToString(Formatting.None);
        }

        /// <summary>The Sign Builder prefab name behind a prefab entity, or null for every other prefab (cached).</summary>
        private string SignPrefabName(Entity prefab)
        {
            if (m_PrefabNames.TryGetValue(prefab, out var cached)) return cached;
            var name = AssemblySaveSystem.PrefabNameOf(EntityManager, m_PrefabSystem, prefab);
            if (name == null || !name.StartsWith(kPrefix, StringComparison.Ordinal)) name = null;
            m_PrefabNames[prefab] = name;
            return name;
        }

        /// <summary>
        /// The record of a placed structure: the library's, or (for one removed from the library this session, or
        /// loaded from a save that could not rebuild it here) the recipe the game still keeps for it.
        /// </summary>
        private static AssemblyRecord RecordFor(string prefabName) =>
            AssemblyLibrary.FromPrefabName(prefabName) ?? AssemblyLibrary.Retired(prefabName) ?? AssemblyRestoreSystem.Unresolved(prefabName);

        private AssemblyRecord RecordOf(Entity entity)
        {
            if (entity == Entity.Null || !EntityManager.Exists(entity) || EntityManager.HasComponent<Deleted>(entity)
                || !EntityManager.TryGetComponent(entity, out PrefabRef prefabRef)) return null;
            var name = SignPrefabName(prefabRef.m_Prefab);
            return name == null ? null : RecordFor(name);
        }

        /// <summary>"ground" = roadside posts, "surface" = surface mounted prop, anything else spans the road.</summary>
        private static string KindOf(string support) => support switch
        {
            "ground" => "roadside",
            "surface" => "prop",
            _ => "gantry",
        };

        /// <summary>The first couple of text legends of a design, e.g. "Boston, Portsmouth NH" (as AssetNames does).</summary>
        private static string FirstLines(JObject design)
        {
            var texts = design.SelectTokens("$..text")
                .Where(t => t.Type == JTokenType.String)
                .Select(t => (string)t)
                .Where(t => !string.IsNullOrWhiteSpace(t) && t.Length > 2)
                .Take(2);
            return string.Join(", ", texts);
        }

        private static string Format(Entity entity) => entity.Index.ToString(CultureInfo.InvariantCulture) + ":" + entity.Version.ToString(CultureInfo.InvariantCulture);

        private static bool TryParse(string id, out Entity entity)
        {
            entity = Entity.Null;
            if (string.IsNullOrEmpty(id)) return false;
            var parts = id.Split(':');
            if (parts.Length != 2
                || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var version)) return false;
            entity = new Entity { Index = index, Version = version };
            return entity != Entity.Null;
        }

        /// <summary>Selects the structure (its info panel opens) and has the camera follow it, as the panel's focus button does.</summary>
        private void FocusOn(string id)
        {
            if (!TryParse(id, out var entity) || RecordOf(entity) == null)
            {
                m_Dirty = true;
                return;
            }
            m_InfoUISystem.SetSelection(entity);
            m_InfoUISystem.Focus(entity);
        }

        /// <summary>Removes a structure the way the selected-object panel's Delete does (marked Deleted; the game cleans up).</summary>
        private void DeleteSign(string id)
        {
            if (!TryParse(id, out var entity) || RecordOf(entity) == null)
            {
                m_Dirty = true;
                return;
            }
            if (m_InfoUISystem.selectedEntity == entity) m_InfoUISystem.SetSelection(Entity.Null);
            new RuntimePrefabs().Delete(entity);
            m_Dirty = true;
        }

        private void Guard(Action action)
        {
            try { action(); }
            catch (Exception e) { Mod.log.Warn(e, "Sign manager action failed"); }
        }

        // ---- Districts ----------------------------------------------------------------------------------------------

        private struct DistrictShape
        {
            public string Name;
            public float2 Min, Max;
            public float2[] Points;
        }

        private List<DistrictShape> LoadDistricts()
        {
            var list = new List<DistrictShape>();
            if (m_Districts.IsEmptyIgnoreFilter) return list;
            using var districts = m_Districts.ToEntityArray(Allocator.Temp);
            foreach (var district in districts)
            {
                if (!EntityManager.TryGetBuffer(district, true, out DynamicBuffer<Game.Areas.Node> nodes) || nodes.Length < 3) continue;
                var points = new float2[nodes.Length];
                var min = new float2(float.MaxValue);
                var max = new float2(float.MinValue);
                for (var i = 0; i < nodes.Length; i++)
                {
                    var p = nodes[i].m_Position.xz;
                    points[i] = p;
                    min = math.min(min, p);
                    max = math.max(max, p);
                }
                string name;
                try { name = m_NameSystem.GetRenderedLabelName(district); }
                catch { name = ""; }
                list.Add(new DistrictShape { Name = name ?? "", Min = min, Max = max, Points = points });
            }
            return list;
        }

        private string DistrictOf(Entity entity, List<DistrictShape> districts)
        {
            // Buildings and some objects carry their district; signs normally do not, so test their position.
            if (EntityManager.TryGetComponent(entity, out Game.Areas.CurrentDistrict current) && current.m_District != Entity.Null)
            {
                try { return m_NameSystem.GetRenderedLabelName(current.m_District) ?? ""; }
                catch { return ""; }
            }
            if (districts.Count == 0 || !EntityManager.TryGetComponent(entity, out Game.Objects.Transform transform)) return "";
            var p = transform.m_Position.xz;
            foreach (var d in districts)
            {
                if (p.x < d.Min.x || p.y < d.Min.y || p.x > d.Max.x || p.y > d.Max.y) continue;
                if (Contains(d.Points, p)) return d.Name;
            }
            return "";
        }

        /// <summary>Even-odd point in polygon test on the ground plane.</summary>
        private static bool Contains(float2[] polygon, float2 p)
        {
            var inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }
    }
}
