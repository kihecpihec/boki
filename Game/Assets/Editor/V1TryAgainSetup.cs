using System;
using Boki.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Boki.Editor
{
    public static class V1TryAgainSetup
    {
        private static readonly Color Paper = new Color32(216, 208, 192, 255);
        private static readonly Color Muted = new Color32(164, 154, 136, 255);
        private const string Fonts = "Assets/UI/Upgrades/Fonts/";

        [MenuItem("Boki/Version 1/Upgrade Try Again Screens")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string map in new[] { "V1_Pohorje", "V1_Maribor", "V1_FERI" })
                {
                    var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + map + ".unity");
                    V1GameFlow flow = null;
                    foreach (var root in scene.GetRootGameObjects())
                        if (root.GetComponentInChildren<V1GameFlow>(true) is V1GameFlow found) flow = found;
                    if (flow == null) throw new InvalidOperationException("Missing V1GameFlow in " + map);
                    Restyle(new SerializedObject(flow));
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("V1_TRY_AGAIN_WIRED: " + map);
                }
            }
            finally
            {
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        public static void UpgradeAndVerify()
        {
            Upgrade();
            VerifyWithPreview();
        }

        public static void VerifyWithPreview()
        {
            CapturePreview();
            SmokeChecks();
        }

        public static void CapturePreview()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/V1_Pohorje.unity");
            var flow = UnityEngine.Object.FindAnyObjectByType<V1GameFlow>();
            var data = new SerializedObject(flow);
            var panel = (GameObject)data.FindProperty("panel").objectReferenceValue;
            panel.SetActive(true);
            ((Text)data.FindProperty("details").objectReferenceValue).text =
                "Your run has ended.\n\nKILLS  12   XP  180\nUPGRADES  3\n\nRoute discoveries are kept.\nStart again at Pohorje.";
            ((Button)data.FindProperty("resume").objectReferenceValue).gameObject.SetActive(false);
            ((Button)data.FindProperty("retry").objectReferenceValue).Select();
            var canvas = flow.GetComponent<Canvas>();
            var cameraObject = new GameObject("Try Again Preview Camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            var target = new RenderTexture(1280, 720, 24);
            camera.targetTexture = target;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << 5;
            foreach (var child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            foreach (var text in canvas.GetComponentsInChildren<Text>())
                text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
            Canvas.ForceUpdateCanvases();
            camera.Render();
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var capture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            capture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            capture.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "boki-try-again.png"), capture.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(capture);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }

        private static double nextCheck;
        private static int smokeMap;
        private static bool restarting;
        private static readonly string[] Maps = { "V1_Pohorje", "V1_Maribor", "V1_FERI" };

        public static void SmokeChecks()
        {
            SessionState.SetBool("Boki.TryAgainSmoke", true);
            EditorSceneManager.OpenScene("Assets/Scenes/V1_Pohorje.unity");
            ResumeSmokeChecks();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void ResumeSmokeChecks()
        {
            if (!SessionState.GetBool("Boki.TryAgainSmoke", false)) return;
            nextCheck = EditorApplication.timeSinceStartup + 5;
            EditorApplication.update -= CheckScreen;
            EditorApplication.update += CheckScreen;
        }

        private static void CheckScreen()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextCheck) return;
            try
            {
                var flow = UnityEngine.Object.FindAnyObjectByType<V1GameFlow>();
                if (flow == null) throw new InvalidOperationException("Missing V1 flow.");
                var data = new SerializedObject(flow);
                var panel = (GameObject)data.FindProperty("panel").objectReferenceValue;
                var health = (Boki.Player.PlayerHealth)data.FindProperty("health").objectReferenceValue;
                if (restarting)
                {
                    if (SceneManager.GetActiveScene().name != "V1_Pohorje" || flow.IsTerminal || panel.activeSelf
                        || health.Current != health.Maximum)
                        throw new InvalidOperationException("Retry did not reset the run at Pohorje.");
                    Debug.Log("V1_TRY_AGAIN_SMOKE_PASSED: " + Maps[smokeMap]);
                    if (++smokeMap == Maps.Length)
                    {
                        SessionState.SetBool("Boki.TryAgainSmoke", false);
                        EditorApplication.Exit(0);
                        return;
                    }
                    restarting = false;
                    EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/" + Maps[smokeMap] + ".unity",
                        new LoadSceneParameters(LoadSceneMode.Single));
                    nextCheck = EditorApplication.timeSinceStartup + 5;
                    return;
                }
                health.TakeDamage(health.Maximum);
                var title = (Text)data.FindProperty("title").objectReferenceValue;
                var retry = (Button)data.FindProperty("retry").objectReferenceValue;
                if (!flow.IsTerminal || !panel.activeInHierarchy || title.text != "Try again" || Time.timeScale != 0f
                    || panel.transform.Find("Try Again Styling/Screen Effects") == null
                    || UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != retry.gameObject)
                    throw new InvalidOperationException("Death screen visibility, styling, pause or selection failed.");
                Canvas.ForceUpdateCanvases();
                var details = (Text)data.FindProperty("details").objectReferenceValue;
                if (details.preferredHeight > details.rectTransform.rect.height)
                    throw new InvalidOperationException("Death summary overflows its text area.");
                var caption = retry.GetComponentInChildren<Text>();
                if (caption.preferredHeight > caption.rectTransform.rect.height)
                    throw new InvalidOperationException("Retry caption overflows its text area.");
                retry.onClick.Invoke();
                restarting = true;
                nextCheck = EditorApplication.timeSinceStartup + 5;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetBool("Boki.TryAgainSmoke", false);
                EditorApplication.Exit(1);
            }
        }

        private static void Restyle(SerializedObject flow)
        {
            var panel = (GameObject)flow.FindProperty("panel").objectReferenceValue;
            var title = (Text)flow.FindProperty("title").objectReferenceValue;
            var details = (Text)flow.FindProperty("details").objectReferenceValue;
            var retry = (Button)flow.FindProperty("retry").objectReferenceValue;
            var resume = (Button)flow.FindProperty("resume").objectReferenceValue;
            if (panel == null || title == null || details == null || retry == null || resume == null)
                throw new InvalidOperationException("Incomplete V1 end-screen references.");
            var existing = panel.transform.Find("Try Again Styling");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            panel.GetComponent<Image>().color = new Color32(8, 6, 6, 255);
            var styling = new GameObject("Try Again Styling", typeof(RectTransform));
            styling.transform.SetParent(panel.transform, false);
            var rect = (RectTransform)styling.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            styling.transform.SetAsFirstSibling();
            var bg = Graphic<RawImage>(styling.transform, "Backdrop", 0, 0, 1280, 720);
            bg.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/Menus/Background.png");
            bg.color = new Color(0.42f, 0.38f, 0.38f, 1);
            bg.uvRect = new Rect(0, 0.24f, 1, 0.55f);
            Graphic<RawImage>(styling.transform, "Dimming", 0, 0, 1280, 720).texture =
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/Upgrades/Dimming.png");
            Label(styling.transform, "Location", 38, 28, 700, 30, "BOKI / END OF THE ROAD", 19, Muted);
            var absent = Label(styling.transform, "Absent", 38, 230, 620, 145, "BOKI", 100, Paper);
            absent.fontStyle = FontStyle.Bold;
            var echo = absent.gameObject.AddComponent<Shadow>();
            echo.effectColor = new Color32(138, 16, 16, 255);
            echo.effectDistance = new Vector2(4, -2);
            Label(styling.transform, "Atmosphere", 42, 365, 570, 60, "The road is still there.", 23, Muted).font = Serif();
            Graphic<Image>(styling.transform, "Red Rule", 744, 186, 72, 3).color = new Color32(155, 28, 28, 255);
            Place(title.rectTransform, 744, 215, 498, 90);
            Style(title, 42, Paper, true);
            title.text = "Try again";
            title.fontStyle = FontStyle.BoldAndItalic;
            Place(details.rectTransform, 744, 320, 485, 174);
            Style(details, 16, Muted, false);
            details.lineSpacing = 1.15f;
            StyleButton(retry, 744, 506, "TRY AGAIN");
            StyleButton(resume, 744, 570, "RESUME");
            Label(styling.transform, "Keys", 744, 651, 498, 30, "ENTER confirm   /   Click to ride again", 14, Muted);
            Label(styling.transform, "Persistence", 38, 651, 650, 30, "YOUR DISCOVERIES STAY WITH YOU.", 14, Muted);
            var effects = Graphic<RawImage>(panel.transform, "Temporary Effects", 0, 0, 1280, 720);
            // Keep all generated decoration under one root so repeated upgrades are safe.
            effects.transform.SetParent(styling.transform, false);
            effects.name = "Screen Effects";
            effects.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/Upgrades/ScreenEffects.png");
            panel.SetActive(false);
        }

        private static void StyleButton(Button button, float x, float y, string caption)
        {
            Place((RectTransform)button.transform, x, y, 490, 52);
            button.GetComponent<Image>().color = Color.white;
            var colors = button.colors;
            colors.normalColor = new Color32(48, 27, 27, 255);
            colors.highlightedColor = colors.selectedColor = new Color32(138, 24, 24, 255);
            colors.pressedColor = new Color32(196, 48, 48, 255);
            button.colors = colors;
            var label = button.GetComponentInChildren<Text>(true);
            Style(label, 23, Paper, false);
            label.rectTransform.sizeDelta = new Vector2(470, 64);
            label.alignment = TextAnchor.MiddleCenter;
            label.text = caption;
        }

        private static Font Serif() => AssetDatabase.LoadAssetAtPath<Font>(Fonts + "LiberationSerif-Regular.ttf");
        private static void Style(Text text, int size, Color color, bool serif)
        {
            text.font = serif ? Serif() : AssetDatabase.LoadAssetAtPath<Font>(Fonts + "LiberationMono-Regular.ttf");
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.raycastTarget = false;
        }
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
        private static T Graphic<T>(Transform parent, string name, float x, float y, float width, float height) where T : Graphic
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(T));
            obj.transform.SetParent(parent, false);
            Place((RectTransform)obj.transform, x, y, width, height);
            var graphic = obj.GetComponent<T>();
            graphic.raycastTarget = false;
            return graphic;
        }
        private static Text Label(Transform parent, string name, float x, float y, float width, float height, string content, int size, Color color)
        {
            var text = Graphic<Text>(parent, name, x, y, width, height);
            Style(text, size, color, false);
            text.text = content;
            return text;
        }
    }
}
