using System;
using Boki.Player;
using Boki.Routes;
using Boki.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Boki.Editor
{
    public static class PhoneStorySetup
    {
        [MenuItem("Boki/Story/Apply Phone Notifications")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Configure("V1_Pohorje", RouteLevel.Forest, "MK_Light_SafeLamp_0", new Vector3(-7.6f, -2f, 0f), new Vector3(28, 10, 8));
                Configure("V1_Maribor", RouteLevel.City, "MK_Exit_City", new Vector3(0, 0, -30), new Vector3(160, 8, 4));
                Configure("V1_FERI", RouteLevel.Feri, null, new Vector3(0, 1, 0), new Vector3(10, 5, 2));
                Configure("Player_Model_Test", RouteLevel.Forest, null, new Vector3(0, 1, 12), new Vector3(20, 5, 4));
            }
            finally
            {
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path)) EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            AssetDatabase.SaveAssets();
        }

        private static void Configure(string name, RouteLevel level, string markerName, Vector3 offset, Vector3 size)
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Single);
            var player = Find<PlayerHealth>(scene);
            if (player == null) throw new InvalidOperationException(name + " has no player.");
            Transform marker = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == markerName) marker = t;
            }
            if (markerName != null && marker == null) throw new InvalidOperationException("Missing marker " + markerName + " in " + name);
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Phone Story")
                {
                    var existing = root.GetComponentInChildren<PhoneStoryTrigger>(true);
                    var existingView = root.GetComponent<PhoneStoryView>();
                    if (existing == null || existingView == null) throw new InvalidOperationException("Phone Story root has no trigger or view.");
                    Vector3 position = (marker != null ? marker.position : player.transform.position) + offset;
                    var existingBox = existing.GetComponent<BoxCollider>();
                    bool changed = EnsureCard(existingView);
                    if (existing.transform.position != position || existingBox.size != size)
                    {
                        existing.transform.position = position;
                        existingBox.size = size;
                        changed = true;
                    }
                    if (changed) EditorSceneManager.SaveScene(scene);
                    Debug.Log("PHONE_STORY_PRESENT: " + name);
                    return;
                }
            var owner = new GameObject("Phone Story");
            var canvasObject = new GameObject("Notifications", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(owner.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 25;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            var panel = Ui("Notification Card", canvasObject.transform, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            var group = panel.GetComponent<CanvasGroup>();
            group.alpha = 0;
            group.blocksRaycasts = group.interactable = false;
            var label = Ui("Header", panel.transform, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.font = BuiltinFont();
            var audio = owner.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.volume = 0.35f;
            var view = owner.AddComponent<PhoneStoryView>();
            Ref(view, "card", group); Ref(view, "header", label); Ref(view, "source", audio);
            Ref(view, "notification", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sfx/intro_phone_beep.wav"));
            EnsureCard(view);
            var trigger = new GameObject("Safe Traversal Trigger", typeof(BoxCollider));
            trigger.transform.SetParent(owner.transform, false);
            trigger.transform.position = (marker != null ? marker.position : player.transform.position) + offset;
            var box = trigger.GetComponent<BoxCollider>();
            box.isTrigger = true; box.size = size;
            var beat = trigger.AddComponent<PhoneStoryTrigger>();
            Ref(beat, "player", player); Ref(beat, "view", view);
            var data = new SerializedObject(beat);
            data.FindProperty("level").enumValueIndex = (int)level;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PHONE_STORY_WIRED: " + name + " at " + trigger.transform.position);
        }

        // Structure only: PhoneStoryView owns geometry, colours and sprites, so a restyle needs no scene re-save.
        private static bool EnsureCard(PhoneStoryView view)
        {
            var data = new SerializedObject(view);
            var card = (CanvasGroup)data.FindProperty("card").objectReferenceValue;
            var header = (Text)data.FindProperty("header").objectReferenceValue;
            if (card == null || header == null) throw new InvalidOperationException("Phone Story view has no card or header.");
            bool changed = false;
            if (header.name != "Header") { header.name = "Header"; changed = true; }
            var icon = data.FindProperty("icon");
            if (icon.objectReferenceValue == null)
            {
                icon.objectReferenceValue = Ui("Icon", card.transform, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                changed = true;
            }
            var bubbles = data.FindProperty("bubbles");
            if (bubbles.arraySize == 0)
            {
                bubbles.arraySize = 2;
                for (int i = 0; i < 2; i++)
                {
                    var bubble = Ui("Bubble " + (i + 1), card.transform, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                    var text = Ui("Text", bubble.transform, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                    text.font = BuiltinFont();
                    var item = bubbles.GetArrayElementAtIndex(i);
                    item.FindPropertyRelative("group").objectReferenceValue = bubble.GetComponent<CanvasGroup>();
                    item.FindPropertyRelative("background").objectReferenceValue = bubble.GetComponent<Image>();
                    item.FindPropertyRelative("text").objectReferenceValue = text;
                }
                changed = true;
            }
            else
            {
                for (int i = 0; i < bubbles.arraySize; i++)
                {
                    var item = bubbles.GetArrayElementAtIndex(i);
                    if (item.FindPropertyRelative("group").objectReferenceValue == null
                        || item.FindPropertyRelative("background").objectReferenceValue == null
                        || item.FindPropertyRelative("text").objectReferenceValue == null)
                        throw new InvalidOperationException("Phone Story bubble " + i + " is incomplete.");
                }
            }
            if (changed) data.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static GameObject Ui(string name, Transform parent, params Type[] components)
        {
            var child = new GameObject(name, components);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static Font BuiltinFont() => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }
        private static void Ref(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            if (value == null) throw new InvalidOperationException("Missing " + property);
            var data = new SerializedObject(target);
            data.FindProperty(property).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
