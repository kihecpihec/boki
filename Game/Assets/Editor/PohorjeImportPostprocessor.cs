using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Boki.Editor
{
    /// <summary>
    /// Re-projects Pohorje surface UVs in world space (4 m per tile) so walls, ramps and rock faces are not stretched
    /// by the Blender planar UVs, and keeps tiling continuous across neighbouring meshes.
    /// </summary>
    public sealed class PohorjeImportPostprocessor : AssetPostprocessor
    {
        private const string Root = "Assets/Art/Pohorje/";
        private const float TileMeters = 4f;
        private static readonly HashSet<string> TiledSurfaces = new HashSet<string> { "Concrete", "Path", "Rock", "Soil" };
        private static readonly Regex Suffix = new Regex(@"\.\d+$");

        public static string SurfaceKey(string materialName)
        {
            string key = Suffix.Replace(materialName ?? "", "");
            return key.StartsWith("PSX_PL_") ? key.Substring(7) : key;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root + "Textures/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 8;
            importer.npotScale = TextureImporterNPOTScale.None;
        }

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root + "Export/")) return;
            ((ModelImporter)assetImporter).isReadable = true; // UVs are rebuilt in OnPostprocessModel
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(Root + "Export/")) return;
            bool world = System.IO.Path.GetFileName(assetPath).StartsWith("PL_");
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mf.sharedMesh == null) continue;
                Reproject(mf.sharedMesh, mr.sharedMaterials, world ? mf.transform.localToWorldMatrix : Matrix4x4.identity,
                    root.transform.worldToLocalMatrix);
            }
        }

        private static void Reproject(Mesh mesh, Material[] materials, Matrix4x4 toWorld, Matrix4x4 toRoot)
        {
            bool any = false;
            for (int s = 0; s < mesh.subMeshCount && s < materials.Length; s++)
                if (materials[s] != null && TiledSurfaces.Contains(SurfaceKey(materials[s].name))) any = true;
            if (!any) return;

            var srcV = mesh.vertices; var srcN = mesh.normals; var srcUv = mesh.uv; var srcC = mesh.colors32;
            var verts = new List<Vector3>(); var normals = new List<Vector3>(); var uvs = new List<Vector2>(); var colors = new List<Color32>();
            var tris = new List<int[]>();
            var m = toRoot * toWorld; // keep tiling continuous in the shared Blender map space
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var idx = mesh.GetTriangles(s);
                bool tile = s < materials.Length && materials[s] != null && TiledSurfaces.Contains(SurfaceKey(materials[s].name));
                var list = new int[idx.Length];
                for (int t = 0; t + 2 < idx.Length; t += 3)
                {
                    Vector3 a = m.MultiplyPoint3x4(srcV[idx[t]]), b = m.MultiplyPoint3x4(srcV[idx[t + 1]]), c = m.MultiplyPoint3x4(srcV[idx[t + 2]]);
                    Vector3 n = Vector3.Cross(b - a, c - a);
                    Vector3 an = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
                    for (int k = 0; k < 3; k++)
                    {
                        int i = idx[t + k]; Vector3 p = k == 0 ? a : k == 1 ? b : c;
                        Vector2 uv;
                        if (!tile) uv = srcUv.Length > i ? srcUv[i] : Vector2.zero;
                        else if (an.y >= an.x && an.y >= an.z) uv = new Vector2(p.x, p.z) / TileMeters;
                        else if (an.x >= an.z) uv = new Vector2(p.z, p.y) / TileMeters;
                        else uv = new Vector2(p.x, p.y) / TileMeters;
                        list[t + k] = verts.Count;
                        verts.Add(srcV[i]); normals.Add(srcN.Length > i ? srcN[i] : Vector3.up); uvs.Add(uv);
                        if (srcC.Length > i) colors.Add(srcC[i]);
                    }
                }
                tris.Add(list);
            }
            mesh.Clear();
            mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts); mesh.SetNormals(normals); mesh.SetUVs(0, uvs);
            if (colors.Count == verts.Count) mesh.SetColors(colors);
            mesh.subMeshCount = tris.Count;
            for (int s = 0; s < tris.Count; s++) mesh.SetTriangles(tris[s], s);
            mesh.RecalculateBounds();
        }
    }
}
