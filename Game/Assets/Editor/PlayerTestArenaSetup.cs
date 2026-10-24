using System;
using System.Linq;
using Boki.Battery;
using Boki.Combat;
using Boki.Enemies;
using Boki.Player;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    public static class PlayerTestArenaSetup
    {
        private const string RootName = "Enemy Testing Arena";
        private static readonly string[] Labels =
        {
            "Forest shadow", "Izgubljeni meščan", "Student shadow", "Forest Watcher",
            "City Watcher", "Floating exam paper", "Guilt", "Professor"
        };
        private static readonly string[] Descriptions =
        {
            "FOREST SHADOW — Pursuit and contact damage. Jump to evade; kill and collect its gold XP pack.",
            "IZGUBLJENI MEŠČAN — City melee variant. Test contact, hit/death animation and XP collection.",
            "STUDENT SHADOW — FERI melee variant. Test pursuit, jumping over contact and XP collection.",
            "FOREST WATCHER — Slow red orbs. Switch laser OFF to observe, use cover, then kill for XP.",
            "CITY WATCHER — City ranged variant. Test orb trails, impact, line of sight and XP collection.",
            "FLOATING EXAM PAPER — FERI ranged reskin using the existing shooter AI and paper projectile.",
            "GUILT — Invulnerable. Stay 20 active seconds, look around for safe offscreen placement, then evade the chase.",
            "PROFESSOR — Exam Barrage and Deadline Slam. Switch laser OFF to observe; boost out of the red circle."
        };

        [MenuItem("Boki/Player Model/Build Enemy Testing Arena")]
        public static void Apply()
        {
            RequireClean();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BokiModelTestSetup.ScenePath) == null)
                throw new InvalidOperationException("Player_Model_Test is missing; refusing to replace it.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(BokiModelTestSetup.ScenePath, OpenSceneMode.Single);
                var player = Find<PlayerHealth>(scene).Single();
                GameObject old = scene.GetRootGameObjects().FirstOrDefault(r => r.name == RootName);
                if (old != null) Object.DestroyImmediate(old);
                // The existing player, rig, movement arena and scene GUID remain intact.
                var root = new GameObject(RootName);
                var templates = Child(root.transform, "Inactive Templates");
                templates.gameObject.SetActive(false);
                var arena = root.AddComponent<PlayerTestArena>();
                Material floor = MaterialAt("Assets/Effects/Boki/Floor.mat");
                Material wall = MaterialAt("Assets/Art/Battery/ChargeDepleted.mat");
                Material gold = MaterialAt("Assets/Effects/Boki/ArenaXP.mat");
                if (gold == null)
                {
                    gold = new Material(MaterialAt("Assets/Art/Battery/ChargeAvailable.mat")) { name = "ArenaXP" };
                    gold.SetColor("_BaseColor", new Color(1f, 0.7f, 0.08f));
                    gold.SetColor("_EmissionColor", new Color(0.7f, 0.35f, 0.02f));
                    AssetDatabase.CreateAsset(gold, "Assets/Effects/Boki/ArenaXP.mat");
                }
                Cube(root.transform, "Arena Floor", new Vector3(160f, -0.5f, 0f), new Vector3(196f, 1f, 104f), floor);
                Cube(root.transform, "West boundary", new Vector3(62f, 3f, 0f), new Vector3(1f, 6f, 104f), wall);
                Cube(root.transform, "East boundary", new Vector3(258f, 3f, 0f), new Vector3(1f, 6f, 104f), wall);
                var hub = Child(root.transform, "Safe Hub Spawn");
                hub.position = new Vector3(136f, 0.1f, 0f);
                hub.rotation = Quaternion.Euler(0f, 0f, 0f);
                Set(arena, "player", player);
                Set(arena, "movement", player.GetComponent<ScooterController>());
                Set(arena, "laser", player.GetComponent<AutoLaser>());
                Set(arena, "hub", hub);
                Set(arena, "hud", Find<Boki.UI.PlayerHud>(scene).Single());
                Set(arena, "followCamera", Find<ThirdPersonCamera>(scene).Single());
                Set(arena, "movementEntry", MakeMarker(root.transform, "Movement Arena Spawn", new Vector3(0f, 0.1f, -8f)));
                var pickupRoot = Child(root.transform, "XP Packs");
                var xp = CreateXp(templates, player, gold);
                Scene source = EditorSceneManager.OpenScene("Assets/Scenes/Enemy_Test.unity", OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var attackSource = source.GetRootGameObjects().Single(r => r.name == "Enemy Attack Templates");
                var attacks = Object.Instantiate(attackSource, templates, false);
                attacks.name = "Projectile Templates";
                var sourceMelee = Find<MeleeEnemy>(source).OrderBy(m => m.name).ToArray();
                var sourceRanged = Find<RangedEnemy>(source).OrderBy(m => m.name).ToArray();
                var sources = new[]
                {
                    sourceMelee.Single(m => m.name.Contains("Forest")).gameObject,
                    sourceMelee.Single(m => m.name.Contains("City")).gameObject,
                    sourceMelee.Single(m => m.name.Contains("Student")).gameObject,
                    sourceRanged.Single(m => m.name.Contains("Forest")).gameObject,
                    sourceRanged.Single(m => m.name.Contains("City")).gameObject,
                    sourceRanged.Single(m => m.name.Contains("Forest")).gameObject,
                    Find<GuiltChaser>(source).Single().gameObject,
                    Find<ProfessorBoss>(source).Single().gameObject
                };
                var stations = new EnemyTestStation[Labels.Length];
                var charges = Find<BatteryCharge>(scene).ToList();
                for (int i = 0; i < stations.Length; i++)
                {
                    float sign = i < 4 ? 1f : -1f;
                    Vector3 center = new Vector3(88f + i % 4 * 48f, 0f, sign * 31f);
                    var stationRoot = Child(root.transform, (i + 1) + " - " + Labels[i]);
                    stationRoot.position = center;
                    var station = stationRoot.gameObject.AddComponent<EnemyTestStation>();
                    stations[i] = station;
                    var volume = stationRoot.gameObject.AddComponent<BoxCollider>();
                    volume.isTrigger = true;
                    volume.center = new Vector3(0f, 3f, 0f);
                    volume.size = new Vector3(44f, 8f, 40f);
                    for (int side = -1; side <= 1; side += 2)
                        Cube(stationRoot, "Side wall " + side, center + new Vector3(side * 22f, 3f, 0f), new Vector3(1f, 6f, 41f), wall);
                    Cube(stationRoot, "Back wall", center + new Vector3(0f, 3f, sign * 20f), new Vector3(44f, 6f, 1f), wall);
                    for (int side = -1; side <= 1; side += 2)
                        Cube(stationRoot, "Entrance wall " + side, center + new Vector3(side * 13.25f, 3f, -sign * 20f), new Vector3(17.5f, 6f, 1f), wall);
                    Sign(stationRoot, (i + 1) + "  " + Labels[i], center + new Vector3(0f, 3.5f, -sign * 20.6f), sign < 0f ? 180f : 0f);
                    var entry = MakeMarker(stationRoot, "Player Entry", center + new Vector3(0f, 0.1f, -sign * 12f));
                    entry.rotation = Quaternion.Euler(0f, sign > 0f ? 0f : 180f, 0f);
                    GameObject enemy = Object.Instantiate(sources[i], templates);
                    enemy.name = Labels[i];
                    enemy.SetActive(false);
                    enemy.transform.position = center + new Vector3(0f, i >= 3 && i <= 5 ? 2f : 0.1f, sign * 6f);
                    enemy.transform.rotation = Quaternion.Euler(0f, sign > 0f ? 180f : 0f, 0f);
                    foreach (MonoBehaviour adapter in enemy.GetComponents<MonoBehaviour>())
                    {
                        var settings = new SerializedObject(adapter);
                        var target = settings.FindProperty("target");
                        if (target != null) target.objectReferenceValue = player;
                        settings.ApplyModifiedPropertiesWithoutUndo();
                    }
                    var ranged = enemy.GetComponent<RangedEnemy>();
                    if (ranged != null)
                        Set(ranged, "projectilePrefab", attacks.transform.Find(i == 5 ? "Proj_ExamPaper" : "Proj_WatcherOrb").GetComponent<EnemyProjectile>());
                    if (i == 5) ConfigureExamPaper(enemy);
                    var boss = enemy.GetComponent<ProfessorBoss>();
                    if (boss != null)
                    {
                        Set(boss, "sheetPrefab", attacks.transform.Find("Proj_ExamPaper").GetComponent<EnemyProjectile>());
                        SetBool(boss, "startOnEnable", true);
                    }
                    var guilt = enemy.GetComponent<GuiltChaser>();
                    if (guilt != null)
                    {
                        SetBool(guilt, "useRunSession", true);
                        var markers = new Transform[3];
                        markers[0] = MakeMarker(stationRoot, "Guilt Spawn Left", center + new Vector3(-16f, 0f, 12f));
                        markers[1] = MakeMarker(stationRoot, "Guilt Spawn Right", center + new Vector3(16f, 0f, 12f));
                        markers[2] = MakeMarker(stationRoot, "Guilt Spawn Back", center + new Vector3(0f, 0f, -15f));
                        SetArray(guilt, "spawnPoints", markers);
                    }
                    Set(station, "arena", arena); Set(station, "volume", volume); Set(station, "entry", entry);
                    Set(station, "enemyTemplate", enemy); Set(station, "xpTemplate", xp); Set(station, "pickupRoot", pickupRoot);
                    SetString(station, "description", Descriptions[i]);
                    ConfigureCharges(stationRoot, player, center, i, charges);
                    for (int n = 0; n < 2; n++)
                    {
                        var pack = Object.Instantiate(xp, pickupRoot);
                        pack.name = Labels[i] + " XP sample " + (n + 1);
                        pack.transform.position = center + new Vector3(n == 0 ? -5f : 5f, 0.65f, -sign * 8f);
                        pack.gameObject.SetActive(true);
                    }
                    // Cover is offset from the riding lane and every pickup remains accessible.
                    if (i >= 3 && i <= 5)
                        Cube(stationRoot, "Projectile Cover", center + new Vector3(-7f, 1.25f, 0f), new Vector3(4f, 2.5f, 1f), wall);
                }
                EditorSceneManager.CloseScene(source, true);
                SetArray(arena, "stations", stations);
                SetArray(player.GetComponent<BokiModelTestEffects>(), "charges", charges.ToArray());
                BuildPanel(root.transform, arena, stations);
                var instructions = Find<Text>(scene).FirstOrDefault(t => t.name == "Model Test Controls");
                if (instructions != null)
                    instructions.text = "ENEMY SANDBOX | WASD: ride | Space: jump | Shift: boost | Esc: use test panel\nH: take damage | B: drain 25 battery | Gold: XP | Green: battery +25\nOnly your current station is active. XP tests collection only; upgrade selection is not implemented.";
                player.transform.SetPositionAndRotation(hub.position, hub.rotation);
                PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
                // The sandbox is deliberately brighter than the atmospheric route scenes.
                RenderSettings.fogDensity = 0.006f;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.4f, 0.43f, 0.5f);
                var ambient = new UnityEngine.Rendering.SphericalHarmonicsL2();
                ambient.AddAmbientLight(RenderSettings.ambientLight);
                RenderSettings.ambientProbe = ambient;
                var darkness = Find<Boki.UI.ScooterDarkness>(scene).Single();
                Edit(darkness, "darknessOpacity", p => p.floatValue = 0.3f);
                Edit(darkness, "extraDarkness", p => p.floatValue = 0.03f);
                foreach (Light light in Find<Light>(scene))
                    if (light.type == LightType.Directional) light.intensity = 0.65f;
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("PLAYER_TEST_ARENA_READY: eight isolated stations, rigged Boki, contact XP, batteries and test controls.");
            }
            finally
            {
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path)) EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static void ConfigureExamPaper(GameObject enemy)
        {
            var watcher = enemy.GetComponent<WatcherView>();
            if (watcher != null) Object.DestroyImmediate(watcher);
            Object.DestroyImmediate(enemy.transform.Find("Visual").gameObject);
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Enemies/Export/PROP_ExamPaper.fbx"), enemy.transform);
            model.name = "Visual";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = MaterialAt("Assets/Art/Enemies/M_ExamPaper.mat");
            Set(enemy.GetComponent<RangedEnemy>(), "muzzle", enemy.transform);
        }

        private static void ConfigureCharges(Transform root, PlayerHealth player, Vector3 center, int index,
            System.Collections.Generic.List<BatteryCharge> charges)
        {
            var source = charges[0];
            var spaceRoot = Child(root, "Battery Combat Space");
            var volume = spaceRoot.gameObject.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.center = new Vector3(0f, 2f, 0f);
            volume.size = new Vector3(44f, 6f, 40f);
            var space = spaceRoot.gameObject.AddComponent<BatteryCombatSpace>();
            Set(space, "player", player); Set(space, "battery", player.GetComponent<LightBattery>());
            SetString(space, "spaceId", "enemy-station-" + index);
            for (int i = 0; i < 3; i++)
            {
                var charge = Object.Instantiate(source, root);
                charge.name = "Battery Charge " + i;
                charge.transform.position = center + new Vector3(i == 0 ? -13f : i == 1 ? 13f : 0f, 0.65f, i == 2 ? 12f : -4f);
                Set(charge, "space", space);
                SetInt(charge, "slot", i);
                charges.Add(charge);
            }
        }

        private static ArenaXpPickup CreateXp(Transform parent, PlayerHealth player, Material material)
        {
            var root = Child(parent, "Gold XP Pack");
            root.gameObject.layer = 2;
            var trigger = root.gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.85f;
            var pickup = root.gameObject.AddComponent<ArenaXpPickup>();
            Set(pickup, "player", player);
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.name = "Gold XP";
            mesh.transform.SetParent(root, false);
            mesh.transform.localScale = Vector3.one * 0.4f;
            mesh.transform.localRotation = Quaternion.Euler(30f, 45f, 30f);
            Object.DestroyImmediate(mesh.GetComponent<Collider>());
            mesh.GetComponent<Renderer>().sharedMaterial = material;
            return pickup;
        }

        private static void BuildPanel(Transform root, PlayerTestArena arena, EnemyTestStation[] stations)
        {
            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("Arena Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(root, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            var ui = new GameObject("Arena Test Panel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            ui.transform.SetParent(root, false);
            var canvas = ui.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = ui.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            Text status = Label(ui.transform, "Station Status", "SAFE HUB", new Vector2(24f, -200f), new Vector2(880f, 66f), 17);
            Set(arena, "status", status);
            Label(ui.transform, "Sandbox Controls",
                "ENEMY SANDBOX | WASD: ride | Mouse: look | Space: jump | Shift: boost\n"
                + "H: take damage | B: drain 25 battery | Gold: XP +1 | Green: battery +25\n"
                + "Choose a station, then click the world to ride. Only that enemy is active.\n"
                + "XP tests collection and HUD feedback; upgrade selection is not implemented.",
                new Vector2(24f, -60f), new Vector2(900f, 110f), 16);
            Label(ui.transform, "Panel Hint", "Esc: release mouse to use buttons", new Vector2(965f, -20f), new Vector2(300f, 35f), 16);
            for (int i = 0; i < stations.Length; i++)
                Button(ui.transform, (i + 1) + ". " + Labels[i], 58f + i * 36f, stations[i].Visit);
            Button(ui.transform, "Safe hub", 354f, arena.ReturnToHub);
            Button(ui.transform, "Reset enemy", 390f, arena.ResetEncounter);
            var laser = Button(ui.transform, "Laser: ON", 426f, arena.ToggleLaser);
            Set(arena, "laserLabel", laser.GetComponentInChildren<Text>());
            Button(ui.transform, "Restart sandbox (HP / XP / battery)", 462f, arena.RestartSandbox);
            Button(ui.transform, "Original movement area", 498f, arena.ReturnToMovement);
        }

        private static Button Button(Transform parent, string label, float y, UnityAction action)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-16f, -y);
            rect.sizeDelta = new Vector2(300f, 31f);
            go.GetComponent<Image>().color = new Color(0.035f, 0.055f, 0.085f, 0.96f);
            var text = Label(go.transform, "Label", label, Vector2.zero, rect.sizeDelta, 15);
            text.alignment = TextAnchor.MiddleCenter;
            var button = go.GetComponent<Button>();
            UnityEventTools.AddPersistentListener(button.onClick, action);
            return button;
        }

        private static Text Label(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = value;
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return text;
        }

        private static void Sign(Transform parent, string label, Vector3 position, float rotation)
        {
            var go = new GameObject(label, typeof(TextMesh));
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, rotation, 0f);
            var text = go.GetComponent<TextMesh>();
            text.text = label;
            text.characterSize = 0.15f;
            text.fontSize = 48;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.85f, 0.3f);
        }

        private static void Cube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Transform MakeMarker(Transform parent, string name, Vector3 position)
        {
            var marker = Child(parent, name);
            marker.position = position;
            return marker;
        }

        private static T[] Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        private static Material MaterialAt(string path) => AssetDatabase.LoadAssetAtPath<Material>(path);
        private static void Set(Object target, string field, Object value) => Edit(target, field, p => p.objectReferenceValue = value);
        private static void SetBool(Object target, string field, bool value) => Edit(target, field, p => p.boolValue = value);
        private static void SetInt(Object target, string field, int value) => Edit(target, field, p => p.intValue = value);
        private static void SetString(Object target, string field, string value) => Edit(target, field, p => p.stringValue = value);
        private static void SetArray(Object target, string field, Object[] values) => Edit(target, field, p =>
        {
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        });
        private static void Edit(Object target, string field, Action<SerializedProperty> edit)
        {
            var settings = new SerializedObject(target);
            var property = settings.FindProperty(field);
            if (property == null) throw new InvalidOperationException(target.name + " is missing " + field);
            edit(property);
            settings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
        private static void RequireClean()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes first.");
        }
    }
}
