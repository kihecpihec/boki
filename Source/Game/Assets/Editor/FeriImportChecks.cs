using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Boki.Editor
{
    /// <summary>
    /// Checks the imported FERI chunks against the export: scale and axes, winding, material and texture ownership,
    /// guide and marker positions, and coplanar same-facing triangles between different render meshes (z-fighting).
    /// Geometry only; it says nothing about collision, camera or gameplay.
    /// </summary>
    public static class FeriImportChecks
    {
        private const string Art = "Assets/Art/FERI/";
        private const float PositionTolerance = 0.01f;
        private const float PlaneTolerance = 0.002f;
        private const float Interior = 0.001f;
        private static readonly Vector2[] TriA = new Vector2[3];
        private static readonly Vector2[] TriB = new Vector2[3];

        [System.Serializable] private class Guide { public string name; public float[] position; public float[] size; public float[] rotation; }
        [System.Serializable] private class GuideList { public Guide[] items; }
        [System.Serializable] private class Marker { public string name; public float[] position; }
        [System.Serializable] private class MarkerList { public Marker[] markers; }

        private sealed class Part
        {
            public string File, Name;
            public Material[] Materials;
            public Mesh Mesh;
            public Vector3[] Vertices;
            public int[] Triangles;
            public Bounds Bounds;
            public Vector3[] Normals;
            public float[] Planes;
        }

        // The importer maps Blender (x, y, z) to Unity (-x, z, -y).
        private static Vector3 Blender(Vector3 u) => new Vector3(-u.x, -u.z, u.y);

        private static string Format(Vector3 v) => $"({v.x:F2},{v.y:F2},{v.z:F2})";

        [MenuItem("Boki/FERI/Run Import Checks")]
        public static void Run()
        {
            int errors = 0;
            void Fail(string message) { errors++; Debug.LogError("FERI_FAIL " + message); }

            var parts = new List<Part>();
            var guides = new Dictionary<string, Bounds>();
            var markers = new Dictionary<string, Vector3>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Art + "Export" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);
                bool isGuide = file.Contains("CollisionGuides"), isMarker = file.Contains("Markers");
                var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                var filePartsStart = parts.Count;
                int tris = 0, insideOut = 0, components = 0;
                var fileBounds = new Bounds();
                bool first = true;
                var materials = new SortedSet<string>();

                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (filter.sharedMesh == null || renderer == null) continue;
                    var part = new Part { File = file, Name = filter.name, Materials = renderer.sharedMaterials, Mesh = filter.sharedMesh };
                    var local = filter.sharedMesh.vertices;
                    var toRoot = filter.transform.localToWorldMatrix;
                    part.Vertices = new Vector3[local.Length];
                    for (int i = 0; i < local.Length; i++) part.Vertices[i] = toRoot.MultiplyPoint3x4(local[i]);
                    part.Triangles = filter.sharedMesh.triangles;
                    part.Bounds = new Bounds(part.Vertices[0], Vector3.zero);
                    foreach (var v in part.Vertices) part.Bounds.Encapsulate(v);
                    if (first) { fileBounds = part.Bounds; first = false; } else fileBounds.Encapsulate(part.Bounds);
                    tris += part.Triangles.Length / 3;
                    parts.Add(part);

                    if (isGuide)
                    {
                        var blenderBounds = new Bounds(Blender(part.Bounds.center), new Vector3(part.Bounds.size.x, part.Bounds.size.z, part.Bounds.size.y));
                        guides[filter.name] = blenderBounds;
                        continue;
                    }
                    foreach (var material in part.Materials)
                    {
                        materials.Add(material == null ? "<null>" : material.name);
                        string materialPath = material == null ? "" : AssetDatabase.GetAssetPath(material);
                        var texture = material == null ? null : material.GetTexture("_BaseMap");
                        string texturePath = texture == null ? "" : AssetDatabase.GetAssetPath(texture);
                        if (!materialPath.StartsWith(Art + "Materials/")) Fail($"{file}/{filter.name} uses material outside Art/FERI: '{materialPath}'");
                        else if (!texturePath.StartsWith(Art + "Textures/")) Fail($"{material.name} base map is '{texturePath}', not an Art/FERI texture");
                    }
                    Orientation(part, ref insideOut, ref components);
                }

                if (isMarker)
                    foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                        if (t != instance.transform) markers[t.name] = Blender(t.position);

                Debug.Log($"FERI_FILE {file} objects={parts.Count - filePartsStart} tris={tris} " +
                          $"bounds={Format(Blender(fileBounds.min))}..{Format(Blender(fileBounds.max))} " +
                          $"materials=[{string.Join(",", materials)}] insideOut={insideOut}/{components}");
                if (insideOut > 0) Fail($"{file}: {insideOut}/{components} solids import inside out");
                Object.DestroyImmediate(instance);
            }

            CheckGuides(guides, Fail);
            CheckMarkers(markers, Fail);
            CheckOverlaps(parts);
            Debug.Log($"FERI_DONE errors={errors}");
            if (Application.isBatchMode) EditorApplication.Exit(errors == 0 ? 0 : 1);
        }

        // Every FERI surface has thickness, so a connected solid with negative signed volume is wound inside out.
        private static void Orientation(Part part, ref int negative, ref int total)
        {
            var ids = new Dictionary<(int, int, int), int>();
            var parent = new List<int>();
            int Id(Vector3 p)
            {
                var key = (Mathf.RoundToInt(p.x * 1000f), Mathf.RoundToInt(p.y * 1000f), Mathf.RoundToInt(p.z * 1000f));
                if (!ids.TryGetValue(key, out int id)) { id = parent.Count; ids[key] = id; parent.Add(id); }
                return id;
            }
            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }

            int count = part.Triangles.Length / 3;
            var anchor = new int[count];
            for (int t = 0; t < count; t++)
            {
                int a = Id(part.Vertices[part.Triangles[t * 3]]), b = Id(part.Vertices[part.Triangles[t * 3 + 1]]), c = Id(part.Vertices[part.Triangles[t * 3 + 2]]);
                parent[Find(b)] = Find(a);
                parent[Find(c)] = Find(a);
                anchor[t] = a;
            }

            var volume = new Dictionary<int, float>();
            var triangles = new Dictionary<int, int>();
            var bounds = new Dictionary<int, Bounds>();
            for (int t = 0; t < count; t++)
            {
                int root = Find(anchor[t]);
                Vector3 p0 = part.Vertices[part.Triangles[t * 3]], p1 = part.Vertices[part.Triangles[t * 3 + 1]], p2 = part.Vertices[part.Triangles[t * 3 + 2]];
                volume.TryGetValue(root, out float v); volume[root] = v + Vector3.Dot(p0, Vector3.Cross(p1, p2)) / 6f;
                triangles.TryGetValue(root, out int n); triangles[root] = n + 1;
                if (!bounds.TryGetValue(root, out var box)) box = new Bounds(p0, Vector3.zero);
                box.Encapsulate(p0); box.Encapsulate(p1); box.Encapsulate(p2);
                bounds[root] = box;
            }
            foreach (var entry in volume)
            {
                total++;
                if (entry.Value >= 0f) continue;
                negative++;
                var box = bounds[entry.Key];
                Debug.Log($"FERI_INSIDEOUT {part.File}/{part.Name} tris={triangles[entry.Key]} volume={entry.Value:F5} " +
                          $"size={Format(new Vector3(box.size.x, box.size.z, box.size.y))} at={Format(Blender(box.center))}");
            }
        }

        private static void CheckGuides(Dictionary<string, Bounds> imported, System.Action<string> fail)
        {
            var list = JsonUtility.FromJson<GuideList>("{\"items\":" + File.ReadAllText(Art + "collision-guides.json") + "}").items;
            int missing = 0, moved = 0, resized = 0;
            foreach (var guide in list)
            {
                if (!imported.TryGetValue(guide.name, out var bounds)) { missing++; continue; }
                var center = new Vector3(guide.position[0], guide.position[1], guide.position[2]);
                if ((bounds.center - center).magnitude > PositionTolerance) moved++;
                bool rotated = Mathf.Abs(guide.rotation[0]) + Mathf.Abs(guide.rotation[1]) + Mathf.Abs(guide.rotation[2]) > 1e-4f;
                var size = new Vector3(guide.size[0], guide.size[1], guide.size[2]);
                if (!rotated && (bounds.size - size).magnitude > PositionTolerance) resized++;
            }
            Debug.Log($"FERI_GUIDES json={list.Length} imported={imported.Count} missing={missing} moved={moved} resized={resized}");
            if (missing + moved + resized > 0 || list.Length != imported.Count) fail("collision guides in the FBX do not match collision-guides.json");
        }

        private static void CheckMarkers(Dictionary<string, Vector3> imported, System.Action<string> fail)
        {
            var list = JsonUtility.FromJson<MarkerList>(File.ReadAllText(Art + "markers.json")).markers;
            int missing = 0, moved = 0;
            foreach (var marker in list)
            {
                if (!imported.TryGetValue(marker.name, out var position)) { missing++; continue; }
                var expected = new Vector3(marker.position[0], marker.position[1], marker.position[2]);
                if ((position - expected).magnitude > PositionTolerance) moved++;
            }
            Debug.Log($"FERI_MARKERS json={list.Length} imported={imported.Count} missing={missing} moved={moved}");
            if (missing + moved > 0 || list.Length != imported.Count) fail("markers in the FBX do not match markers.json");
        }

        // ------------------------------------------------------------------ overlap

        private static void CheckOverlaps(List<Part> all)
        {
            var parts = all.FindAll(p => !p.File.Contains("CollisionGuides") && !p.File.Contains("Markers"));
            foreach (var p in parts) Prepare(p);

            // Known and intentional (README: block construction retains internal adjoining faces); counted, not listed.
            var intra = new Dictionary<string, int>();
            int cross = 0;
            for (int i = 0; i < parts.Count; i++)
            {
                for (int j = i + 1; j < parts.Count; j++)
                {
                    var a = parts[i]; var b = parts[j];
                    var grown = a.Bounds; grown.Expand(PlaneTolerance);
                    if (!grown.Intersects(b.Bounds)) continue;
                    int hits = CountCoplanar(a, b, out var region, out var facing);
                    if (hits == 0) continue;
                    if (a.File == b.File) { intra.TryGetValue(a.File, out var n); intra[a.File] = n + hits; continue; }
                    cross++;
                    Debug.Log($"FERI_ZFIGHT {a.File}/{a.Name} x {b.File}/{b.Name} trianglePairs={hits} facing={Format(Blender(facing))} " +
                              $"firstRegion={Format(Blender(region.min))}..{Format(Blender(region.max))}");
                }
            }
            foreach (var entry in intra) Debug.Log($"FERI_ZFIGHT_INTRA {entry.Key} trianglePairs={entry.Value}");
            Debug.Log($"FERI_ZFIGHT_CROSS meshPairs={cross}");

            var files = new Dictionary<string, Bounds>();
            foreach (var p in parts)
            {
                if (files.TryGetValue(p.File, out var b)) { b.Encapsulate(p.Bounds); files[p.File] = b; } else files[p.File] = p.Bounds;
            }
            var names = new List<string>(files.Keys); names.Sort();
            for (int i = 0; i < names.Count; i++)
                for (int j = i + 1; j < names.Count; j++)
                {
                    var a = files[names[i]]; var b = files[names[j]];
                    if (!a.Intersects(b)) continue;
                    var min = Vector3.Max(a.min, b.min); var max = Vector3.Min(a.max, b.max);
                    Debug.Log($"FERI_BOUNDS_OVERLAP {names[i]} x {names[j]} overlap={Format(Blender(min))}..{Format(Blender(max))}");
                }
        }

        private static void Prepare(Part p)
        {
            int count = p.Triangles.Length / 3;
            p.Normals = new Vector3[count];
            p.Planes = new float[count];
            for (int t = 0; t < count; t++)
            {
                Vector3 v0 = p.Vertices[p.Triangles[t * 3]], v1 = p.Vertices[p.Triangles[t * 3 + 1]], v2 = p.Vertices[p.Triangles[t * 3 + 2]];
                var n = Vector3.Cross(v1 - v0, v2 - v0);
                p.Normals[t] = n.sqrMagnitude < 1e-12f ? Vector3.zero : n.normalized;
                p.Planes[t] = Vector3.Dot(p.Normals[t], v0);
            }
        }

        private static int CountCoplanar(Part a, Part b, out Bounds firstRegion, out Vector3 firstFacing)
        {
            int hits = 0;
            firstRegion = new Bounds();
            firstFacing = Vector3.zero;
            for (int ta = 0; ta < a.Normals.Length; ta++)
            {
                var na = a.Normals[ta];
                if (na == Vector3.zero) continue;
                for (int tb = 0; tb < b.Normals.Length; tb++)
                {
                    if (Vector3.Dot(na, b.Normals[tb]) < 0.9999f) continue; // same facing only
                    if (Mathf.Abs(a.Planes[ta] - b.Planes[tb]) > PlaneTolerance) continue;
                    if (!Overlap(a, ta, b, tb, na)) continue;
                    if (hits++ != 0) continue;
                    var boxA = TriangleBounds(a, ta); var boxB = TriangleBounds(b, tb);
                    firstRegion.SetMinMax(Vector3.Max(boxA.min, boxB.min), Vector3.Min(boxA.max, boxB.max));
                    firstFacing = na;
                }
            }
            return hits;
        }

        private static Bounds TriangleBounds(Part p, int t)
        {
            var box = new Bounds(p.Vertices[p.Triangles[t * 3]], Vector3.zero);
            box.Encapsulate(p.Vertices[p.Triangles[t * 3 + 1]]);
            box.Encapsulate(p.Vertices[p.Triangles[t * 3 + 2]]);
            return box;
        }

        private static bool Overlap(Part a, int ta, Part b, int tb, Vector3 normal)
        {
            // Drop the dominant normal axis to compare the triangles as 2D polygons in their shared plane.
            int drop = Mathf.Abs(normal.x) > Mathf.Abs(normal.y) ? (Mathf.Abs(normal.x) > Mathf.Abs(normal.z) ? 0 : 2) : (Mathf.Abs(normal.y) > Mathf.Abs(normal.z) ? 1 : 2);
            for (int k = 0; k < 3; k++)
            {
                TriA[k] = Project(a.Vertices[a.Triangles[ta * 3 + k]], drop);
                TriB[k] = Project(b.Vertices[b.Triangles[tb * 3 + k]], drop);
            }
            return Separates(TriA, TriB) && Separates(TriB, TriA);
        }

        private static Vector2 Project(Vector3 v, int drop) => drop == 0 ? new Vector2(v.y, v.z) : drop == 1 ? new Vector2(v.x, v.z) : new Vector2(v.x, v.y);

        // True when no edge normal of 'edges' separates the two triangles, i.e. their interiors overlap on that side.
        private static bool Separates(Vector2[] edges, Vector2[] other)
        {
            for (int i = 0; i < 3; i++)
            {
                var edge = edges[(i + 1) % 3] - edges[i];
                var axis = new Vector2(-edge.y, edge.x);
                if (axis.sqrMagnitude < 1e-12f) continue;
                axis.Normalize();
                Range(edges, axis, out var minA, out var maxA);
                Range(other, axis, out var minB, out var maxB);
                if (maxA <= minB + Interior || maxB <= minA + Interior) return false;
            }
            return true;
        }

        private static void Range(Vector2[] tri, Vector2 axis, out float min, out float max)
        {
            min = max = Vector2.Dot(tri[0], axis);
            for (int i = 1; i < 3; i++) { float d = Vector2.Dot(tri[i], axis); if (d < min) min = d; if (d > max) max = d; }
        }
    }
}
