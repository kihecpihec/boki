using System;
using Boki.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Boki.Editor
{
    public static class V1TryAgainSetup
    {
        private static readonly Color Paper = new Color32(214, 201, 183, 255);
        private static readonly Color Muted = new Color32(166, 158, 141, 255);
        private const string Fonts = "Assets/UI/Upgrades/Fonts/";

        [MenuItem("Boki/Version 1/Upgrade Try Again Screens")]
        public static void Upgrade()
        {
            RequireCleanEditor();
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
            RequireCleanEditor();
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/V1_Pohorje.unity");
                var flow = UnityEngine.Object.FindAnyObjectByType<V1GameFlow>();
                var data = new SerializedObject(flow);
                var panel = (GameObject)data.FindProperty("panel").objectReferenceValue;
                panel.SetActive(true);
                ((Text)data.FindProperty("details").objectReferenceValue).gameObject.SetActive(false);
                ((Text)data.FindProperty("retryKills").objectReferenceValue).text = "12";
                ((Text)data.FindProperty("retryXp").objectReferenceValue).text = "45";
                ((Text)data.FindProperty("retryUpgrades").objectReferenceValue).text = "3";
                ((Button)data.FindProperty("resume").objectReferenceValue).gameObject.SetActive(false);
                var retry = (Button)data.FindProperty("retry").objectReferenceValue;
                retry.targetGraphic.CrossFadeColor(retry.colors.selectedColor, 0f, true, true);
                var canvas = flow.GetComponent<Canvas>();
                Capture(canvas, 1280, 720, "boki-try-again.png");
                Capture(canvas, 1920, 1080, "boki-try-again-1080.png");
            }
            finally
            {
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static void Capture(Canvas canvas, int width, int height, string filename)
        {
            var cameraObject = new GameObject("Try Again Preview Camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            var target = new RenderTexture(width, height, 24);
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
            var capture = new Texture2D(width, height, TextureFormat.RGB24, false);
            capture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            capture.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), filename), capture.EncodeToPNG());
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
            RequireCleanEditor();
            smokeMap = 0;
            restarting = false;
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
                    var restarted = Boki.Core.RunSession.Instance.State;
                    if (SceneManager.GetActiveScene().name != "V1_Pohorje" || flow.IsTerminal || panel.activeSelf
                        || health.Current != health.Maximum || restarted.KillCount != 0 || restarted.XP != 0
                        || restarted.Progression.Selections != 0 || restarted.LightBattery.Capacity != 100f
                        || restarted.Choice(Boki.Routes.RouteLevel.Forest) != Boki.Routes.RouteChoice.None)
                        throw new InvalidOperationException("Retry did not reset the run at Pohorje.");
                    Debug.Log("V1_TRY_AGAIN_SMOKE_PASSED: " + Maps[smokeMap]);
                    if (++smokeMap == Maps.Length)
                    {
                        flow.Victory();
                        if (((Text)data.FindProperty("title").objectReferenceValue).text != "Danes je sobota."
                            || !((Text)data.FindProperty("details").objectReferenceValue).gameObject.activeInHierarchy
                            || ((GameObject)data.FindProperty("retrySummary").objectReferenceValue).activeSelf
                            || ((Text)data.FindProperty("retryLocation").objectReferenceValue).gameObject.activeSelf)
                            throw new InvalidOperationException("Victory did not preserve its ending text and hide death-only content.");
                        Debug.Log("V1_TRY_AGAIN_VICTORY_PASSED");
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
                var state = Boki.Core.RunSession.Instance.State;
                state.KillCount = 12;
                state.Progression.AddXp(45);
                for (int i = 0; i < 3; i++)
                {
                    if (!state.Progression.CreateOffer(new System.Random(i))
                        || !state.Progression.TrySelect(0, state.Progression.OfferVersion))
                        throw new InvalidOperationException("Could not prepare the run summary fixture.");
                }
                health.TakeDamage(health.Maximum);
                var title = (Text)data.FindProperty("title").objectReferenceValue;
                var retry = (Button)data.FindProperty("retry").objectReferenceValue;
                if (!flow.IsTerminal || !panel.activeInHierarchy || title.text != "ABSENT" || Time.timeScale != 0f
                    || panel.transform.Find("Try Again Styling/Screen Effects") == null
                    || UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != retry.gameObject)
                    throw new InvalidOperationException("Death screen visibility, styling, pause or selection failed.");
                Canvas.ForceUpdateCanvases();
                var details = (Text)data.FindProperty("details").objectReferenceValue;
                var summary = (GameObject)data.FindProperty("retrySummary").objectReferenceValue;
                var location = (Text)data.FindProperty("retryLocation").objectReferenceValue;
                string expectedLocation = smokeMap == 0 ? "POHORJE" : smokeMap == 1 ? "MARIBOR" : "FERI";
                if (details.gameObject.activeSelf || !summary.activeInHierarchy || location.text != expectedLocation
                    || ((Text)data.FindProperty("retryKills").objectReferenceValue).text != state.KillCount.ToString()
                    || ((Text)data.FindProperty("retryXp").objectReferenceValue).text != state.XP.ToString()
                    || ((Text)data.FindProperty("retryUpgrades").objectReferenceValue).text != state.Progression.Selections.ToString())
                    throw new InvalidOperationException("Death screen did not display the live run summary and location.");
                foreach (var text in panel.GetComponentsInChildren<Text>())
                    if (!text.resizeTextForBestFit && (text.preferredHeight > text.rectTransform.rect.height
                        || text.preferredWidth > text.rectTransform.rect.width))
                        throw new InvalidOperationException("End-screen text overflows: " + text.name);
                var caption = retry.GetComponentInChildren<Text>();
                if (caption.preferredHeight > caption.rectTransform.rect.height)
                    throw new InvalidOperationException("Retry caption overflows its text area.");
                if (smokeMap == 1)
                    ExecuteEvents.Execute(retry.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
                else ExecuteEvents.Execute(retry.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
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

        internal static void Restyle(SerializedObject flow)
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
            bg.color = new Color(0.52f, 0.49f, 0.48f, 1);
            bg.uvRect = new Rect(0, 0.24f, 1, 0.55f);
            var dimming = Graphic<RawImage>(styling.transform, "Dimming", 0, 0, 1280, 720);
            dimming.texture =
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/Upgrades/Dimming.png");
            dimming.color = new Color(1, 1, 1, 0.55f);
            string scene = ((V1GameFlow)flow.targetObject).gameObject.scene.name;
            var location = Label(styling.transform, "Location", 390, 155, 500, 30,
                scene == "V1_Maribor" ? "MARIBOR" : scene == "V1_FERI" ? "FERI" : "POHORJE", 16, Muted);
            Place(title.rectTransform, 140, 196, 1000, 140);
            Style(title, 112, Paper, true);
            title.text = "ABSENT";
            var echo = title.GetComponent<Shadow>();
            if (echo == null) echo = title.gameObject.AddComponent<Shadow>();
            echo.effectColor = new Color32(101, 37, 38, 255);
            echo.effectDistance = new Vector2(2, -1);
            Place(details.rectTransform, 300, 342, 680, 135);
            Style(details, 16, Muted, false);
            details.lineSpacing = 1.15f;
            details.gameObject.SetActive(false);
            var summary = new GameObject("Run Summary", typeof(RectTransform));
            summary.transform.SetParent(styling.transform, false);
            Place((RectTransform)summary.transform, 0, 0, 1280, 720);
            Graphic<Image>(summary.transform, "Top Rule", 408, 352, 464, 1).color = new Color32(205, 189, 165, 54);
            Graphic<Image>(summary.transform, "Bottom Rule", 408, 452, 464, 1).color = new Color32(205, 189, 165, 54);
            var kills = Stat(summary.transform, 408, "KILLS");
            var xp = Stat(summary.transform, 563, "XP");
            var upgrades = Stat(summary.transform, 718, "UPGRADES");
            Graphic<Image>(summary.transform, "Kills Divider", 563, 373, 1, 60).color = new Color32(205, 189, 165, 43);
            Graphic<Image>(summary.transform, "XP Divider", 718, 373, 1, 60).color = new Color32(205, 189, 165, 43);
            StyleButton(retry, 464, 490, "TRY AGAIN");
            StyleButton(resume, 464, 592, "RESUME");
            Label(summary.transform, "Persistence", 390, 559, 500, 28, "Route discoveries kept.", 13, Muted);
            flow.FindProperty("retryLocation").objectReferenceValue = location;
            flow.FindProperty("retrySummary").objectReferenceValue = summary;
            flow.FindProperty("retryKills").objectReferenceValue = kills;
            flow.FindProperty("retryXp").objectReferenceValue = xp;
            flow.FindProperty("retryUpgrades").objectReferenceValue = upgrades;
            flow.ApplyModifiedPropertiesWithoutUndo();
            var effects = Graphic<RawImage>(panel.transform, "Temporary Effects", 0, 0, 1280, 720);
            // Keep all generated decoration under one root so repeated upgrades are safe.
            effects.transform.SetParent(styling.transform, false);
            effects.name = "Screen Effects";
            effects.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/Upgrades/ScreenEffects.png");
            effects.color = new Color(1, 1, 1, 0.08f);
            panel.SetActive(false);
        }

        private static Text Stat(Transform parent, float x, string caption)
        {
            var value = Label(parent, caption + " Value", x, 368, 154, 44, "0", 32, Paper);
            value.resizeTextForBestFit = true;
            value.resizeTextMinSize = 18;
            value.resizeTextMaxSize = 32;
            Label(parent, caption + " Label", x, 413, 154, 24, caption, 12, Muted);
            return value;
        }

        private static void StyleButton(Button button, float x, float y, string caption)
        {
            Place((RectTransform)button.transform, x, y, 352, 60);
            button.GetComponent<Image>().color = Color.white;
            var colors = button.colors;
            colors.normalColor = new Color32(63, 25, 23, 245);
            colors.highlightedColor = colors.selectedColor = new Color32(95, 29, 25, 245);
            colors.pressedColor = new Color32(125, 39, 33, 255);
            button.colors = colors;
            var border = button.GetComponent<Outline>();
            if (border == null) border = button.gameObject.AddComponent<Outline>();
            border.effectColor = new Color32(152, 80, 73, 255);
            border.effectDistance = Vector2.one;
            var label = button.GetComponentInChildren<Text>(true);
            Style(label, 19, Paper, false);
            Place(label.rectTransform, 30, 0, 292, 60);
            label.text = caption;
            if (button.transform.Find("Selection Arrow") == null)
                Label(button.transform, "Selection Arrow", 16, 0, 30, 60, "▸", 20, Paper);
        }

        private static Font Serif() => AssetDatabase.LoadAssetAtPath<Font>(Fonts + "LiberationSerif-Regular.ttf");
        private static void RequireCleanEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");
        }
        private static void Style(Text text, int size, Color color, bool serif)
        {
            text.font = serif ? Serif() : AssetDatabase.LoadAssetAtPath<Font>(Fonts + "LiberationMono-Regular.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Normal;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
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
