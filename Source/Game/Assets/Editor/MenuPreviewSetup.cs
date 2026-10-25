using System;
using System.IO;
using Boki.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Boki.Editor
{
    public static class MenuPreviewSetup
    {
        private const string Folder = "Assets/UI/Menus";
        private const string ScenePath = "Assets/Scenes/Menu_Preview.unity";
        private static readonly Color Paper = new Color(0.91f, 0.89f, 0.86f);

        [MenuItem("Boki/Menus/Create Isolated Menu Preview")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.Log("Menu preview already exists; open " + ScenePath + ". Existing edits were preserved.");
                return;
            }
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Directory.CreateDirectory(Folder);
                string backgroundPath = Folder + "/Background.png";
                if (!File.Exists(backgroundPath))
                    File.Copy(Path.GetFullPath("../output/start-screen-mock/ref/background.png"), backgroundPath);
                AssetDatabase.Refresh();
                var importer = (TextureImporter)AssetImporter.GetAtPath(backgroundPath);
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
                Texture2D background = AssetDatabase.LoadAssetAtPath<Texture2D>(backgroundPath);
                var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(Folder + "/MenuInput.asset");
                if (input == null)
                {
                    input = ScriptableObject.CreateInstance<InputActionAsset>();
                    var map = input.AddActionMap("Menus");
                    var navigate = map.AddAction("Navigate", InputActionType.Button);
                    navigate.AddCompositeBinding("1DAxis").With("Positive", "<Keyboard>/w").With("Negative", "<Keyboard>/s");
                    navigate.AddCompositeBinding("1DAxis").With("Positive", "<Keyboard>/upArrow").With("Negative", "<Keyboard>/downArrow");
                    map.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter");
                    map.AddAction("Back", InputActionType.Button, "<Keyboard>/escape");
                    AssetDatabase.CreateAsset(input, Folder + "/MenuInput.asset");
                    foreach (InputAction action in map.actions)
                    {
                        var reference = InputActionReference.Create(action);
                        reference.name = action.name;
                        AssetDatabase.AddObjectToAsset(reference, input);
                    }
                    AssetDatabase.SaveAssets();
                }
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var canvasObject = new GameObject("Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1600, 900);
                scaler.matchWidthOrHeight = 0.5f;
                var bg = Element<RawImage>(canvas.transform, "Mockup Background", 0, 0, 1600, 900);
                bg.texture = background;
                bg.color = new Color(0.65f, 0.65f, 0.65f);
                string maskPath = Folder + "/Readability.png";
                if (!File.Exists(maskPath))
                {
                    var mask = new Texture2D(8, 225, TextureFormat.RGBA32, false);
                    for (int y = 0; y < 225; y++)
                        for (int x = 0; x < 8; x++)
                        {
                            float height = y / 224f;
                            float bottom = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.17f, 0.38f, height));
                            float top = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 0.92f, height));
                            mask.SetPixel(x, y, new Color(0.02f, 0.02f, 0.03f, Mathf.Max(0.25f, Mathf.Max(top, bottom))));
                        }
                    mask.Apply();
                    File.WriteAllBytes(maskPath, mask.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(mask);
                    AssetDatabase.ImportAsset(maskPath);
                }
                Element<RawImage>(canvas.transform, "Readability", 0, 0, 1600, 900).texture = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
                Label(canvas.transform, "Preview Notice", 800, 82, 750, 65, "MENU PREVIEW / MAIN MENU", 18, TextAnchor.UpperRight);
                Text notice = canvas.transform.Find("Preview Notice").GetComponent<Text>();
                var panel = new GameObject("Menu Panel", typeof(RectTransform));
                panel.transform.SetParent(canvas.transform, false);
                var panelRect = (RectTransform)panel.transform;
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
                Text location = Label(panel.transform, "Location", 46, 34, 600, 45, "BELLEVUE", 26);
                Label(panel.transform, "Watch", 1354, 34, 200, 45, "07:58", 26, TextAnchor.UpperRight);
                Label(panel.transform, "Title Red", 40, 130, 700, 230, "BOKI", 190).color = new Color(0.65f, 0.06f, 0.1f);
                Label(panel.transform, "Title Green", 52, 130, 700, 230, "BOKI", 190).color = new Color(0.08f, 0.35f, 0.3f);
                Label(panel.transform, "Title", 46, 130, 700, 230, "BOKI", 190);
                Text subtitle = Label(panel.transform, "Subtitle", 52, 372, 950, 60, "", 24);
                Text details = Label(panel.transform, "Run Summary", 1050, 150, 500, 360, "", 24);
                var outline = details.gameObject.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(2, -2);
                Text hint = Label(panel.transform, "Hints", 1000, 770, 550, 75, "", 24, TextAnchor.LowerRight);
                var view = canvasObject.AddComponent<MenuScreen>();
                var serialized = new SerializedObject(view);
                Set(serialized, "panel", panel);
                Set(serialized, "location", location);
                Set(serialized, "subtitle", subtitle);
                Set(serialized, "details", details);
                Set(serialized, "hint", hint);
                serialized.FindProperty("buttons").arraySize = 4;
                for (int i = 0; i < 4; i++)
                {
                    var image = Element<Image>(panel.transform, "Option " + i, 46, 635 + i * 51, 640, 48);
                    image.raycastTarget = true;
                    var button = image.gameObject.AddComponent<Button>();
                    var colors = button.colors;
                    colors.normalColor = new Color(0, 0, 0, 0);
                    colors.highlightedColor = colors.selectedColor = new Color(0.5f, 0.06f, 0.08f, 0.65f);
                    colors.pressedColor = new Color(0.7f, 0.1f, 0.1f, 0.8f);
                    button.colors = colors;
                    button.navigation = new Navigation { mode = Navigation.Mode.None };
                    Label(button.transform, "Label", 34, 0, 600, 48, "", 30, TextAnchor.MiddleLeft);
                    UnityEventTools.AddIntPersistentListener(button.onClick, view.Activate, i);
                    serialized.FindProperty("buttons").GetArrayElementAtIndex(i).objectReferenceValue = button;
                }
                var effects = new GameObject("Screen Effects", typeof(RectTransform));
                effects.transform.SetParent(canvas.transform, false);
                var effectRect = (RectTransform)effects.transform;
                effectRect.anchorMin = Vector2.zero;
                effectRect.anchorMax = Vector2.one;
                effectRect.offsetMin = effectRect.offsetMax = Vector2.zero;
                string effectsPath = Folder + "/ScreenEffects.png";
                if (!File.Exists(effectsPath))
                {
                    var texture = new Texture2D(400, 225, TextureFormat.RGBA32, false);
                    for (int y = 0; y < 225; y++)
                        for (int x = 0; x < 400; x++)
                        {
                            float edge = Mathf.Max(Mathf.Abs(x / 399f * 2 - 1), Mathf.Abs(y / 224f * 2 - 1));
                            float alpha = Mathf.SmoothStep(0, 0.65f, Mathf.InverseLerp(0.65f, 1, edge));
                            texture.SetPixel(x, y, new Color(0.3f, 0.01f, 0.02f, alpha));
                        }
                    texture.Apply();
                    File.WriteAllBytes(effectsPath, texture.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(texture);
                    AssetDatabase.ImportAsset(effectsPath);
                }
                Element<RawImage>(effects.transform, "Red Edges", 0, 0, 1600, 900).texture = AssetDatabase.LoadAssetAtPath<Texture2D>(effectsPath);
                Set(serialized, "effects", effects);
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(Folder + "/MenuInput.asset"))
                    if (asset is InputActionReference reference)
                        Set(serialized, reference.name.ToLowerInvariant(), reference);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var preview = canvasObject.AddComponent<MenuPreview>();
                var previewSerialized = new SerializedObject(preview);
                Set(previewSerialized, "screen", view);
                Set(previewSerialized, "notice", notice);
                previewSerialized.ApplyModifiedPropertiesWithoutUndo();
                var events = new GameObject("Menu Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.GetComponent<EventSystem>().sendNavigationEvents = false;
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("MENU_PREVIEW_CREATED: open " + ScenePath + ". No gameplay scenes or build settings changed.");
            }
            finally
            {
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static void Set(SerializedObject target, string name, UnityEngine.Object value)
            => target.FindProperty(name).objectReferenceValue = value;

        private static T Element<T>(Transform parent, string name, float x, float y, float width, float height) where T : Graphic
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(T));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            T graphic = obj.GetComponent<T>();
            graphic.raycastTarget = false;
            return graphic;
        }

        private static Text Label(Transform parent, string name, float x, float y, float width, float height,
            string content, int size, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            Text text = Element<Text>(parent, name, x, y, width, height);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.text = content;
            text.color = Paper;
            text.alignment = alignment;
            return text;
        }
    }
}
