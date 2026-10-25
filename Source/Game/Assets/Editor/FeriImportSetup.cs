using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Boki.Editor
{
    /// <summary>
    /// Applies the import settings from output/world-modeling-v1/maps/feri/README.md to Assets/Art/FERI and builds the
    /// opaque URP materials its FBX files reference. Textures are bound by explicit path because Maribor and Pohorje
    /// ship same-named textures (Concrete, Leaf, Wood). Idempotent; touches nothing outside Assets/Art/FERI.
    /// </summary>
    public static class FeriImportSetup
    {
        private const string Art = "Assets/Art/FERI/";
        private const string Prefix = "M_FERI_";
        private static readonly Regex Suffix = new Regex(@"\.\d+$");

        // README step 1: Tube and Danger emission is recreated here because Blender emission does not survive FBX.
        private static readonly Dictionary<string, Color> Emission = new Dictionary<string, Color>
        {
            { "Tube", new Color(0.85f, 1f, 0.8f) * 1.5f },
            { "Danger", new Color(1f, 0.35f, 0.25f) * 1.2f },
        };

        [MenuItem("Boki/FERI/Configure Import")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("FERI_FAIL exit Play Mode first"); return; }
            AssetDatabase.Refresh();
            Directory.CreateDirectory(Art + "Materials");

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Art + "Textures" }))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 0;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            var models = new List<(ModelImporter importer, HashSet<string> sources)>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Art + "Export" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.globalScale = 1f;
                importer.useFileScale = true; // Convert Units
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.addCollider = false; // colliders come from the COL_ guides
                importer.isReadable = false;
                importer.meshCompression = ModelImporterMeshCompression.Off;
                importer.importNormals = ModelImporterNormals.Import;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();

                var sources = new HashSet<string>();
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is Material embedded) sources.Add(embedded.name);
                foreach (var entry in importer.GetExternalObjectMap())
                    if (entry.Key.type == typeof(Material)) sources.Add(entry.Key.name); // already remapped on a previous run
                models.Add((importer, sources));
            }

            var built = new Dictionary<string, Material>();
            foreach (var (importer, sources) in models)
            {
                foreach (string source in sources)
                {
                    string key = Suffix.Replace(source, "");
                    if (!key.StartsWith(Prefix))
                    {
                        // The collision guides carry no material, so Unity names its placeholder "No Name". Leave it unmapped.
                        importer.RemoveRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source));
                        continue;
                    }
                    if (!built.TryGetValue(key, out var material)) built[key] = material = BuildMaterial(key);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source), material);
                }
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            Debug.Log("FERI_MATERIALS " + string.Join(", ", built.Keys));
        }

        private static Material BuildMaterial(string key)
        {
            string path = Art + "Materials/" + key + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new System.InvalidOperationException("URP Lit shader not found");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            string surface = key.Substring(Prefix.Length);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Textures/" + surface + ".png");
            if (texture == null) Debug.LogWarning($"FERI_WARN no texture Art/FERI/Textures/{surface}.png for {key}");
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_SpecularHighlights", 0f); material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.SetFloat("_EnvironmentReflections", 0f); material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.enableInstancing = true;
            if (Emission.TryGetValue(surface, out var emission))
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                material.SetColor("_EmissionColor", emission);
                material.SetTexture("_EmissionMap", texture);
            }
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
