using System;
using Boki.Battery;
using Boki.Enemies;
using Boki.Player;
using Boki.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Boki.Editor
{
    public static class PlayerHudSetup
    {
        private const string ScenePath = "Assets/Scenes/Player_Model_Test.unity";
        private static readonly Color Paper = new Color(0.85f, 0.83f, 0.75f);

        [MenuItem("Boki/Player Model/Apply Mockup HUD to Test Scene")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                ScooterController movement = Find<ScooterController>(scene);
                if (movement == null || movement.GetComponent<PlayerHealth>() == null || movement.GetComponent<LightBattery>() == null)
                    throw new InvalidOperationException("The test player needs movement, health and light battery.");
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name == "Boki HUD") UnityEngine.Object.DestroyImmediate(root);
                    else if (root.name == "MovementHUD") root.SetActive(false);
                }
                foreach (BatteryView old in FindAll<BatteryView>(scene))
                {
                    old.enabled = false;
                    var serialized = new SerializedObject(old);
                    var label = serialized.FindProperty("statusText").objectReferenceValue as Text;
                    if (label != null) label.gameObject.SetActive(false);
                }
                var hud = new GameObject("Boki HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                SceneManager.MoveGameObjectToScene(hud, scene);
                var canvas = hud.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 20;
                var scaler = hud.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                scaler.matchWidthOrHeight = 0.5f;
                var edges = Element<RawImage>(hud.transform, "Guilt Edges", Vector2.zero, Vector2.zero, Vector2.zero);
                edges.rectTransform.anchorMax = Vector2.one;
                edges.color = Color.clear;
                var view = hud.AddComponent<PlayerHud>();
                Set(view, "health", movement.GetComponent<PlayerHealth>());
                Set(view, "battery", movement.GetComponent<LightBattery>());
                Set(view, "movement", movement);
                Set(view, "guilt", Find<GuiltChaser>(scene));
                Set(view, "guiltEdges", edges);
                Set(view, "healthText", Label(hud.transform, "HP", Vector2.zero, new Vector2(34, 130), new Vector2(270, 24), "HP 100/100"));
                Set(view, "healthFill", Bar(hud.transform, "Health", Vector2.zero, new Vector2(34, 117), new Vector2(245, 13), new Color(0.55f, 0.15f, 0.15f)));
                Set(view, "batteryText", Label(hud.transform, "Battery", Vector2.zero, new Vector2(34, 87), new Vector2(300, 24), "BAT 100/100"));
                Set(view, "batteryFill", Bar(hud.transform, "Battery Bar", Vector2.zero, new Vector2(34, 74), new Vector2(245, 13), new Color(0.42f, 0.46f, 0.15f)));
                var bottomCenter = new Vector2(0.5f, 0f);
                Set(view, "xpText", Label(hud.transform, "XP", bottomCenter, new Vector2(-90, 43), new Vector2(180, 24), "XP 0/3"));
                Set(view, "xpFill", Bar(hud.transform, "XP Bar", bottomCenter, new Vector2(-90, 28), new Vector2(180, 13), new Color(0.55f, 0.55f, 0.53f)));
                var boost = Label(hud.transform, "Boost", Vector2.right, new Vector2(-334, 28), new Vector2(300, 24), "SHIFT: BOOST READY");
                boost.alignment = TextAnchor.MiddleRight;
                Set(view, "boostText", boost);
                Set(view, "locationText", Label(hud.transform, "Location", Vector2.up, new Vector2(34, -50), new Vector2(250, 28), "POHORJE"));
                Validate(view);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("PLAYER_HUD_APPLIED: mockup HUD wired only in Player_Model_Test.");
            }
            finally
            {
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static void Validate(PlayerHud view)
        {
            var serialized = new SerializedObject(view);
            foreach (string name in new[] { "health", "battery", "movement", "healthText", "batteryText", "xpText",
                "boostText", "locationText", "healthFill", "batteryFill", "xpFill", "guiltEdges" })
                if (serialized.FindProperty(name).objectReferenceValue == null)
                    throw new InvalidOperationException("HUD reference missing: " + name);
            if (view.GetComponent<Canvas>().sortingOrder <= 0)
                throw new InvalidOperationException("The HUD must render above the darkness overlay.");
        }

        private static T[] FindAll<T>(Scene scene) where T : Component
        {
            var result = new System.Collections.Generic.List<T>();
            foreach (GameObject root in scene.GetRootGameObjects()) result.AddRange(root.GetComponentsInChildren<T>(true));
            return result.ToArray();
        }
        private static T Find<T>(Scene scene) where T : Component
        {
            var items = FindAll<T>(scene);
            return items.Length > 0 ? items[0] : null;
        }
        private static void Set(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static T Element<T>(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size) where T : Graphic
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(T));
            obj.transform.SetParent(parent, false);
            T graphic = obj.GetComponent<T>();
            graphic.raycastTarget = false;
            var rect = graphic.rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return graphic;
        }
        private static Text Label(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, string content)
        {
            var text = Element<Text>(parent, name, anchor, position, size);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.color = Paper;
            text.text = content;
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }
        private static Image Bar(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var border = Element<Image>(parent, name, anchor, position, size);
            border.color = new Color(0.015f, 0.015f, 0.015f, 0.95f);
            var background = Element<Image>(border.transform, "Track", Vector2.zero, Vector2.one * 2, size - Vector2.one * 4);
            background.color = new Color(0.12f, 0.13f, 0.13f, 0.95f);
            var fill = Element<Image>(background.transform, "Fill", Vector2.zero, Vector2.zero, size - Vector2.one * 4);
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.color = color;
            return fill;
        }
    }
}
