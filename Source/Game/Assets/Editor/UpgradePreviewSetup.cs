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
    public static class UpgradePreviewSetup
    {
        private const string Folder = "Assets/UI/Upgrades";
        private const string ScenePath = "Assets/Scenes/Upgrade_Preview.unity";
        private const string PrefabPath = Folder + "/UpgradeScreen.prefab";
        private static readonly Color Paper = new Color32(230, 226, 214, 255);

        [MenuItem("Boki/Menus/Create Isolated Upgrade Preview")]
        public static void Create()
        {
            RequireSavedScenes();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.Log("Upgrade preview already exists; existing edits preserved.");
                return;
            }
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
                InputActionAsset input = CreateInput();
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var backgroundCanvas = CanvasObject("Preview Backdrop", 0);
                var background = Element<RawImage>(backgroundCanvas.transform, "Backdrop", 0, 0, 1600, 900);
                background.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/Menus/Background.png");
                background.color = new Color(0.65f, 0.65f, 0.65f);
                Text notice = Label(backgroundCanvas.transform, "Preview Notice", 46, 65, 780, 80, "", 20);
                var reopenImage = Element<Image>(backgroundCanvas.transform, "Show Sample Offer", 46, 160, 370, 60);
                reopenImage.color = new Color32(65, 15, 15, 255);
                reopenImage.raycastTarget = true;
                Button reopen = reopenImage.gameObject.AddComponent<Button>();
                Label(reopen.transform, "Label", 20, 0, 330, 60, "SHOW SAMPLE OFFER", 24, TextAnchor.MiddleCenter);

                GameObject canvasObject;
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                    canvasObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
                else
                {
                    canvasObject = BuildScreen(input);
                    PrefabUtility.SaveAsPrefabAssetAndConnect(canvasObject, PrefabPath, InteractionMode.AutomatedAction);
                }
                var preview = backgroundCanvas.AddComponent<UpgradePreview>();
                var serialized = new SerializedObject(preview);
                Set(serialized, "screen", canvasObject.GetComponent<UpgradeScreen>());
                Set(serialized, "notice", notice);
                Set(serialized, "reopen", reopen);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var events = new GameObject("Upgrade Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.GetComponent<EventSystem>().sendNavigationEvents = false;
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("UPGRADE_PREVIEW_CREATED: " + ScenePath);
            }
            finally
            {
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static GameObject BuildScreen(InputActionAsset input)
        {
            var root = CanvasObject("Upgrade Screen", 20);
            var panel = new GameObject("Offer Panel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var dim = Element<RawImage>(panel.transform, "Pause Dimming", 0, 0, 1600, 900);
            dim.texture = CreateOverlay(false);
            dim.raycastTarget = true;
            Label(panel.transform, "Paused", 0, 26, 1600, 30, "— PAUSED —", 18, TextAnchor.UpperCenter).color = new Color32(154, 143, 128, 255);
            var redTitle = Label(panel.transform, "Title Red", 933, 150, 620, 70, "Level up", 58);
            redTitle.color = new Color32(138, 16, 16, 255);
            redTitle.fontStyle = FontStyle.BoldAndItalic;
            redTitle.font = SerifFont();
            var title = Label(panel.transform, "Title", 930, 150, 620, 70, "Level up", 58);
            title.color = new Color32(200, 191, 174, 255);
            title.fontStyle = FontStyle.BoldAndItalic;
            title.font = SerifFont();
            var subtitle = Label(panel.transform, "Subtitle", 930, 220, 620, 36, "Something in the dark noticed. Take one.", 22);
            subtitle.fontStyle = FontStyle.Italic;
            subtitle.font = SerifFont();
            subtitle.color = new Color32(138, 128, 112, 255);
            var screen = root.AddComponent<UpgradeScreen>();
            var serialized = new SerializedObject(screen);
            Set(serialized, "panel", panel);
            foreach (string name in new[] { "buttons", "titles", "descriptions", "tags" })
                serialized.FindProperty(name).arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var border = Element<Image>(panel.transform, "Card " + (i + 1), 930, 276 + i * 126, 620, 110);
                border.raycastTarget = true;
                var button = border.gameObject.AddComponent<Button>();
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                var colors = button.colors;
                colors.normalColor = new Color32(58, 48, 48, 255);
                colors.selectedColor = colors.highlightedColor = new Color32(155, 28, 28, 255);
                colors.pressedColor = new Color32(196, 48, 48, 255);
                button.colors = colors;
                Element<Image>(border.transform, "Card Face", 6, 2, 612, 106).color = new Color32(11, 8, 8, 245);
                Label(border.transform, "Number", 20, 14, 35, 40, (i + 1).ToString(), 26).color = new Color32(164, 154, 136, 255);
                Text cardTitle = Label(border.transform, "Title", 62, 25, 540, 36, "", 28);
                cardTitle.fontStyle = FontStyle.Bold;
                cardTitle.font = SerifFont();
                Text description = Label(border.transform, "Effect", 62, 66, 540, 40, "", 19);
                description.color = new Color32(164, 154, 136, 255);
                Text tag = Label(border.transform, "Tag", 280, 6, 322, 20, "", 13, TextAnchor.UpperRight);
                tag.color = new Color32(107, 98, 88, 255);
                SetArray(serialized, "buttons", i, button);
                SetArray(serialized, "titles", i, cardTitle);
                SetArray(serialized, "descriptions", i, description);
                SetArray(serialized, "tags", i, tag);
                UnityEventTools.AddIntPersistentListener(button.onClick, screen.RequestChoice, i);
            }
            Text arrow = Label(panel.transform, "Selection Arrow", 896, 284, 32, 40, "▶", 26);
            arrow.color = new Color32(196, 48, 48, 255);
            Set(serialized, "selectionArrow", arrow.rectTransform);
            Label(panel.transform, "Keys", 930, 675, 620, 56,
                "A / D choose   ENTER take   1 2 3 quick\nNo skip", 16).color = new Color32(164, 154, 136, 255);
            var effects = Element<RawImage>(panel.transform, "Screen Effects", 0, 0, 1600, 900);
            effects.texture = CreateOverlay(true);
            Set(serialized, "effects", effects.gameObject);
            serialized.FindProperty("quickSelect").arraySize = 3;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(input)))
            {
                if (!(asset is InputActionReference reference)) continue;
                if (reference.name.StartsWith("Choice", StringComparison.Ordinal))
                    SetArray(serialized, "quickSelect", int.Parse(reference.name.Substring(6)) - 1, reference);
                else Set(serialized, reference.name.ToLowerInvariant(), reference);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            // Prefabs start hidden; the integration supplies three cards before showing them.
            panel.SetActive(false);
            return root;
        }

        private static InputActionAsset CreateInput()
        {
            string path = Folder + "/UpgradeInput.asset";
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            if (input != null) return input;
            input = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = input.AddActionMap("Upgrade");
            var navigate = map.AddAction("Navigate", InputActionType.Button);
            foreach (var pair in new[] { new[] { "a", "d" }, new[] { "leftArrow", "rightArrow" }, new[] { "w", "s" }, new[] { "upArrow", "downArrow" } })
                navigate.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/" + pair[0]).With("Positive", "<Keyboard>/" + pair[1]);
            map.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter");
            for (int i = 1; i <= 3; i++) map.AddAction("Choice" + i, InputActionType.Button, "<Keyboard>/" + i);
            AssetDatabase.CreateAsset(input, path);
            foreach (InputAction action in map.actions)
            {
                var reference = InputActionReference.Create(action);
                reference.name = action.name;
                AssetDatabase.AddObjectToAsset(reference, input);
            }
            AssetDatabase.SaveAssets();
            return input;
        }

        private static Texture2D CreateOverlay(bool effects)
        {
            string path = Folder + (effects ? "/ScreenEffects.png" : "/Dimming.png");
            if (!File.Exists(path))
            {
                var texture = new Texture2D(400, 225, TextureFormat.RGBA32, false);
                for (int y = 0; y < 225; y++)
                    for (int x = 0; x < 400; x++)
                    {
                        float edge = Mathf.Max(Mathf.Abs(x / 399f * 2 - 1), Mathf.Abs(y / 224f * 2 - 1));
                        float vignette = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.6f, 1f, edge));
                        float alpha = effects ? Mathf.Max(vignette * 0.5f, y % 3 == 0 ? 0.1f : 0)
                            : Mathf.Lerp(0.4f, 0.82f, Mathf.InverseLerp(0.4f, 0.85f, x / 399f));
                        texture.SetPixel(x, y, effects ? new Color(0.35f, 0.02f, 0.02f, alpha) : new Color(0, 0, 0, alpha));
                    }
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static GameObject CanvasObject(string name, int order)
        {
            var obj = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = obj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = obj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            return obj;
        }

        private static void RequireSavedScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");
        }

        private static void Set(SerializedObject target, string name, UnityEngine.Object value)
            => target.FindProperty(name).objectReferenceValue = value;
        private static void SetArray(SerializedObject target, string name, int index, UnityEngine.Object value)
            => target.FindProperty(name).GetArrayElementAtIndex(index).objectReferenceValue = value;

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
            text.font = AssetDatabase.LoadAssetAtPath<Font>(Folder + "/Fonts/LiberationMono-Regular.ttf");
            text.fontSize = size;
            text.text = content;
            text.color = Paper;
            text.alignment = alignment;
            return text;
        }

        private static Font SerifFont()
            => AssetDatabase.LoadAssetAtPath<Font>(Folder + "/Fonts/LiberationSerif-Regular.ttf");
    }
}
