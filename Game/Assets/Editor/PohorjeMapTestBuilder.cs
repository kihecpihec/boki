using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Boki.Editor
{
    /// <summary>Builds Pohorje_Map_Test: the Blender action-layout map around the existing Boki test player.</summary>
    public static class PohorjeMapTestBuilder
    {
        private const string Source = "Assets/Scenes/Player_Model_Test.unity";
        private const string Target = "Assets/Scenes/Pohorje_Map_Test.unity";
        private const string Export = "Assets/Art/Pohorje/Export/";
        private const string Textures = "Assets/Art/Pohorje/Textures/";
        private const string Materials = "Assets/Art/Pohorje/Materials/";

        [MenuItem("Boki/Pohorje/Build Map Test Scene")]
        public static void Build()
        {
            AssetDatabase.ImportAsset("Assets/Art/Pohorje", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            var scene = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var player = default(GameObject);
            foreach (var r in roots)
            {
                Debug.Log("POHORJE_ROOT " + r.name);
                if (r.GetComponentInChildren<Boki.Player.ScooterController>(true) != null) player = r;
                else if (r.GetComponent<Camera>() == null && r.GetComponent<Light>() == null
                         && r.GetComponentInChildren<Canvas>(true) == null && r.GetComponentInChildren<Camera>(true) == null)
                    Object.DestroyImmediate(r);
            }
            if (player == null) { Debug.LogError("POHORJE_FAIL no player"); return; }

            Directory.CreateDirectory(Materials);
            var map = new GameObject("Pohorje_Map");
            foreach (var name in new[] { "PL_Shared_Bellevue", "PL_Shared_Fork", "PL_Shared_Overlook", "PL_Route_A",
                         "PL_Route_B", "PL_Route_C", "PL_C_Removable_Roofs", "PL_Terrain_Chunks", "PL_Forest_Chunks" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Export + name + ".fbx");
                if (prefab == null) { Debug.LogError("POHORJE_FAIL missing " + name); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.SetParent(map.transform, false);
                ApplyMaterials(go);
                // Walkable surfaces need mesh collision; forest chunks are scenic only.
                if (name != "PL_Forest_Chunks")
                    foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                        mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                go.isStatic = true;
            }

            // The Blender export leaves a 0.5 m hole (2.5 m deep) between the Bellevue slab and the fork slab.
            var filler = GameObject.CreatePrimitive(PrimitiveType.Cube);
            filler.name = "PL_BellevueForkGapFill";
            filler.transform.SetParent(map.transform, false);
            filler.transform.position = new Vector3(0f, 7.85f, 10.75f);
            filler.transform.localScale = new Vector3(20.5f, 0.3f, 0.6f);
            filler.GetComponent<Renderer>().sharedMaterial = GetMaterial("Path");
            filler.isStatic = true;

            var markers = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Export + "Pohorje_GameplayMarkers.fbx"));
            markers.transform.SetParent(map.transform, false);
            Transform start = null;
            foreach (var t in markers.GetComponentsInChildren<Transform>(true))
                if (t.name == "PlayerStart") start = t;
            foreach (var r in markers.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            Debug.Log("POHORJE_START " + (start != null ? start.position.ToString("F2") : "none"));

            var cc = player.GetComponentInChildren<CharacterController>(true);
            // FBX import rotates the Blender map 180 degrees about Y: Blender PlayerStart (0,-14,8) is Unity (0,8,14).
            var facing = Quaternion.Euler(0, 180, 0);
            var pos = start != null ? start.position : new Vector3(0, 8, 14);
            pos += Vector3.up * 1.0f;
            if (cc != null) cc.enabled = false;
            player.transform.SetPositionAndRotation(pos, facing);
            if (cc != null) cc.enabled = true;
            var cam = Camera.main;
            if (cam != null) { cam.nearClipPlane = 0.3f; cam.farClipPlane = 350f; cam.transform.SetPositionAndRotation(pos + new Vector3(0, 3, 6), Quaternion.Euler(15, 180, 0)); }
            Physics.SyncTransforms();

            EditorSceneManager.SaveScene(scene, Target);
            Debug.Log("POHORJE_DONE");
        }

        [MenuItem("Boki/Pohorje/Play Map Test Scene")]
        public static void Play()
        {
            EditorSceneManager.OpenScene(Target, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        // Blender material table (linear base colour, optional emission strength); textured surfaces use the image only.
        private static readonly Dictionary<string, (Color color, float emission)> Plain = new Dictionary<string, (Color, float)>
        {
            ["PitConcrete"] = (new Color(0.06f, 0.08f, 0.09f), 0f), ["Action_PitConcrete"] = (new Color(0.06f, 0.08f, 0.09f), 0f),
            ["BlackGlass"] = (new Color(0.05f, 0.06f, 0.07f), 0f), ["Metal"] = (new Color(0.10f, 0.12f, 0.12f), 0f),
            ["ServiceRed"] = (new Color(0.24f, 0.09f, 0.06f), 0f), ["ChargeEmission"] = (new Color(0.18f, 0.48f, 0.37f), 1f),
            ["RaveEmission"] = (new Color(0.37f, 0.08f, 0.64f), 2f), ["WarmEmission"] = (new Color(0.80f, 0.39f, 0.13f), 2f),
        };
        private static readonly Dictionary<string, string> TextureFor = new Dictionary<string, string>
        {
            ["Bark"] = "PSX_Bark", ["Concrete"] = "Concrete", ["Foliage"] = "PSX_Foliage",
            ["Path"] = "PSX_Path", ["Rock"] = "PSX_Rock", ["Soil"] = "PSX_Soil",
        };

        private static void ApplyMaterials(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = GetMaterial(PohorjeImportPostprocessor.SurfaceKey(mats[i] != null ? mats[i].name : "Default"));
                r.sharedMaterials = mats;
            }
        }

        private static Material GetMaterial(string key)
        {
            string path = Materials + "Pohorje_" + key + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Pohorje_" + key };
            mat.SetFloat("_Smoothness", 0f); mat.SetFloat("_Metallic", 0f); mat.SetFloat("_SpecularHighlights", 0f);
            if (TextureFor.TryGetValue(key, out var texName))
            {
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + texName + ".png"));
                mat.SetColor("_BaseColor", Color.white);
            }
            else
            {
                var p = Plain.TryGetValue(key, out var v) ? v : (new Color(0.5f, 0.5f, 0.5f), 0f);
                mat.SetColor("_BaseColor", p.Item1.gamma);
                if (p.Item2 > 0f)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    mat.SetColor("_EmissionColor", p.Item1 * p.Item2);
                }
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
