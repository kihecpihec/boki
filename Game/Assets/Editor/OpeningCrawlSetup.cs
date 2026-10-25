using System;
using System.IO;
using System.Linq;
using Boki.Core;
using Boki.Intro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Boki.Editor
{
    // The opening crawl is its own scene with no map assets, so it cannot collide with anyone's level work.
    // RunSession.StartV1Run loads it from the main menu; it ends in RunSession.BeginV1Run.
    public static class OpeningCrawlSetup
    {
        private const string Folder = "Assets/Cutscenes/Crawl";
        private const string ScenePath = RunSession.OpeningCrawlScene;
        private const string MainMenuPath = RunSession.MainMenuScene;
        private const string FadePath = Folder + "/FarFade.png";
        private const string InputPath = "Assets/Cutscenes/Intro/IntroInput.asset";
        private const string BedPath = "Assets/Audio/Ambience/feri_ventilation.wav";
        private const string StingerPath = "Assets/Audio/Sfx/arena_intro_stinger.wav";
        private const string HeartbeatPath = "Assets/Audio/Sfx/intro_heartbeat.wav";
        private const float TiltDegrees = 48f;
        private const float PlaneWidth = 1180f;
        private const float TravelHeight = 1900f;

        private static readonly Color Paper = new Color(0.91f, 0.89f, 0.86f);
        private static readonly Color Lcd = new Color(0.62f, 0.72f, 0.3f);
        private static readonly Color Blood = new Color(0.78f, 0.16f, 0.14f);

        // Story §3 OPENING CRAWL holds the same copy.
        private const string PrefaceText = "Last night, on a hill above Maribor,\na rave went on a little longer than planned...";
        private const string CrawlBody =
            "<size=56>PROLOGUE</size>\n<size=78>THE MORNING AFTER</size>\n\n"
            + "Boki said he would have one drink and leave early.\n\n"
            + "He did not leave early.\n\n"
            + "Somewhere between the bass and the sunrise, he strapped a rave laser to his scooter. "
            + "It seemed important at the time. Now it is the only thing keeping the dark away.\n\n"
            + "When the music stopped, the city did not sound the same.\n\n"
            + "He remembers green light. He remembers a promise. He does not remember what he did, "
            + "or why something has been following him all night.\n\n"
            + "It is nearly 08:00. The lecture at FERI will not wait. The professor will not forgive.\n\n"
            + "<color=#C82A24>Guilt</color> is patient.\nBoki is late.";

        [MenuItem("Boki/Intro/Rebuild Opening Crawl")]
        public static void Create()
        {
            IntroPreviewSetup.RequireIdle();
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Directory.CreateDirectory(Folder);
                EnsureFadeSprite();
                AssetDatabase.Refresh();

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var cameraObject = new GameObject("Crawl Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
                var camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.fieldOfView = 60f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 200f;

                var canvasObject = new GameObject("Crawl Canvas", typeof(Canvas), typeof(CanvasScaler));
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1600f, 900f);
                scaler.matchWidthOrHeight = 0.5f;
                Transform root = canvasObject.transform;

                CanvasGroup preface = BuildCard(root, "Preface", PrefaceText, 46, Lcd, Vector2.zero, new Vector2(1500f, 220f));

                var titleCard = IntroPreviewSetup.Rect(root, "Title", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 500f));
                var titleGroup = titleCard.gameObject.AddComponent<CanvasGroup>();
                titleGroup.alpha = 0f;
                titleGroup.blocksRaycasts = false;
                titleGroup.interactable = false;
                Words(titleCard, "Name", "BOKI", 300, Blood, TextAnchor.MiddleCenter, new Vector2(0f, 70f), new Vector2(1500f, 330f), true);
                Words(titleCard, "Subtitle", "LATE TO FERI", 96, Paper, TextAnchor.MiddleCenter, new Vector2(0f, -150f), new Vector2(1500f, 120f), false);

                var tilt = IntroPreviewSetup.Rect(root, "Crawl Tilt", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(PlaneWidth, TravelHeight));
                tilt.localRotation = Quaternion.Euler(TiltDegrees, 0f, 0f);
                Text crawlText = Words(tilt, "Crawl Text", CrawlBody, 58, Paper, TextAnchor.UpperCenter,
                    Vector2.zero, new Vector2(PlaneWidth, 0f), false);
                var crawlRect = crawlText.rectTransform;
                crawlRect.anchorMin = crawlRect.anchorMax = new Vector2(0.5f, 0f);
                crawlRect.pivot = new Vector2(0.5f, 1f);
                crawlText.horizontalOverflow = HorizontalWrapMode.Wrap;
                crawlText.lineSpacing = 1.25f;
                tilt.gameObject.SetActive(false);

                // Dissolves the far end of the plane into the dark, so the text does not just end at an edge.
                var farFade = IntroPreviewSetup.Rect(root, "Far Fade", new Vector2(0f, 0.38f), Vector2.one,
                    new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
                farFade.sprite = IntroPreviewSetup.Load<Sprite>(FadePath);
                farFade.raycastTarget = false;

                var hint = IntroPreviewSetup.Label(IntroPreviewSetup.Rect(root, "Skip Hint", new Vector2(1f, 0f), new Vector2(1f, 0f),
                    new Vector2(1f, 0f), new Vector2(-48f, 40f), new Vector2(400f, 36f)),
                    "Text", "ENTER: SKIP", 22, new Color(Paper.r, Paper.g, Paper.b, 0.6f), TextAnchor.LowerRight);

                var blackout = IntroPreviewSetup.Fill(IntroPreviewSetup.Rect(root, "Blackout", Vector2.zero, Vector2.one,
                    new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), Color.black);
                blackout.gameObject.SetActive(false);

                var crawlObject = new GameObject("Opening Crawl");
                var bed = crawlObject.AddComponent<AudioSource>();
                Configure(bed, IntroPreviewSetup.Load<AudioClip>(BedPath));
                var effects = crawlObject.AddComponent<AudioSource>();
                Configure(effects, null);
                var crawl = crawlObject.AddComponent<OpeningCrawl>();
                var input = AssetDatabase.LoadAllAssetsAtPath(InputPath).OfType<InputActionReference>().Single(r => r.name == "Skip");
                var so = new SerializedObject(crawl);
                so.FindProperty("preface").objectReferenceValue = preface;
                so.FindProperty("title").objectReferenceValue = titleCard;
                so.FindProperty("titleGroup").objectReferenceValue = titleGroup;
                so.FindProperty("crawlTilt").objectReferenceValue = tilt;
                so.FindProperty("crawlText").objectReferenceValue = crawlText;
                so.FindProperty("blackout").objectReferenceValue = blackout;
                so.FindProperty("skipHint").objectReferenceValue = hint.transform.parent.gameObject;
                so.FindProperty("bed").objectReferenceValue = bed;
                so.FindProperty("effects").objectReferenceValue = effects;
                so.FindProperty("titleStinger").objectReferenceValue = IntroPreviewSetup.Load<AudioClip>(StingerPath);
                so.FindProperty("heartbeat").objectReferenceValue = IntroPreviewSetup.Load<AudioClip>(HeartbeatPath);
                so.FindProperty("skipAction").objectReferenceValue = input;
                so.FindProperty("travelHeight").floatValue = TravelHeight;
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.SaveScene(scene, ScenePath);
                AddToBuildSettings();
                AssetDatabase.SaveAssets();
                Debug.Log("OPENING_CRAWL_REBUILT: " + ScenePath + " (no map asset used). It is in the build list right after the main menu.");
            }
            finally
            {
                IntroPreviewSetup.RestoreScenes(previous);
            }
        }

        [MenuItem("Boki/Intro/Open Opening Crawl")]
        public static void Open()
        {
            IntroPreviewSetup.RequireIdle();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("Rebuild the opening crawl first.");
            EditorSceneManager.OpenScene(ScenePath);
        }

        private static CanvasGroup BuildCard(Transform parent, string name, string content, int size, Color color, Vector2 position, Vector2 dimensions)
        {
            var card = IntroPreviewSetup.Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), position, dimensions);
            var group = card.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            Words(card, "Text", content, size, color, TextAnchor.MiddleCenter, Vector2.zero, dimensions, false);
            return group;
        }

        private static Text Words(Transform parent, string name, string content, int size, Color color, TextAnchor alignment,
            Vector2 position, Vector2 dimensions, bool bold)
        {
            var rect = IntroPreviewSetup.Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), position, dimensions);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.text = content;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void Configure(AudioSource source, AudioClip clip)
        {
            source.clip = clip;
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
        }

        // A 1 x N column of black whose opacity runs from solid at the top to clear at the bottom.
        private static void EnsureFadeSprite()
        {
            if (File.Exists(FadePath)) return;
            const int height = 256;
            var texture = new Texture2D(4, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                float t = y / (height - 1f);
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.15f) / 0.75f));
                for (int x = 0; x < 4; x++) texture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
            }
            File.WriteAllBytes(FadePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(FadePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(FadePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        // Right after the main menu, so scene indices that already matter (the menu is 0) do not move.
        private static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath)) return;
            int menu = scenes.FindIndex(s => s.path == MainMenuPath);
            scenes.Insert(menu + 1, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
