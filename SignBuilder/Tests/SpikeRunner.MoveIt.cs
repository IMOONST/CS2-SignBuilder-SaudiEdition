using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Colossal.IO.AssetDatabase;
using Colossal.Mathematics;
using Game.Assets;
using Game.Common;
using Game.Prefabs;
using Game.SceneFlow;
using Game.UI;
using Game.UI.Menu;
using SignBuilder.Assemblies;
using SignBuilder.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SignBuilder.Tests
{
    internal sealed partial class SpikeRunner
    {
        private const string kMoveItSave = "SignBuilder_MoveIt";
        private static string MoveItSignsFile => Path.Combine(SignBuilderRuntime.DataDir, "moveit_signs.txt");

        /// <summary>
        /// Mode "movesave": places signs and changes them the way Move It does when it moves a prop (new position,
        /// Elevation added or removed, detached, Updated), then saves "SignBuilder_MoveIt" and quits. Mode "moveload"
        /// (a fresh boot into that save) then watches each sign for 30 seconds. Reproduces "a sign moved with Move It
        /// shows for a second after loading, then disappears".
        /// </summary>
        private IEnumerator RunMoveItSave()
        {
            yield return WaitForGame();
            yield return new WaitForSecondsRealtime(8f);
            m_Runtime = new RuntimePrefabs();
            yield return Test("movesave", MoveItSave());
            RestoreContinueTarget();
            Finish();
        }

        private IEnumerator RunMoveItLoad()
        {
            yield return WaitForGame();
            m_Runtime = new RuntimePrefabs();
            yield return Test("moveload", MoveItLoad());
            RestoreContinueTarget();
            Finish();
        }

        private IEnumerator MoveItSave()
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var prefabs = m_Runtime.PrefabSystem;
            StaticObjectPrefab sign = null;
            foreach (var record in AssemblyLibrary.All)
            {
                if (prefabs.TryGetPrefab(new PrefabID(nameof(StaticObjectPrefab), record.PrefabName), out var prefab))
                {
                    sign = (StaticObjectPrefab)prefab;
                    break;
                }
            }
            if (sign == null) yield break;
            SpikeReport.Set("movesave.prefab", sign.name);
            using var roads = em.CreateEntityQuery(ComponentType.ReadOnly<Game.Net.Road>(), ComponentType.ReadOnly<Game.Net.Curve>(),
                ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Game.Tools.Temp>());
            using var edges = roads.ToEntityArray(Allocator.Temp);
            if (edges.Length == 0) yield break;
            var curve = em.GetComponentData<Game.Net.Curve>(edges[0]).m_Bezier;
            var mid = MathUtils.Position(curve, 0.5f);
            var along = math.normalizesafe(MathUtils.Tangent(curve, 0.5f) * new float3(1, 0, 1));
            var side = new float3(along.z, 0, -along.x);
            var rotation = quaternion.LookRotationSafe(along, math.up());

            // label: where it is placed, then where "Move It" takes it (offset from the road's middle) and its elevation.
            var cases = new (string label, float3 place, float3 moveTo, float elevation)[]
            {
                ("control", mid + side * 25f, mid + side * 25f, float.NaN),
                ("ontoRoad", mid + side * 25f + along * 20f, mid + along * 20f, 0f),
                ("raised", mid + side * 25f + along * 40f, mid + side * 25f + along * 40f + new float3(0, 6f, 0), 6f),
                ("lowered", mid + side * 25f + along * 60f, mid + side * 25f + along * 60f - new float3(0, 2f, 0), -2f),
                ("raisedOverRoad", mid + side * 25f + along * 80f, mid + along * 80f + new float3(0, 6f, 0), 6f),
            };
            var placed = new List<(string label, Entity entity, float3 position)>();
            foreach (var c in cases)
            {
                var before = InstancesOf(sign.name);
                m_Runtime.Spawn(sign, c.place, rotation);
                var deadline = Time.realtimeSinceStartup + 10f;
                var entity = Entity.Null;
                while (entity == Entity.Null && Time.realtimeSinceStartup < deadline)
                {
                    entity = InstancesOf(sign.name).FirstOrDefault(e => !before.Contains(e));
                    yield return null;
                }
                placed.Add((c.label, entity, c.moveTo));
            }
            yield return new WaitForSecondsRealtime(2f);
            foreach (var (label, entity, _) in placed) SpikeReport.Set($"movesave.placed.{label}", entity == Entity.Null ? "missing" : MoveItDescribe(em, entity));

            // What Move It does at the end of a move: new transform, Elevation set (or removed at 0), detached, Updated.
            for (var i = 0; i < cases.Length; i++)
            {
                var entity = placed[i].entity;
                if (entity == Entity.Null || float.IsNaN(cases[i].elevation)) continue;
                var t = em.GetComponentData<Game.Objects.Transform>(entity);
                t.m_Position = cases[i].moveTo;
                em.SetComponentData(entity, t);
                var elevation = cases[i].elevation;
                if (math.abs(elevation) < 0.0005f)
                {
                    if (em.HasComponent<Game.Objects.Elevation>(entity)) em.RemoveComponent<Game.Objects.Elevation>(entity);
                }
                else
                {
                    var value = new Game.Objects.Elevation(elevation, elevation < 0 ? Game.Objects.ElevationFlags.Lowered : 0);
                    if (em.HasComponent<Game.Objects.Elevation>(entity)) em.SetComponentData(entity, value);
                    else em.AddComponentData(entity, value);
                }
                if (em.HasComponent<Game.Objects.Attached>(entity))
                {
                    var attached = em.GetComponentData<Game.Objects.Attached>(entity);
                    attached.m_Parent = Entity.Null;
                    em.SetComponentData(entity, attached);
                }
                em.AddComponent<Updated>(entity);
                em.AddComponent<Game.Common.BatchesUpdated>(entity);
            }
            yield return new WaitForSecondsRealtime(3f);
            foreach (var (label, entity, _) in placed) SpikeReport.Set($"movesave.moved.{label}", entity == Entity.Null || !em.Exists(entity) ? "missing" : MoveItDescribe(em, entity));
            LookAt(placed[0].entity, 90f);
            yield return new WaitForSecondsRealtime(3f);
            SpikeReport.Screenshot("movesave.png");

            File.WriteAllLines(MoveItSignsFile, new[] { sign.name }.Concat(placed.Where(p => p.entity != Entity.Null && em.Exists(p.entity))
                .Select(p =>
                {
                    var t = em.GetComponentData<Game.Objects.Transform>(p.entity).m_Position;
                    return $"{p.label} {t.x} {t.y} {t.z}";
                })));
            var menu = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<MenuUISystem>();
            var preview = ScreenCaptureHelper.CreateRenderTarget("SignBuilder-MoveIt", 680, 383);
            ScreenCaptureHelper.CaptureScreenshot(Camera.main, preview, new MenuHelpers.SaveGamePreviewSettings());
            var task = GameManager.instance.Save(kMoveItSave, menu.GetSaveInfo(autoSave: false), AssetDatabase.user, preview);
            while (!task.IsCompleted) yield return null;
            SpikeReport.Set("movesave.save", task.Status == TaskStatus.RanToCompletion ? "ok" : task.Exception?.ToString());
            var meta = AssetDatabase.user.GetAssets<SaveGameMetadata>().FirstOrDefault(m => m.name == kMoveItSave);
            if (meta != null) File.WriteAllText(Path.Combine(SignBuilderRuntime.DataDir, "moveit-save.guid"), meta.id.guid.ToString());
            SpikeReport.Set("movesave.guid", meta?.id.guid.ToString() ?? "not found");
        }

        private IEnumerator MoveItLoad()
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            if (!File.Exists(MoveItSignsFile)) yield break;
            var lines = File.ReadAllLines(MoveItSignsFile);
            var name = lines[0];
            var signs = lines.Skip(1).Select(l => l.Split(' ')).Select(p => (label: p[0], position: new float3(float.Parse(p[1]), float.Parse(p[2]), float.Parse(p[3])))).ToList();
            SpikeReport.Set("moveload.expected", signs.Count);
            // Everything the city has of that prefab, matched to the saved signs by position (nearest within 1 m).
            Dictionary<string, Entity> Find()
            {
                var found = new Dictionary<string, Entity>();
                var all = InstancesOf(name);
                foreach (var s in signs)
                {
                    var match = all.FirstOrDefault(e => math.distance(em.GetComponentData<Game.Objects.Transform>(e).m_Position, s.position) < 1f);
                    if (match != Entity.Null) found[s.label] = match;
                }
                return found;
            }
            var start = Time.realtimeSinceStartup;
            var shot = false;
            foreach (var at in new[] { 0f, 1f, 3f, 10f, 30f })
            {
                while (Time.realtimeSinceStartup - start < at) yield return null;
                var found = Find();
                SpikeReport.Set($"moveload.t{at:0}s.count", InstancesOf(name).Count);
                foreach (var s in signs)
                    SpikeReport.Set($"moveload.t{at:0}s.{s.label}", found.TryGetValue(s.label, out var e) ? MoveItDescribe(em, e) : "gone");
                if (!shot && found.TryGetValue("control", out var control))
                {
                    shot = true;
                    LookAt(control, 90f);
                }
                if (at >= 3f) SpikeReport.Screenshot($"moveload_t{at:0}.png");
                SpikeReport.Write();
            }
        }

        /// <summary>A sign object's state: shown or not, its elevation, and every component that can hide or remove it.</summary>
        private static string MoveItDescribe(EntityManager em, Entity e)
        {
            var batches = em.HasBuffer<Game.Rendering.MeshBatch>(e) ? em.GetBuffer<Game.Rendering.MeshBatch>(e, true).Length : 0;
            var t = em.GetComponentData<Game.Objects.Transform>(e).m_Position;
            var elevation = em.HasComponent<Game.Objects.Elevation>(e) ? em.GetComponentData<Game.Objects.Elevation>(e).m_Elevation.ToString("F1") : "none";
            var parent = em.HasComponent<Game.Objects.Attached>(e) ? (em.GetComponentData<Game.Objects.Attached>(e).m_Parent == Entity.Null ? "null" : "set") : "none";
            using var types = em.GetComponentTypes(e, Allocator.Temp);
            var names = string.Join(",", types.Select(c => c.GetManagedType()?.Name).Where(n => n != null).OrderBy(n => n));
            return $"batches={batches} y={t.y:F1} elevation={elevation} attached={parent} overridden={em.HasComponent<Overridden>(e)} deleted={em.HasComponent<Deleted>(e)} components=[{names}]";
        }
    }
}
