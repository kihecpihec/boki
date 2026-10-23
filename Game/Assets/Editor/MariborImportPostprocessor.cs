using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Boki.Editor
{
    /// <summary>
    /// Some Maribor solids import inside out (front faces and normals point into the volume), so their floors are
    /// culled from above and raycasts cannot hit them. Each connected component with negative signed volume gets
    /// its winding reversed and its normals negated. Flat open pieces (decals) are turned to face up.
    /// </summary>
    public sealed class MariborImportPostprocessor : AssetPostprocessor
    {
        private const string Root = "Assets/Art/Maribor/Export/";
        private const float FlatHeight = 0.05f;

        private void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(Root)) return;
            int flipped = 0, total = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Orient(mf.sharedMesh, ref flipped, ref total);
            }
            Debug.Log($"MARIBOR_ORIENT {assetPath} flippedComponents={flipped}/{total}");
        }

        public static void Orient(Mesh mesh, ref int flipped, ref int total)
        {
            var verts = mesh.vertices;
            var normals = mesh.normals;
            var subs = new int[mesh.subMeshCount][];
            for (int s = 0; s < subs.Length; s++) subs[s] = mesh.GetTriangles(s);

            // Union triangles that share a welded position.
            var parent = new Dictionary<long, long>();
            long Find(long x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            var cell = new Dictionary<(int, int, int), long>();
            long next = 0;
            long Key(Vector3 p)
            {
                var k = (Mathf.RoundToInt(p.x * 1000f), Mathf.RoundToInt(p.y * 1000f), Mathf.RoundToInt(p.z * 1000f));
                if (!cell.TryGetValue(k, out var id)) { id = next++; cell[k] = id; parent[id] = id; }
                return id;
            }
            var triKey = new List<long>();
            foreach (var tris in subs)
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    long a = Key(verts[tris[t]]), b = Key(verts[tris[t + 1]]), c = Key(verts[tris[t + 2]]);
                    long ra = Find(a), rb = Find(b), rc = Find(c);
                    if (ra != rb) parent[rb] = ra;
                    if (Find(ra) != Find(rc)) parent[Find(rc)] = Find(ra);
                    triKey.Add(a);
                }

            var volume = new Dictionary<long, float>();
            var weightedUp = new Dictionary<long, float>();
            var min = new Dictionary<long, Vector3>(); var max = new Dictionary<long, Vector3>();
            int n = 0;
            foreach (var tris in subs)
                for (int t = 0; t + 2 < tris.Length; t += 3, n++)
                {
                    long r = Find(triKey[n]);
                    Vector3 p0 = verts[tris[t]], p1 = verts[tris[t + 1]], p2 = verts[tris[t + 2]];
                    volume.TryGetValue(r, out var v); volume[r] = v + Vector3.Dot(p0, Vector3.Cross(p1, p2)) / 6f;
                    weightedUp.TryGetValue(r, out var u); weightedUp[r] = u + Vector3.Cross(p1 - p0, p2 - p0).y;
                    min[r] = min.TryGetValue(r, out var lo) ? Vector3.Min(Vector3.Min(lo, p0), Vector3.Min(p1, p2)) : Vector3.Min(Vector3.Min(p0, p1), p2);
                    max[r] = max.TryGetValue(r, out var hi) ? Vector3.Max(Vector3.Max(hi, p0), Vector3.Max(p1, p2)) : Vector3.Max(Vector3.Max(p0, p1), p2);
                }

            var flip = new Dictionary<long, bool>();
            foreach (var r in volume.Keys)
            {
                bool flat = max[r].y - min[r].y < FlatHeight;
                flip[r] = flat ? weightedUp[r] < 0f : volume[r] < 0f;
                total++; if (flip[r]) flipped++;
            }

            var flipVertex = new bool[verts.Length];
            n = 0;
            for (int s = 0; s < subs.Length; s++)
            {
                var tris = subs[s];
                for (int t = 0; t + 2 < tris.Length; t += 3, n++)
                {
                    if (!flip[Find(triKey[n])]) continue;
                    (tris[t + 1], tris[t + 2]) = (tris[t + 2], tris[t + 1]);
                    flipVertex[tris[t]] = flipVertex[tris[t + 1]] = flipVertex[tris[t + 2]] = true;
                }
            }
            if (normals.Length == verts.Length)
            {
                for (int i = 0; i < normals.Length; i++) if (flipVertex[i]) normals[i] = -normals[i];
                mesh.normals = normals;
            }
            for (int s = 0; s < subs.Length; s++) mesh.SetTriangles(subs[s], s);
        }
    }
}
