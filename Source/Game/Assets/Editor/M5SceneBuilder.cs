using System;
using Boki.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    public static class M5SceneBuilder
    {
        [MenuItem("Boki/Apply M5 Unlocks and C Paths")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (var path in new[] { RunSession.PohorjeScene, RunSession.MariborScene, RunSession.FeriScene })
                {
                    var scene = EditorSceneManager.OpenScene(path);
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        if (root.name == "Reserved C route") Object.DestroyImmediate(root);
                    }
                    if (GameObject.Find("M5 C Path") == null)
                    {
                        var parent = new GameObject("M5 C Path");
                        var wall = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/M2/Wall.mat");
                        bool forest = path == RunSession.PohorjeScene;
                        // Existing floor, dividers, entrance gate and rear seal define the lane.
                        // Raised roof and primitive side columns give C a distinct tunnel identity.
                        Add(parent, "C tunnel roof", new Vector3(12, 6, 31), new Vector3(11, 0.4f, 54), wall);
                        for (int i = 0; i < 4; i++)
                        {
                            Add(parent, "C side column", new Vector3(i % 2 == 0 ? 8 : 16, 2.5f, 12 + i * 12),
                                new Vector3(1, 5, 1), wall);
                            if (forest) Add(parent, "Rave tunnel beam", new Vector3(12, 5.5f, 12 + i * 12), new Vector3(10, 0.3f, 0.4f), wall);
                        }
                        var sign = new GameObject("C path marker");
                        sign.transform.SetParent(parent.transform);
                        sign.transform.position = new Vector3(12, 4, 55);
                        sign.transform.rotation = Quaternion.Euler(0, 180, 0);
                        var text = sign.AddComponent<TextMesh>();
                        text.text = "SHARED EXIT →"; text.fontSize = 40; text.characterSize = 0.07f;
                        text.anchor = TextAnchor.MiddleCenter;
                    }
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                if (setup.Length > 0 && !string.IsNullOrEmpty(setup[0].path)) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            Debug.Log("M5_SCENES_APPLIED");
        }
        private static void Add(GameObject parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name; cube.transform.SetParent(parent.transform);
            cube.transform.position = position; cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
