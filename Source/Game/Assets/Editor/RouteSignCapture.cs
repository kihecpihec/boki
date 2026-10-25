using System;
using System.IO;
using Boki.Routes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    // Renders each V1 fork from the rider's approach so sign polish and fog fade can be reviewed without Play Mode.
    // Scenes are opened read-only in spirit: nothing is saved and the previous scene setup is restored.
    public static class RouteSignCapture
    {
        private static readonly string[] Scenes = { "Assets/Scenes/V1_Pohorje.unity", "Assets/Scenes/V1_Maribor.unity", "Assets/Scenes/V1_FERI.unity" };
        private static readonly float[] Distances = { 9f, 22f, 38f, 60f };

        [MenuItem("Boki/Version 1/Capture Route Signs")]
        public static void Capture()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            string folder = Path.Combine(Path.GetTempPath(), "boki-route-signs");
            Directory.CreateDirectory(folder);
            try
            {
                foreach (string path in Scenes)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    CaptureScene(scene, folder);
                }
            }
            finally
            {
                if (previous.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            Debug.Log("Route sign captures written to " + folder);
        }

        private static void CaptureScene(Scene scene, string folder)
        {
            RouteGate[] gates = Object.FindObjectsByType<RouteGate>(FindObjectsInactive.Include);
            if (gates.Length == 0) return;
            Vector3 center = Vector3.zero, forward = Vector3.zero;
            foreach (RouteGate gate in gates)
            {
                var sign = (TextMesh)new SerializedObject(gate).FindProperty("sign").objectReferenceValue;
                var choice = (RouteChoice)new SerializedObject(gate).FindProperty("choice").intValue;
                var manager = (RouteManager)new SerializedObject(gate).FindProperty("manager").objectReferenceValue;
                // First-run look: A and B open, C still to discover; Maribor shows a sealed gate and FERI a chosen one.
                GateState state = choice == RouteChoice.C ? GateState.PersistentLock : GateState.Available;
                if (scene.name == "V1_Maribor" && choice == RouteChoice.B) state = GateState.RouteLock;
                if (scene.name == "V1_FERI" && choice == RouteChoice.A) state = GateState.Selected;
                gate.PreviewSign(manager != null ? manager.Level : RouteLevel.Forest, state);
                center += sign.transform.position;
                forward += sign.transform.forward;
            }
            center /= gates.Length;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include)) camera.enabled = false;

            var rig = new GameObject("Route Sign Capture");
            var view = rig.AddComponent<Camera>();
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = Color.black;
            view.fieldOfView = 60f;
            view.farClipPlane = 400f;
            var lampObject = new GameObject("Capture Headlight");
            var lamp = lampObject.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 28f;
            lamp.intensity = 60f;
            lamp.shadows = LightShadows.None;
            var texture = new RenderTexture(1280, 720, 24);
            view.targetTexture = texture;
            foreach (RouteGate gate in gates)
            {
                Transform sign = ((TextMesh)new SerializedObject(gate).FindProperty("sign").objectReferenceValue).transform;
                Vector3 approach = Vector3.ProjectOnPlane(sign.forward, Vector3.up).normalized;
                Shoot(view, lampObject, lamp, sign.position, approach, 9f, Path.Combine(folder, $"{scene.name}_{gate.name}.png"));
            }
            foreach (float distance in Distances)
                Shoot(view, lampObject, lamp, center, forward, distance, Path.Combine(folder, $"{scene.name}_{distance:00}m.png"));
            view.targetTexture = null;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(rig);
            Object.DestroyImmediate(lampObject);
        }

        private static void Shoot(Camera view, GameObject lampObject, Light lamp, Vector3 center, Vector3 forward, float distance, string file)
        {
            Vector3 ground = center - forward * distance;
            // Cast from sign height so indoor forks do not land the camera on a roof.
            if (Physics.Raycast(new Vector3(ground.x, center.y + 0.5f, ground.z), Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
                ground = hit.point;
            else ground.y = center.y - 3.2f;
            // Third-person framing: camera behind and above the rider, headlight on the scooter ahead of it.
            Vector3 eye = ground + Vector3.up * 2.6f - forward * 4.5f;
            // Pull the camera in front of any wall between it and the fork, as the follow camera would.
            if (Physics.Linecast(center + Vector3.down * 1.2f, eye, out RaycastHit wall, ~0, QueryTriggerInteraction.Ignore))
                eye = wall.point + (center - eye).normalized * 0.4f;
            view.transform.position = eye;
            view.transform.rotation = Quaternion.LookRotation(center - view.transform.position + Vector3.down * 1.2f);
            lampObject.transform.position = ground + Vector3.up * 0.75f;
            foreach (RouteSign sign in Object.FindObjectsByType<RouteSign>(FindObjectsInactive.Include))
                sign.Reveal(lampObject.transform.position, lamp.range);
            view.Render();
            Save(view.targetTexture, file);
        }

        private static void Save(RenderTexture source, string path)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = source;
            var image = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            RenderTexture.active = previous;
        }
    }
}
