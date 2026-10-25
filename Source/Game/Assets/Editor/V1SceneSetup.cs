using System;
using System.Collections.Generic;
using System.IO;
using Boki.Battery;
using Boki.Combat;
using Boki.Core;
using Boki.Enemies;
using Boki.Player;
using Boki.Upgrades;
using Boki.Routes;
using Boki.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    public static class V1SceneSetup
    {
        public static readonly string[] Scenes = { "Assets/Scenes/V1_Pohorje.unity", "Assets/Scenes/V1_Maribor.unity", "Assets/Scenes/V1_FERI.unity" };
        private const string Folder = "Assets/V1";
        private sealed class Marker
        {
            public string Name, Kind, Owner;
            public Vector3 Position, Size, Forward = Vector3.forward;
        }
        [Serializable] private sealed class FeriMarker { public string name, type, owner; public float[] position, size; }
        [Serializable] private sealed class FeriMarkers { public FeriMarker[] markers; }
        private static Material gateMaterial, portalMaterial, chargeMaterial, emptyMaterial;
        private static InputActionReference pauseAction, interactAction;
        private static InputActionReference upgradeNavigate, upgradeSubmit;
        private static readonly InputActionReference[] upgradeChoices = new InputActionReference[3];

        [MenuItem("Boki/Version 1/Create Missing Game Scenes")]
        public static void Build()
        {
            RequireCleanEditor();
            bool hasMissingScene = false;
            foreach (string path in Scenes) hasMissingScene |= !File.Exists(path);
            if (!hasMissingScene)
            {
                Debug.Log("V1_SCENES_PRESERVED: All V1 scenes already exist. Use a targeted upgrade for existing scenes.");
                return;
            }
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "V1");
                Materials(); Controls();
                var entries = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                for (int i = 0; i < Scenes.Length; i++)
                {
                    if (!CreateMissingLevel((RouteLevel)i, Scenes[i])) continue;
                    if (!entries.Exists(entry => entry.path == Scenes[i]))
                        entries.Add(new EditorBuildSettingsScene(Scenes[i], true));
                }
                EditorBuildSettings.scenes = entries.ToArray();
                AssetDatabase.SaveAssets();
                Debug.Log("V1_SCENES_READY: Pohorje -> Maribor -> FERI -> professor; routes, HUD, XP/upgrades, battery, Guilt, death/retry and portals.");
            }
            finally
            {
                bool restore = previous.Length > 0;
                foreach (var item in previous) restore &= !string.IsNullOrEmpty(item.path);
                if (restore) EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [MenuItem("Boki/Version 1/Upgrade Enemy Effects")]
        public static void UpgradeEnemyEffects()
        {
            RequireCleanEditor();
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string path in Scenes)
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                        throw new InvalidOperationException("Missing V1 scene: " + path);
                foreach (string path in Scenes)
                {
                    Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    EnemyPrefabBuilder.UpgradeAttacks(scene);
                    V1SceneChecks.ValidateEnemyEffects(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("V1_ENEMY_EFFECTS_SAVED " + path);
                }
            }
            finally
            {
                bool restore = previous.Length > 0;
                foreach (var item in previous) restore &= !string.IsNullOrEmpty(item.path);
                if (restore) EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [MenuItem("Boki/Version 1/Open Pohorje")]
        public static void Open()
        {
            RequireCleanEditor();
            EditorSceneManager.OpenScene(Scenes[0], OpenSceneMode.Single);
        }

        internal static void RequireCleanEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes before V1 setup.");
        }

        [MenuItem("Boki/Version 1/Run Scene Preservation Checks")]
        public static void RunPreservationChecks()
        {
            RequireCleanEditor();
            var previous = EditorSceneManager.GetSceneManagerSetup();
            string fixturePath = "Assets/V1PreservationCheck-" + Guid.NewGuid().ToString("N") + ".unity";
            try
            {
                foreach (string path in Scenes)
                    if (!File.Exists(path)) throw new InvalidOperationException("Preservation check requires existing V1 scenes: " + path);
                var files = new Dictionary<string, string>();
                foreach (string path in Scenes) files.Add(path, File.ReadAllText(path));
                const string buildSettings = "ProjectSettings/EditorBuildSettings.asset";
                files.Add(buildSettings, File.ReadAllText(buildSettings));
                foreach (string path in Directory.GetFiles(Folder, "*", SearchOption.AllDirectories))
                    files.Add(path, File.ReadAllText(path));
                Build();
                Build();
                foreach (var file in files)
                    if (File.ReadAllText(file.Key) != file.Value)
                        throw new InvalidOperationException("Setup changed existing content: " + file.Key);

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var prop = new GameObject("Authored Prop", typeof(BoxCollider));
                var audio = new GameObject("Authored Audio", typeof(AudioSource));
                var generatedName = new GameObject("V1 Gameplay");
                var nested = new GameObject("Authored Child");
                nested.transform.SetParent(generatedName.transform);
                var mapName = new GameObject("FERI_Map");
                prop.transform.position = new Vector3(3f, 4f, 5f);
                if (!EditorSceneManager.SaveScene(scene, fixturePath))
                    throw new InvalidOperationException("Could not save preservation fixture.");
                string saved = File.ReadAllText(fixturePath);
                for (int i = 0; i < 2; i++)
                    if (CreateMissingLevel(RouteLevel.Forest, fixturePath))
                        throw new InvalidOperationException("Existing scene was treated as a new scene.");
                if (prop == null || audio == null || generatedName == null || nested == null || mapName == null
                    || nested.transform.parent != generatedName.transform || scene.isDirty
                    || prop.transform.position != new Vector3(3f, 4f, 5f) || File.ReadAllText(fixturePath) != saved)
                    throw new InvalidOperationException("Existing authored objects were modified.");

                new GameObject("Unsaved Authored Object");
                EditorSceneManager.MarkSceneDirty(scene);
                bool refusedDirty = false;
                try { Build(); }
                catch (InvalidOperationException) { refusedDirty = true; }
                if (!refusedDirty) throw new InvalidOperationException("Setup accepted a dirty scene.");
                Debug.Log("V1_SCENE_PRESERVATION_PASSED");
            }
            finally
            {
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(fixturePath);
            }
        }

        private static bool CreateMissingLevel(RouteLevel level, string path)
        {
            // Names and component types cannot establish ownership of existing scene content.
            // Check the file as well as the AssetDatabase so an unimported scene is protected.
            if (File.Exists(path) || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null) return false;
            if (!AssetDatabase.CopyAsset("Assets/Scenes/Player_Model_Test.unity", path))
                throw new InvalidOperationException("Could not create V1 from the existing player model scene.");
            InitializeNewLevel(level, path);
            return true;
        }

        // Template cleanup is only valid on the fresh copy made by CreateMissingLevel.
        private static void InitializeNewLevel(RouteLevel level, string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var player = Find<ScooterController>(scene);
            var health = player.GetComponent<PlayerHealth>();
            var camera = Find<ThirdPersonCamera>(scene);
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "V1 Gameplay" || root.GetComponent<RunSession>() != null || root.name == "Pohorje_Production" || root.name == "Maribor_Production" || root.name == "FERI_Map")
                    Object.DestroyImmediate(root);
                else if (root.GetComponentInChildren<ScooterController>(true) == null
                    && root.GetComponentInChildren<Camera>(true) == null && root.GetComponent<Light>() == null
                    && root.GetComponentInChildren<Canvas>(true) == null && root.GetComponent<EventSystem>() == null
                    && root.GetComponent<ParticleSystem>() == null) Object.DestroyImmediate(root);
            }
            foreach (var test in All<BokiModelTestEffects>(scene)) Object.DestroyImmediate(test);
            foreach (var old in All<EnemyCombatPreview>(scene)) Object.DestroyImmediate(old);
            foreach (var old in All<PohorjeHudPreview>(scene)) Object.DestroyImmediate(old.gameObject);
            foreach (var old in All<CombatSession>(scene)) Object.DestroyImmediate(old);
            foreach (var old in All<PlayerGameplayFeedback>(scene)) Object.DestroyImmediate(old);
            foreach (var old in All<PlayerTestArena>(scene)) Object.DestroyImmediate(old);
            foreach (var old in All<UpgradeManager>(scene)) Object.DestroyImmediate(old);
            foreach (var old in All<UpgradeScreen>(scene)) Object.DestroyImmediate(old.gameObject);
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "MovementHUD" || root.name == "Model Test Controls") root.SetActive(false);
            var runtime = new GameObject("V1 Gameplay");
            var session = new GameObject("V1 Run Session").AddComponent<RunSession>();
            String(session, "forestDestination", Scenes[0]); String(session, "cityDestination", Scenes[1]); String(session, "feriDestination", Scenes[2]);
            Bool(session, "persistentProgress", true); Bool(health, "useRunSession", true);
            var markers = new List<Marker>();
            var map = Environment(level, scene, markers);
            Physics.SyncTransforms();
            Marker start = markers.Find(m => m.Kind == "player_start");
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.SetPositionAndRotation(start.Position + Vector3.up * 0.12f, Quaternion.identity);
            controller.enabled = true;
            camera.transform.SetPositionAndRotation(player.transform.position + new Vector3(0, 2.6f, -5), Quaternion.Euler(15, 0, 0));
            var battery = player.GetComponent<LightBattery>();
            var laser = player.GetComponent<AutoLaser>();
            if (battery == null || laser == null) throw new InvalidOperationException("Player_Model_Test must have its current light and laser setup.");
            Ref(battery, "player", health);
            ScooterLightSetup.ConfigureWideWhiteLight(scene);
            var hud = Find<PlayerHud>(scene);
            if (hud == null) throw new InvalidOperationException("Player_Model_Test must have Ismar's HUD.");
            Ref(hud, "health", health); Ref(hud, "movement", player); Ref(hud, "battery", battery);
            var boostLabel = new SerializedObject(hud).FindProperty("boostText").objectReferenceValue as Text;
            if (boostLabel != null) boostLabel.gameObject.SetActive(false);
            var ui = CreateUI(runtime.transform, health, player, camera, laser, level);
            var manager = Child(runtime.transform, "Route Manager").AddComponent<RouteManager>();
            Int(manager, "level", (int)level); Ref(manager, "player", health); Ref(manager, "status", ui.Status);
            Bool(manager, "playerFacingStatus", true);
            var guilt = CreateGuilt(runtime.transform, health, markers);
            Ref(hud, "guilt", guilt); Ref(ui.Flow, "guilt", guilt); Ref(player, "guilt", guilt);
            var feedback = player.gameObject.AddComponent<PlayerGameplayFeedback>();
            Ref(feedback, "movement", player); Ref(feedback, "health", health); Ref(feedback, "effects", player.GetComponent<BokiPlayerEffects>());
            Ref(feedback, "battery", battery); Ref(feedback, "presentation", player.GetComponent<ScooterPresentation>());
            var charges = new List<Object>();
            var branches = new List<GameObject>();
            for (int choice = 1; choice <= 3; choice++)
            {
                string owner = ((RouteChoice)choice).ToString();
                var branch = Child(runtime.transform, "Route " + owner + " Encounters");
                branches.Add(branch);
                var occupancy = markers.Find(m => m.Kind == "combat_occupancy" && m.Owner == owner);
                var space = CreateSpace(branch.transform, occupancy, battery, health, guilt, markers, charges, level == RouteLevel.City && choice == 2 ? 4 : 3);
                CreateEncounter(branch.transform, level, owner, space, health, camera.GetComponent<Camera>(), markers);
                ConfigureGate(map, runtime.transform, markers, manager, (RouteChoice)choice, branch, level);
                branch.SetActive(false);
            }
            if (level != RouteLevel.Feri)
            {
                var exit = markers.Find(m => m.Kind == "level_exit");
                CreatePortal(runtime.transform, Ground(exit.Position), manager, level == RouteLevel.Forest ? "MARIBOR" : "FERI");
                if (level == RouteLevel.City) CreateCityEscape(runtime.transform, markers, manager, ui.Flow);
            }
            else CreateBoss(runtime.transform, markers, manager, health, battery, guilt, ui, branches.ToArray(), charges);
            EnemyPrefabBuilder.UpgradeAttacks(scene);
            foreach (var root in scene.GetRootGameObjects()) if (root.name == "Enemy Attack Templates") root.transform.SetParent(runtime.transform, true);
            ArrayRef(feedback, "charges", charges.ToArray());
            EnsureEventSystem(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("V1_LEVEL_SAVED " + path);
        }

        private static GameObject Environment(RouteLevel level, Scene destination, List<Marker> markers)
        {
            GameObject map;
            if (level != RouteLevel.Feri)
            {
                string name = level == RouteLevel.Forest ? "Pohorje" : "Maribor";
                string art = "Assets/Art/" + name + "_v3/";
                map = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(art + name + "_Production.prefab"), destination);
                var manifest = JsonUtility.FromJson<MapProductionTools.Manifest>(File.ReadAllText(art + name + "_Manifest.json"));
                foreach (var m in manifest.markers)
                    markers.Add(new Marker { Name = m.name, Kind = m.kind, Owner = m.owner, Position = MapProductionTools.Vector(m.position),
                        Size = MapProductionTools.Vector(m.size), Forward = MapProductionTools.Vector(m.forward) });
                foreach (var m in markers)
                {
                    if (m.Kind != "light_violet" && m.Kind != "light_warm") continue;
                    var lamp = map.transform.Find("Markers/" + m.Name).gameObject.AddComponent<Light>();
                    lamp.type = LightType.Point; lamp.range = Mathf.Min(m.Size.x, 12); lamp.intensity = 2f;
                    lamp.color = m.Kind == "light_violet" ? new Color(0.6f, 0.2f, 1f) : new Color(1f, 0.45f, 0.16f);
                    lamp.shadows = LightShadows.None;
                }
            }
            else
            {
                var source = EditorSceneManager.OpenScene("Assets/Scenes/FERI_Map_Test.unity", OpenSceneMode.Additive);
                GameObject authored = null;
                foreach (var root in source.GetRootGameObjects()) if (root.name == "FERI_Map") authored = root;
                if (authored == null) throw new InvalidOperationException("FERI_Map_Test is missing its authored map root.");
                map = Object.Instantiate(authored); map.name = "FERI_Map";
                SceneManager.MoveGameObjectToScene(map, destination);
                EditorSceneManager.CloseScene(source, true); SceneManager.SetActiveScene(destination);
                var list = JsonUtility.FromJson<FeriMarkers>(File.ReadAllText("Assets/Art/FERI/markers.json"));
                foreach (var m in list.markers)
                {
                    string kind = m.type == "Start" ? "player_start" : m.type == "Gate" ? "gate_front" : m.type == "Trigger" ? "route_selection"
                        : m.type == "Occupancy" ? "combat_occupancy" : m.type == "Charge" ? "battery_charge" : m.type == "Spawn" ? "enemy_spawn"
                        : m.type == "Guilt" ? "guilt_anchor" : m.name.Contains("_Rear") ? "gate_rear" : m.type;
                    string owner = m.owner.StartsWith("Route_") ? m.owner.Substring(6) : "Shared";
                    markers.Add(new Marker { Name = m.name, Kind = kind, Owner = owner, Position = FeriMapTestBuilder.Unity(m.position),
                        Size = m.size == null ? Vector3.zero : FeriMapTestBuilder.Unity(m.size) });
                }
            }
            ScooterLightSetup.ConfigureLighting(destination);
            return map;
        }

        private static void ConfigureGate(GameObject map, Transform parent, List<Marker> markers, RouteManager manager, RouteChoice choice, GameObject encounter, RouteLevel level)
        {
            string owner = choice.ToString();
            var front = markers.Find(m => m.Kind == "gate_front" && m.Owner == owner);
            var rear = markers.Find(m => m.Kind == "gate_rear" && m.Owner == owner);
            var select = markers.Find(m => m.Kind == "route_selection" && m.Owner == owner);
            var gateObject = Trigger(parent, "Route " + owner + " Selection", select.Position, select.Size);
            gateObject.transform.rotation = Quaternion.LookRotation(select.Forward);
            Collider frontBlocker = null, rearBlocker = null;
            Renderer frontVisual = null, rearVisual = null;
            if (level != RouteLevel.Feri)
            {
                foreach (var collider in map.GetComponentsInChildren<Collider>(true))
                {
                    if (collider.name.Contains("Seal_" + owner + "_Front")) frontBlocker = collider;
                    if (collider.name.Contains("Seal_" + owner + "_Rear")) rearBlocker = collider;
                }
                foreach (var visual in map.GetComponentsInChildren<Renderer>(true))
                {
                    if (visual.name == "PL_Seal_" + owner + "_Front") frontVisual = visual;
                    if (visual.name == "PL_Seal_" + owner + "_Rear") rearVisual = visual;
                }
                map.transform.Find("Seals").gameObject.SetActive(true);
            }
            if (frontBlocker == null) frontBlocker = Cube(parent, "Front Seal " + owner, front.Position + Vector3.up * 2.5f, new Vector3(front.Size.x, 5f, 0.5f), gateMaterial).GetComponent<Collider>();
            if (rearBlocker == null) rearBlocker = Cube(parent, "Rear Seal " + owner, rear.Position + Vector3.up * 2.5f, new Vector3(rear.Size.x, 5f, 0.5f), gateMaterial).GetComponent<Collider>();
            if (frontVisual == null) frontVisual = frontBlocker.GetComponent<Renderer>();
            if (rearVisual == null) rearVisual = rearBlocker.GetComponent<Renderer>();
            if (frontVisual != null) frontVisual.gameObject.SetActive(true);
            if (rearVisual != null) rearVisual.gameObject.SetActive(true);
            var sign = Sign(parent, "Route " + owner + " Sign", front.Position + Vector3.up * 3.2f - front.Forward * 1.5f, "", 0.12f);
            sign.transform.rotation = Quaternion.LookRotation(front.Forward);
            var gate = gateObject.AddComponent<RouteGate>();
            Ref(gate, "manager", manager); Int(gate, "choice", (int)choice); Ref(gate, "selectionTrigger", gateObject.GetComponent<Collider>());
            Ref(gate, "blocker", frontBlocker); Ref(gate, "barrierVisual", frontVisual); Ref(gate, "exitBlocker", rearBlocker);
            Ref(gate, "exitVisual", rearVisual); Ref(gate, "encounterRoot", encounter); Ref(gate, "sign", sign);
            Transform interior = level != RouteLevel.Feri ? map.transform.Find("Route_" + owner + "/InteriorRender") : map.transform.Find("FERI_Route_" + owner);
            Ref(gate, "interiorRoot", interior != null ? interior.gameObject : null);
        }

        private static GuiltChaser CreateGuilt(Transform parent, PlayerHealth health, List<Marker> markers)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Guilt.prefab"));
            instance.transform.SetParent(parent, false); instance.name = "Ambient Guilt";
            var guilt = instance.GetComponent<GuiltChaser>();
            Ref(guilt, "target", health); Bool(guilt, "useRunSession", true);
            var anchors = new List<Object>();
            foreach (var marker in markers) if (marker.Kind == "guilt_anchor")
            {
                var point = Child(parent, marker.Name); point.transform.position = marker.Position + Vector3.up * 0.05f;
                anchors.Add(point.transform);
            }
            ArrayRef(guilt, "spawnPoints", anchors.ToArray());
            Int(guilt, "obstructionMask", 1);
            return guilt;
        }

        private static BatteryCombatSpace CreateSpace(Transform parent, Marker occupancy, LightBattery battery, PlayerHealth player,
            GuiltChaser guilt, List<Marker> markers, List<Object> allCharges, int count)
        {
            var volume = Trigger(parent, occupancy.Name, occupancy.Position, occupancy.Size);
            var space = volume.AddComponent<BatteryCombatSpace>();
            Ref(space, "battery", battery); Ref(space, "player", player); String(space, "spaceId", occupancy.Name); Int(space, "chargeCount", count);
            if (guilt != null)
            {
                var pressure = volume.AddComponent<GuiltCombatSpace>(); Ref(pressure, "guilt", guilt); Ref(pressure, "player", player);
            }
            var positions = markers.FindAll(m => m.Kind == "battery_charge" && m.Owner == occupancy.Owner);
            for (int i = 0; i < count; i++)
            {
                Vector3 position = positions[i].Position + Vector3.up * 0.65f;
                var item = Child(parent, "Battery Charge " + i); item.transform.position = position;
                var sphere = item.AddComponent<SphereCollider>(); sphere.isTrigger = true; sphere.radius = 0.85f;
                var charged = Sphere(item.transform, "Charged", Vector3.zero, 0.6f, chargeMaterial);
                var depleted = Sphere(item.transform, "Depleted", Vector3.zero, 0.3f, emptyMaterial); depleted.SetActive(false);
                var charge = item.AddComponent<BatteryCharge>(); Ref(charge, "space", space); Ref(charge, "player", player);
                Int(charge, "slot", i); Ref(charge, "chargedVisual", charged); Ref(charge, "depletedVisual", depleted);
                allCharges.Add(charge);
            }
            return space;
        }

        private static void CreateEncounter(Transform parent, RouteLevel level, string owner, BatteryCombatSpace space, PlayerHealth player, Camera camera, List<Marker> markers)
        {
            string melee = level == RouteLevel.Forest ? "Melee_Forest" : level == RouteLevel.City ? "Melee_City" : "Melee_Student";
            string ranged = level == RouteLevel.City ? "Watcher_City" : "Watcher_Forest";
            var templates = new List<Object>();
            for (int i = 0; i < 3; i++)
            {
                var template = EnemyTemplate(parent, i == 2 ? ranged : melee, player, Vector3.zero, true);
                if (level == RouteLevel.Feri && i == 2)
                {
                    template.name = "ExamPaper Student";
                    Object.DestroyImmediate(template.GetComponent<WatcherView>());
                    foreach (var renderer in template.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                    var paper = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Enemies/Export/PROP_ExamPaper.fbx"));
                    paper.transform.SetParent(template.transform, false);
                    foreach (var renderer in paper.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Enemies/M_ExamPaper.mat");
                    Ref(template.GetComponent<RangedEnemy>(), "projectilePrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Proj_ExamPaper.prefab").GetComponent<EnemyProjectile>());
                }
                templates.Add(template);
            }
            var points = new List<Object>();
            foreach (var marker in markers) if (marker.Kind == "enemy_spawn" && marker.Owner == owner)
            {
                var point = Child(parent, marker.Name); point.transform.position = marker.Position; points.Add(point.transform);
            }
            var encounter = parent.gameObject.AddComponent<RouteEncounter>(); Ref(encounter, "space", space); Ref(encounter, "player", player); Ref(encounter, "viewCamera", camera);
            ArrayRef(encounter, "templates", templates.ToArray()); ArrayRef(encounter, "spawnPoints", points.ToArray());
            Int(encounter, "initialCount", 15);
            Float(encounter, "cadence", level == RouteLevel.City ? 3f : 4f);
        }

        private static GameObject EnemyTemplate(Transform parent, string name, PlayerHealth player, Vector3 position, bool reward)
        {
            var template = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + name + ".prefab"));
            template.transform.SetParent(parent, false); template.transform.position = position;
            if (template.TryGetComponent<MeleeEnemy>(out var melee)) Ref(melee, "target", player);
            if (template.TryGetComponent<RangedEnemy>(out var ranged)) Ref(ranged, "target", player);
            template.SetActive(false);
            return template;
        }

        private static void CreatePortal(Transform parent, Vector3 position, RouteManager manager, string destination)
        {
            var portal = Trigger(parent, "Portal to " + destination, position, new Vector3(4f, 4f, 2f));
            portal.GetComponent<BoxCollider>().center = Vector3.up * 2f;
            var line = portal.AddComponent<LineRenderer>(); line.sharedMaterial = portalMaterial; line.useWorldSpace = false;
            line.loop = true; line.widthMultiplier = 0.14f; line.positionCount = 48; line.shadowCastingMode = ShadowCastingMode.Off;
            for (int i = 0; i < 48; i++)
            {
                float angle = i * Mathf.PI * 2f / 48;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * 2f, 2.2f + Mathf.Sin(angle) * 2.2f, 0));
            }
            var label = Sign(portal.transform, "Portal Label", position + Vector3.up * 5f, "PORTAL TO " + destination, 0.15f);
            var travel = portal.AddComponent<LevelPortal>(); Ref(travel, "manager", manager); Ref(travel, "label", label); String(travel, "destinationLabel", destination);
        }

        private sealed class UIReferences { public V1GameFlow Flow; public Text Status, Boss; }
        private static UIReferences CreateUI(Transform parent, PlayerHealth health, ScooterController movement, ThirdPersonCamera camera, AutoLaser laser, RouteLevel level)
        {
            var canvas = Child(parent, "V1 Run UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; canvas.GetComponent<Canvas>().sortingOrder = 50;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720);
            var status = Label(canvas.transform, "Route Status", new Vector2(0.5f, 1), new Vector2(0, -35), new Vector2(1000, 55), "Choose A or B. C routes unlock on later runs.", 16);
            var objective = Label(canvas.transform, "Objective", new Vector2(0.5f, 1), new Vector2(0, -95), new Vector2(1000, 40), "Choose a route, collect XP and battery charges, then " + (level == RouteLevel.Feri ? "reach the classroom." : "find the portal."), 16);
            var boss = Label(canvas.transform, "Boss Health", new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(800, 35), "", 22);
            var panel = Child(canvas.transform, "Pause and Death", typeof(RectTransform), typeof(Image));
            var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.015f, 0.02f, 0.025f, 0.94f);
            var title = Label(panel.transform, "Title", Vector2.one * 0.5f, new Vector2(0, 125), new Vector2(1000, 90), "ABSENT", 58);
            var details = Label(panel.transform, "Details", Vector2.one * 0.5f, Vector2.zero, new Vector2(900, 130), "", 22);
            var resume = Button(panel.transform, "Resume", new Vector2(-170, -150));
            var retry = Button(panel.transform, "Retry / New Run", new Vector2(170, -150));
            var upgradeObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/Upgrades/UpgradeScreen.prefab"));
            upgradeObject.transform.SetParent(parent, false);
            var upgradeCanvas = upgradeObject.GetComponent<Canvas>(); if (upgradeCanvas != null) upgradeCanvas.sortingOrder = 100;
            var upgradeScreen = upgradeObject.GetComponentInChildren<UpgradeScreen>(true);
            Ref(upgradeScreen, "navigate", upgradeNavigate); Ref(upgradeScreen, "submit", upgradeSubmit);
            ArrayRef(upgradeScreen, "quickSelect", upgradeChoices);
            var flow = canvas.AddComponent<V1GameFlow>();
            Ref(flow, "health", health); Ref(flow, "movement", movement); Ref(flow, "followCamera", camera); Ref(flow, "laser", laser);
            Ref(flow, "panel", panel); Ref(flow, "title", title); Ref(flow, "details", details); Ref(flow, "objective", objective);
            Ref(flow, "resume", resume); Ref(flow, "retry", retry); Ref(flow, "upgrades", upgradeScreen); Ref(flow, "pauseAction", pauseAction);
            V1TryAgainSetup.Restyle(new SerializedObject(flow));
            panel.SetActive(false);
            return new UIReferences { Flow = flow, Status = status, Boss = boss };
        }

        private static void CreateCityEscape(Transform parent, List<Marker> markers, RouteManager manager, V1GameFlow flow)
        {
            var reveal = markers.Find(m => m.Kind == "event_guilt_reveal" && m.Owner == "C");
            var side = markers.Find(m => m.Kind == "event_side_escape");
            var wall = Cube(parent, "Podzemni prehod Turn Back", reveal.Position + new Vector3(0, 2.5f, 3f), new Vector3(5, 5, 0.6f), gateMaterial);
            var shadow = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Guilt.prefab"));
            shadow.transform.SetParent(wall.transform, true); shadow.transform.position = reveal.Position + Vector3.forward;
            Object.DestroyImmediate(shadow.GetComponent<GuiltView>());
            Object.DestroyImmediate(shadow.GetComponent<GuiltChaser>()); Object.DestroyImmediate(shadow.GetComponent<CharacterController>());
            wall.SetActive(false);
            RouteEvent(parent, reveal.Position + Vector3.up, new Vector3(5, 4, 4), V1RouteEventKind.CityReveal, manager, flow, wall);
            RouteEvent(parent, side.Position + Vector3.up, new Vector3(4, 4, 4), V1RouteEventKind.CitySideDoor, manager, flow, wall);
            Sign(parent, "Side Door Sign", side.Position + Vector3.up * 2.5f, "SIDE DOOR →", 0.1f);
        }

        private static void CreateBoss(Transform parent, List<Marker> markers, RouteManager manager, PlayerHealth health,
            LightBattery battery, GuiltChaser guilt, UIReferences ui, GameObject[] branches, List<Object> charges)
        {
            var entry = markers.Find(m => m.Name == "Event_ArenaEntry");
            var spawn = markers.Find(m => m.Name == "Spawn_Professor");
            var professor = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Professor.prefab"));
            professor.transform.SetParent(parent, false); professor.transform.position = spawn.Position;
            var boss = professor.GetComponent<ProfessorBoss>(); Ref(boss, "target", health); Bool(boss, "startOnEnable", false);
            professor.SetActive(false);
            var barrier = Cube(parent, "Classroom Entrance Seal", new Vector3(0, 2.5f, 75), new Vector3(4, 5, 0.5f), gateMaterial); barrier.SetActive(false);
            var arena = Trigger(parent, "Professor Arena Entry", entry.Position + Vector3.up, new Vector3(4, 4, 2));
            var component = arena.AddComponent<V1BossArena>(); Ref(component, "manager", manager); Ref(component, "flow", ui.Flow);
            Ref(component, "professor", boss); Ref(component, "bossHealth", professor.GetComponent<EnemyHealth>()); Ref(component, "entranceSeal", barrier);
            Ref(component, "bossStatus", ui.Boss); ArrayRef(component, "approachEncounters", branches);
            var adds = new Object[2];
            for (int i = 0; i < 2; i++) adds[i] = EnemyTemplate(parent, "Melee_Student", health, new Vector3(i == 0 ? -8 : 8, 0.05f, 92), true);
            ArrayRef(component, "reinforcementTemplates", adds);
            var room = markers.Find(m => m.Name == "Occupancy_Classroom");
            var roomMarkers = new List<Marker>();
            foreach (var m in markers) if (m.Name.StartsWith("Charge_Classroom_")) roomMarkers.Add(m);
            room.Owner = "Classroom";
            foreach (var marker in roomMarkers) marker.Owner = "Classroom";
            CreateSpace(parent, room, battery, health, null, roomMarkers, charges, 2);
            var power = markers.Find(m => m.Name == "Event_PowerOffInteract");
            RouteEvent(parent, power.Position + Vector3.up, new Vector3(3, 3, 3), V1RouteEventKind.PowerOff, manager, ui.Flow, null);
            Sign(parent, "POWER OFF panel", power.Position + Vector3.up * 2f, "POWER OFF\nPress E", 0.1f);
        }

        private static void RouteEvent(Transform parent, Vector3 position, Vector3 size, V1RouteEventKind kind, RouteManager manager, V1GameFlow flow, GameObject reveal)
        {
            var obj = Trigger(parent, kind.ToString(), position, size);
            var component = obj.AddComponent<V1RouteEvent>(); Ref(component, "manager", manager); Ref(component, "flow", flow);
            Int(component, "kind", (int)kind); Ref(component, "interact", interactAction); Ref(component, "reveal", reveal);
        }

        private static void Materials()
        {
            gateMaterial = Material("Gate", new Color(0.025f, 0.035f, 0.045f));
            portalMaterial = Material("Portal", new Color(0.2f, 1f, 0.85f));
            chargeMaterial = Material("Battery Available", new Color(0.3f, 1f, 0.25f));
            emptyMaterial = Material("Battery Depleted", new Color(0.2f, 0.25f, 0.2f));
        }
        private static Material Material(string name, Color color)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color); return material;
        }
        private static void Controls()
        {
            string path = Folder + "/Controls.asset";
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<InputActionAsset>();
                var map = asset.AddActionMap("V1"); map.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape"); map.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
                AssetDatabase.CreateAsset(asset, path);
            }
            var actions = asset.FindActionMap("V1");
            if (actions.FindAction("UpgradeNavigate") == null)
            {
                var navigate = actions.AddAction("UpgradeNavigate", InputActionType.Value);
                navigate.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
                navigate.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
                actions.AddAction("UpgradeSubmit", InputActionType.Button, "<Keyboard>/enter");
                for (int i = 1; i <= 3; i++) actions.AddAction("UpgradeChoice" + i, InputActionType.Button, "<Keyboard>/" + i);
            }
            var references = new List<InputActionReference>();
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path)) if (obj is InputActionReference reference) references.Add(reference);
            foreach (var action in actions.actions)
            {
                if (references.Exists(r => r.action == action)) continue;
                var reference = InputActionReference.Create(action); reference.name = action.name; AssetDatabase.AddObjectToAsset(reference, asset);
            }
            EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets();
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path)) if (obj is InputActionReference reference)
            {
                if (reference.action.name == "Pause") pauseAction = reference;
                if (reference.action.name == "Interact") interactAction = reference;
                if (reference.action.name == "UpgradeNavigate") upgradeNavigate = reference;
                if (reference.action.name == "UpgradeSubmit") upgradeSubmit = reference;
                for (int i = 0; i < 3; i++) if (reference.action.name == "UpgradeChoice" + (i + 1)) upgradeChoices[i] = reference;
            }
        }
        private static void EnsureEventSystem(Scene scene)
        {
            if (Find<EventSystem>(scene) != null) return;
            var obj = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            obj.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        private static Vector3 Ground(Vector3 near)
            => Physics.Raycast(near + Vector3.up * 8, Vector3.down, out var hit, 30, 1, QueryTriggerInteraction.Ignore) ? hit.point : near;
        private static GameObject Trigger(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var obj = Child(parent, name); obj.transform.position = position;
            var trigger = obj.AddComponent<BoxCollider>(); trigger.isTrigger = true; trigger.size = size;
            var body = obj.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false; return obj;
        }
        private static GameObject Child(Transform parent, string name, params Type[] components)
        {
            var obj = new GameObject(name, components); obj.transform.SetParent(parent, false); return obj;
        }
        private static GameObject Cube(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name; obj.transform.SetParent(parent, false);
            obj.transform.position = position; obj.transform.localScale = size; obj.GetComponent<Renderer>().sharedMaterial = material; return obj;
        }
        private static GameObject Sphere(Transform parent, string name, Vector3 position, float size, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere); obj.name = name; obj.layer = 2;
            Object.DestroyImmediate(obj.GetComponent<Collider>()); obj.transform.SetParent(parent, false); obj.transform.localPosition = position;
            obj.transform.localScale = Vector3.one * size; obj.GetComponent<Renderer>().sharedMaterial = material;
            obj.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off; return obj;
        }
        private static TextMesh Sign(Transform parent, string name, Vector3 position, string content, float size)
        {
            var obj = Child(parent, name); obj.transform.position = position;
            var text = obj.AddComponent<TextMesh>(); text.text = content; text.characterSize = size; text.fontSize = 64; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
            text.color = new Color(0.7f, 1f, 0.85f); return text;
        }
        private static Text Label(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, string content, int fontSize)
        {
            var obj = Child(parent, name, typeof(RectTransform), typeof(Text));
            var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = anchor; rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = obj.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = fontSize;
            text.text = content; text.color = new Color(0.86f, 0.84f, 0.77f); text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; return text;
        }
        private static Button Button(Transform parent, string name, Vector2 position)
        {
            var obj = Child(parent, name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = obj.GetComponent<RectTransform>(); rect.anchoredPosition = position; rect.sizeDelta = new Vector2(290, 65);
            obj.GetComponent<Image>().color = new Color(0.15f, 0.22f, 0.2f);
            Label(obj.transform, "Label", Vector2.one * 0.5f, Vector2.zero, new Vector2(280, 60), name, 23);
            var button = obj.GetComponent<Button>(); button.targetGraphic = obj.GetComponent<Image>(); return button;
        }
        internal static T Find<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects()) { var value = root.GetComponentInChildren<T>(true); if (value != null) return value; }
            return null;
        }
        internal static List<T> All<T>(Scene scene) where T : Component
        {
            var values = new List<T>(); foreach (var root in scene.GetRootGameObjects()) values.AddRange(root.GetComponentsInChildren<T>(true)); return values;
        }
        internal static void Ref(Object target, string field, Object value) { var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void String(Object target, string field, string value) { var so = new SerializedObject(target); so.FindProperty(field).stringValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Int(Object target, string field, int value) { var so = new SerializedObject(target); so.FindProperty(field).intValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Float(Object target, string field, float value) { var so = new SerializedObject(target); so.FindProperty(field).floatValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Bool(Object target, string field, bool value) { var so = new SerializedObject(target); so.FindProperty(field).boolValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void ArrayRef(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target); var array = so.FindProperty(field); array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
