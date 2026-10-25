using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    /// <summary>Checks the exported collision contract and real CharacterController traversal, without saving scene changes.</summary>
    internal static class MapProductionChecks
    {
        private const float Dt = 1f / 60f;

        [Serializable] private sealed class Result
        {
            public int floorSamples, floorFailures, laneRides, blockedRides, falls, markerFailures;
            public int sealChecks, sealFailures, wanderRuns, wanderFailures, terrainColliders, primitiveColliders;
            public int renderTriangles, renderObjects;
            public string[] errors;
        }

        internal static void Run(MapProductionTools tools)
        {
            MapProductionTools.RequireSafeEditor();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var manifest = tools.LoadManifest();
                EditorSceneManager.OpenScene(tools.PreviewScene, OpenSceneMode.Single);
                var cc = Object.FindAnyObjectByType<CharacterController>();
                if (cc == null) throw new InvalidOperationException("The production preview has no CharacterController.");
                int mask = Physics.DefaultRaycastLayers & ~(1 << cc.gameObject.layer);
                Physics.SyncTransforms();
                var result = new Result();
                var errors = new List<string>();
                CheckCounts(manifest, result, errors);
                CheckContents(manifest, tools, result, errors);
                float walkableNormalY = Mathf.Cos(cc.slopeLimit * Mathf.Deg2Rad);
                foreach (var point in manifest.floorSamples)
                {
                    result.floorSamples++;
                    if (Ground(point.Value, mask, out var hit) && Mathf.Abs(hit.point.y - point.y) < 0.06f && hit.normal.y >= walkableNormalY) continue;
                    result.floorFailures++;
                    AddError(errors, $"Floor at {point.Value:F2}: got {hit.point:F3}, normalY={hit.normal.y:F3} on " +
                        (hit.collider != null ? hit.collider.name : "nothing"));
                }
                foreach (float speed in new[] { 6f, 10.5f, 18f })
                {
                    foreach (var lane in manifest.lanes)
                    {
                        foreach (float offset in lane.offsets != null && lane.offsets.Length > 0 ? lane.offsets : new[] { -1.1f, 0f, 1.1f })
                        {
                            var points = new List<Vector3>();
                            for (int i = 0; i < lane.points.Length; i++)
                            {
                                Vector3 tangent = lane.points[Mathf.Min(i + 1, lane.points.Length - 1)].Value -
                                    lane.points[Mathf.Max(i - 1, 0)].Value;
                                tangent.y = 0;
                                points.Add(lane.points[i].Value + Vector3.Cross(Vector3.up, tangent.normalized) * offset);
                            }
                            Ride(cc, points, speed, mask, result, errors, lane.name + " offset " + offset);
                        }
                    }
                    foreach (string route in tools.IsCity ? Array.Empty<string>() : new[] { "A", "B", "C" })
                    {
                        var entry = Array.Find(manifest.lanes, l => l.name == route);
                        var exit = Array.Find(manifest.lanes, l => l.name == "Exit" + route);
                        Ride(cc, new List<Vector3> { entry.points[entry.points.Length - 1].Value, exit.points[0].Value },
                            speed, mask, result, errors, "Arena " + route);
                    }
                }
                CheckMarkers(manifest, cc, mask, result, errors);
                CheckSeals(manifest, cc, mask, result, errors);
                Wander(manifest, cc, mask, result, errors);
                result.errors = errors.ToArray();
                string path = Path.Combine(tools.Output, "unity-validation.json");
                File.WriteAllText(path, JsonUtility.ToJson(result, true) + "\n");
                Debug.Log("MAP_PRODUCTION_CHECK " + JsonUtility.ToJson(result));
                if (errors.Count > 0) throw new InvalidOperationException("Environment collision validation failed; see " + path);
            }
            finally { MapProductionTools.RestoreSetup(setup); }
        }

        private static void AddError(List<string> errors, string error)
        {
            if (errors.Count < 150) errors.Add(error);
        }

        private static void CheckCounts(MapProductionTools.Manifest data, Result result, List<string> errors)
        {
            foreach (var collider in Object.FindObjectsByType<Collider>())
            {
                if (collider.name.StartsWith("COL_Mesh_", StringComparison.Ordinal))
                {
                    result.terrainColliders++;
                    if (!(collider is MeshCollider mesh) || mesh.convex || mesh.sharedMesh == null)
                        AddError(errors, "Invalid terrain collider " + collider.name);
                }
                else if (collider.name.StartsWith("COL_", StringComparison.Ordinal)) result.primitiveColliders++;
            }
            if (result.terrainColliders != data.terrain.Length) AddError(errors, "Terrain collider count differs from manifest.");
            if (result.primitiveColliders != data.colliders.Length) AddError(errors, "Primitive collider count differs from manifest.");
        }

        private static bool Ground(Vector3 point, int mask, out RaycastHit hit)
        {
            var hits = Physics.RaycastAll(new Vector3(point.x, 120, point.z), Vector3.down, 220, mask, QueryTriggerInteraction.Ignore);
            hit = default;
            foreach (var candidate in hits)
            {
                if (!IsFloor(candidate.collider.name)) continue;
                if (hit.collider != null && hit.point.y > candidate.point.y) continue;
                hit = candidate;
            }
            return hit.collider != null;
        }

        private static bool IsFloor(string name) => name.StartsWith("COL_Mesh_", StringComparison.Ordinal) || name.StartsWith("COL_Ground_", StringComparison.Ordinal) || name.StartsWith("COL_Ramp_", StringComparison.Ordinal) || name == "COL_Ramp_Deck";

        private static void CheckContents(MapProductionTools.Manifest data, MapProductionTools tools, Result result, List<string> errors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(tools.PrefabPath);
            if (prefab.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 ||
                prefab.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
                prefab.GetComponentsInChildren<Light>(true).Length != 0 ||
                prefab.GetComponentsInChildren<Camera>(true).Length != 0)
                AddError(errors, "The environment prefab contains a runtime script, Rigidbody, camera or presentation light.");
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                result.renderObjects++;
                for (int s = 0; s < mesh.subMeshCount; s++) result.renderTriangles += (int)mesh.GetIndexCount(s) / 3;
                var renderer = filter.GetComponent<MeshRenderer>();
                foreach (var material in renderer.sharedMaterials)
                    if (material == null || !AssetDatabase.GetAssetPath(material).StartsWith(tools.Art + "Materials/", StringComparison.Ordinal))
                        AddError(errors, filter.name + ": missing or external environment material.");
            }
            if (result.renderTriangles != data.expected.render_triangles || result.renderObjects != data.expected.render_objects)
                AddError(errors, "Unity render geometry count differs from the Blender source.");
        }

        private static float Feet(CharacterController cc) => cc.transform.TransformPoint(cc.center).y - cc.height * cc.transform.lossyScale.y * 0.5f;

        private static void Place(CharacterController cc, Vector3 feet)
        {
            float offset = cc.center.y - cc.height * 0.5f;
            cc.enabled = false;
            cc.transform.position = feet - Vector3.up * offset;
            cc.enabled = true;
            Physics.SyncTransforms();
        }

        private static void Ride(CharacterController cc, List<Vector3> points, float speed, int mask,
            Result result, List<string> errors, string name)
        {
            result.laneRides++;
            if (!Ground(points[0], mask, out var start))
            {
                result.falls++;
                AddError(errors, name + ": start has no ground.");
                return;
            }
            Place(cc, start.point + Vector3.up * 0.15f);
            float vy = 0;
            for (int w = 1; w < points.Count; w++)
            {
                float stuck = 0;
                int maxSteps = Mathf.CeilToInt(Vector3.Distance(cc.transform.position, points[w]) / speed / Dt) + 240;
                bool arrived = false;
                for (int i = 0; i < maxSteps; i++)
                {
                    Vector3 before = cc.transform.position;
                    Vector3 toward = points[w] - before;
                    toward.y = 0;
                    if (toward.magnitude < 0.45f) { arrived = true; break; }
                    vy = Mathf.Max(-35, vy - 25f * Dt);
                    var flags = cc.Move(toward.normalized * Mathf.Min(speed * Dt, toward.magnitude) + Vector3.up * vy * Dt);
                    if ((flags & CollisionFlags.Below) != 0) vy = -2;
                    Vector3 after = cc.transform.position;
                    Vector3 moved = after - before; moved.y = 0;
                    stuck = moved.magnitude < speed * Dt * 0.15f ? stuck + Dt : 0;
                    if (stuck > 0.7f)
                    {
                        result.blockedRides++;
                        AddError(errors, $"{name} at {speed} m/s blocked near {after:F2} toward {points[w]:F2}.");
                        return;
                    }
                    if (!Ground(after, mask, out var floor) || Feet(cc) < floor.point.y - 0.6f)
                    {
                        result.falls++;
                        AddError(errors, $"{name} at {speed} m/s fell at {after:F2}.");
                        return;
                    }
                }
                if (arrived) continue;
                result.blockedRides++;
                AddError(errors, $"{name} at {speed} m/s did not reach waypoint {w}.");
                return;
            }
        }

        private static void CheckMarkers(MapProductionTools.Manifest data, CharacterController cc, int mask, Result result, List<string> errors)
        {
            foreach (var marker in data.markers)
            {
                if (marker.kind != "enemy_spawn" && marker.kind != "stronger_enemy" && marker.kind != "battery_charge" && marker.kind != "player_start" && marker.kind != "guaranteed_pickup") continue;
                Vector3 position = MapProductionTools.Vector(marker.position);
                if (!Ground(position, mask, out var hit) || Mathf.Abs(hit.point.y - position.y) > 0.15f)
                {
                    result.markerFailures++;
                    AddError(errors, marker.name + ": not grounded.");
                    continue;
                }
                // Exclude the terrain skin beneath the feet while checking the full standing envelope.
                Vector3 bottom = hit.point + Vector3.up * (cc.radius + 0.12f);
                Vector3 top = hit.point + Vector3.up * (cc.height - cc.radius);
                if (!Physics.CheckCapsule(bottom, top, cc.radius, mask, QueryTriggerInteraction.Ignore)) continue;
                result.markerFailures++;
                AddError(errors, marker.name + ": standing capsule overlaps collision.");
            }
        }

        private static void CheckSeals(MapProductionTools.Manifest data, CharacterController cc, int mask, Result result, List<string> errors)
        {
            foreach (var shape in data.colliders)
            {
                if (!shape.role.StartsWith("seal_", StringComparison.Ordinal)) continue;
                result.sealChecks++;
                var obj = GameObject.Find(shape.name);
                var blocker = obj.GetComponent<Collider>();
                Vector3 center = MapProductionTools.Vector(shape.center);
                Vector3 forward = MapProductionTools.Rotation(shape.rotation) * Vector3.forward;
                Vector3 start = center - forward * 3f;
                if (!Ground(start, mask, out var floor))
                {
                    result.sealFailures++; AddError(errors, shape.name + ": no approach floor."); continue;
                }
                blocker.enabled = true;
                bool blocked = true;
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                foreach (float side in new[] { -1f, 0f, 1f })
                {
                    Vector3 approach = start + right * side * (shape.size[0] * 0.5f + 1f);
                    if (!Ground(approach, mask, out var sideFloor)) continue;
                    Place(cc, sideFloor.point + Vector3.up * 0.12f);
                    float vy = 7.75f;
                    for (int i = 0; i < 60; i++)
                    {
                        vy -= 25f * Dt;
                        var flags = cc.Move(forward * 18f * Dt + Vector3.up * vy * Dt);
                        if ((flags & CollisionFlags.Below) != 0) vy = -2f;
                    }
                    Vector3 delta = cc.transform.position - center;
                    delta.y = 0;
                    if (Vector3.Dot(delta, forward) > 0.1f) blocked = false;
                }
                blocker.enabled = false;
                Place(cc, floor.point + Vector3.up * 0.12f);
                for (int i = 0; i < 90; i++) cc.Move(forward * 6f * Dt + Vector3.down * 5f * Dt);
                Vector3 relative = cc.transform.position - center; relative.y = 0;
                bool open = Vector3.Dot(relative, forward) > 1f;
                if (blocked && open) continue;
                result.sealFailures++;
                AddError(errors, shape.name + $": closedBlocks={blocked}, openPasses={open}.");
            }
        }

        private static void Wander(MapProductionTools.Manifest data, CharacterController cc, int mask, Result result, List<string> errors)
        {
            var random = new System.Random(751958);
            foreach (var pad in data.pads)
            {
                if (!pad.name.StartsWith("Arena", StringComparison.Ordinal) && pad.name != "Bellevue" && pad.name != "Overlook" && pad.name != "Arrival") continue;
                for (int attempt = 0; attempt < 50; attempt++)
                {
                    Vector3 start = new Vector3(pad.cx + ((float)random.NextDouble()*2-1)*(pad.hx-3), pad.height,
                        pad.cz + ((float)random.NextDouble()*2-1)*(pad.hz-3));
                    if (!Ground(start, mask, out var hit)) continue;
                    if (Physics.CheckCapsule(hit.point+Vector3.up*(cc.radius+0.12f), hit.point+Vector3.up*(cc.height-cc.radius),
                        cc.radius, mask, QueryTriggerInteraction.Ignore)) continue;
                    result.wanderRuns++;
                    Place(cc, hit.point + Vector3.up*0.15f);
                    float yaw = (float)random.NextDouble()*360, vy = 0;
                    for (int i = 0; i < 240; i++)
                    {
                        if (i % 45 == 0) yaw += (float)random.NextDouble()*100-50;
                        Vector3 direction = Quaternion.Euler(0,yaw,0)*Vector3.forward;
                        vy = Mathf.Max(-35, vy-25f*Dt);
                        var flags = cc.Move(direction*15f*Dt + Vector3.up*vy*Dt);
                        if ((flags & CollisionFlags.Below) != 0) vy = random.NextDouble()<0.07 ? 7.75f : -2f;
                        if (Ground(cc.transform.position,mask,out var floor) && Feet(cc)>=floor.point.y-0.6f) continue;
                        result.wanderFailures++;
                        AddError(errors, $"Wander {pad.name} fell through or left the terrain at {cc.transform.position:F2}.");
                        break;
                    }
                }
            }
        }
    }
}
