using System;
using Boki.Combat;
using Boki.Enemies;
using Boki.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Boki.Editor
{
    // Builds enemy materials, Animator controllers, prefabs and the owned Enemy_Test scene.
    // Idempotent: existing assets are reused, never overwritten. Production scenes are not touched.
    public static class EnemyPrefabBuilder
    {
        private const string Art = EnemyModelSetup.Art;
        private const string PrefabFolder = "Assets/Prefabs/Enemies/";
        private const string ScenePath = "Assets/Scenes/Enemy_Test.unity";
        private const string SourceScenePath = "Assets/Scenes/Combat_Test.unity";
        private const int IgnoreRaycastLayer = 2;

        [MenuItem("Boki/Enemy Models/Create Prefabs")]
        public static void CreatePrefabs()
        {
            RequireClean();
            EnemyModelSetup.ConfigureImports();
            Material forest = Atlas("M_Melee_Forest", "T_Melee_Forest_BaseColor");
            Material rust = Atlas("M_Melee_Rust", "T_Melee_Rust_BaseColor");
            Material student = Atlas("M_Melee_Student", "T_Melee_Student_BaseColor");
            Material watcherForest = Atlas("M_Watcher_Forest", "T_Watcher_Forest_BaseColor");
            Material watcherCity = Atlas("M_Watcher_City", "T_Watcher_City_BaseColor");
            Material eye = Emissive("M_Watcher_Eye", "T_Watcher_Eye", new Color(1f, 0.1f, 0.05f));
            Material guilt = Atlas("M_Guilt", "T_Guilt_BaseColor");
            Material professor = Atlas("M_Professor", "T_Professor_BaseColor");
            Material props = Atlas("M_Professor_Props", "T_Professor_Props");
            Material paper = Atlas("M_ExamPaper", "T_ExamPaper_BaseColor");
            Material ring = Emissive("M_FloorRing", "T_Professor_Props", new Color(1f, 0.05f, 0.05f));

            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Enemies")) AssetDatabase.CreateFolder("Assets/Prefabs", "Enemies");
            EnemyProjectile orb = BuildProjectile("Proj_WatcherOrb", "PROJ_WatcherOrb", eye, 0f);
            EnemyProjectile sheet = BuildProjectile("Proj_ExamPaper", "PROP_ExamPaper", paper, 240f);

            BuildMelee("Melee_Forest", forest, false, false);
            BuildMelee("Melee_City", rust, true, false);
            BuildMelee("Melee_Student", student, true, true);
            BuildWatcher("Watcher_Forest", watcherForest, eye, orb);
            BuildWatcher("Watcher_City", watcherCity, eye, orb);
            BuildGuilt(guilt);
            BuildProfessor(professor, props, ring, sheet);
            AssetDatabase.SaveAssets();
            Debug.Log("Enemy prefabs ready in " + PrefabFolder);
        }

        [MenuItem("Boki/Enemy Models/Create Test Scene")]
        public static void CreateTestScene()
        {
            RequireClean();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.Log("Enemy_Test already exists; open it from Assets/Scenes.");
                return;
            }
            if (!AssetDatabase.CopyAsset(SourceScenePath, ScenePath))
                throw new InvalidOperationException("Could not copy " + SourceScenePath);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            foreach (EnemyHealth old in UnityEngine.Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
            if (player == null) throw new InvalidOperationException("No PlayerHealth in the copied scene.");
            Vector3 origin = player.transform.position;

            Place("Melee_Forest", origin + new Vector3(-6f, 0f, 10f), player);
            Place("Melee_City", origin + new Vector3(0f, 0f, 12f), player);
            Place("Melee_Student", origin + new Vector3(6f, 0f, 10f), player);
            Place("Watcher_Forest", origin + new Vector3(-8f, 2f, 18f), player);
            Place("Watcher_City", origin + new Vector3(8f, 2f, 18f), player);

            GameObject guilt = Place("Guilt", new Vector3(0f, 0f, 10f), player);
            // Combat_Test's arena is 32 x 32 m centred on the origin; keep every marker inside its walls.
            var markerPositions = new[] { new Vector3(-12f, 0f, 10f), new Vector3(12f, 0f, 10f), new Vector3(0f, 0f, -13f) };
            var markers = new Transform[markerPositions.Length];
            for (int i = 0; i < markers.Length; i++)
            {
                var marker = new GameObject("GuiltSpawn_" + (i + 1)).transform;
                marker.position = markerPositions[i];
                markers[i] = marker;
            }
            SetArray(guilt.GetComponent<GuiltChaser>(), "spawnPoints", markers);

            GameObject boss = Place("Professor", new Vector3(0f, 0f, 13f), player);
            boss.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            SetBool(boss.GetComponent<ProfessorBoss>(), "startOnEnable", true);

            EditorSceneManager.SaveScene(scene);
            Debug.Log("Enemy_Test saved. Not in the build list.");
        }

        // ---- prefab builders ----

        [MenuItem("Boki/Enemy Models/Upgrade Test Models and Effects")]
        public static void UpgradeTestScene()
        {
            RequireClean();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("Enemy_Test is missing; refusing to create a replacement.");
            var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Boki_Rigged_Test.prefab");
            if (template == null || template.transform.Find("Visuals") == null)
                throw new InvalidOperationException("The rigged Boki test prefab is missing its Visuals.");
            SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var player = UnityEngine.Object.FindFirstObjectByType<ScooterController>();
                if (player == null) throw new InvalidOperationException("Enemy_Test has no player.");
                Transform visuals = player.transform.Find("Visuals");
                if (visuals == null)
                {
                    // Copy only the presentation; retain the arena's player, combat, input and camera references.
                    visuals = UnityEngine.Object.Instantiate(template.transform.Find("Visuals"), player.transform, false);
                    visuals.name = "Visuals";
                }
                foreach (string name in new[] { "Boki placeholder", "Scooter deck", "Forward marker" })
                {
                    Transform placeholder = player.transform.Find(name);
                    if (placeholder == null) throw new InvalidOperationException("Missing placeholder: " + name);
                    placeholder.gameObject.SetActive(false);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(placeholder.gameObject);
                }
                Animator animator = visuals.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.runtimeAnimatorController == null)
                    throw new InvalidOperationException("The riding model has no configured Animator.");
                animator.applyRootMotion = false;
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                    if (clip.name == "Boki_RideIdle") clip.SampleAnimation(animator.gameObject, 0f);

                var laser = player.GetComponent<AutoLaser>();
                Transform muzzle = FindDeep(visuals, "Socket_LaserMuzzle");
                if (laser == null || muzzle == null)
                    throw new InvalidOperationException("The existing laser or scooter muzzle is missing.");
                SetObject(laser, "beamOrigin", muzzle);
                PrefabUtility.RecordPrefabInstancePropertyModifications(laser);

                var enemies = UnityEngine.Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
                if (enemies.Length != 6)
                    throw new InvalidOperationException("Expected three melee enemies, two Watchers and the professor.");
                foreach (EnemyHealth health in enemies)
                {
                    if (health.GetComponent<EnemyFeedback>() == null) health.gameObject.AddComponent<EnemyFeedback>();
                    var serialized = new SerializedObject(health);
                    serialized.FindProperty("showCombatFeedback").boolValue = true;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(health);
                }
                UpgradeAttacks(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("ENEMY_TEST_UPGRADED: rigged Boki and scooter, muzzle-mounted laser, six hit/death feedback components; professor cast/recoil, exact-radius deadline countdown and shockwave, paper/orb trails and impacts configured.");
            }
            finally
            {
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        internal static void UpgradeAttacks(Scene scene)
        {
            const string materialPath = Art + "M_EnemyAttackFX.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/EnemyFeedbackParticles.shader");
                if (shader == null) throw new InvalidOperationException("Missing enemy effect shader.");
                material = new Material(shader) { name = "M_EnemyAttackFX" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            Transform templates = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                    if (candidate.name == "Enemy Attack Templates") templates = candidate;
            if (templates == null) templates = new GameObject("Enemy Attack Templates").transform;
            templates.gameObject.SetActive(false);
            EnemyProjectile paper = AttackTemplate(templates, "Proj_ExamPaper", material,
                new Color(1f, 0.65f, 0.12f), 0.16f);
            EnemyProjectile orb = AttackTemplate(templates, "Proj_WatcherOrb", material,
                new Color(1f, 0.1f, 0.05f), 0.2f);
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (RangedEnemy ranged in root.GetComponentsInChildren<RangedEnemy>(true))
            {
                SetObject(ranged, "projectilePrefab", ranged.name.StartsWith("ExamPaper", StringComparison.Ordinal) ? paper : orb);
                PrefabUtility.RecordPrefabInstancePropertyModifications(ranged);
            }
            ProfessorBoss boss = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<ProfessorBoss>(true) is ProfessorBoss foundBoss) boss = foundBoss;
            if (boss == null) return;
            SetObject(boss, "sheetPrefab", paper);
            PrefabUtility.RecordPrefabInstancePropertyModifications(boss);
            // Replace the textured model with an exact world-space radius, retaining the boss's marker reference.
            var settings = new SerializedObject(boss);
            Transform marker = (Transform)settings.FindProperty("slamRing").objectReferenceValue;
            foreach (Renderer renderer in marker.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            var view = boss.GetComponent<ProfessorAttackView>();
            if (view == null) view = boss.gameObject.AddComponent<ProfessorAttackView>();
            SetObject(view, "visual", boss.transform.Find("Visual"));
            SetObject(view, "warning", AttackLine(boss.transform, "Deadline Boundary", material));
            SetObject(view, "countdown", AttackLine(boss.transform, "Deadline Countdown", material));
            SetObject(view, "shockwave", AttackLine(boss.transform, "Deadline Impact", material));
            SetObject(view, "castPulse", AttackLine(boss.transform, "Exam Cast", material));
            AssetDatabase.SaveAssets();
        }

        private static EnemyProjectile AttackTemplate(Transform parent, string name, Material material, Color color, float width)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + name + ".prefab");
                if (prefab == null) throw new InvalidOperationException("Missing projectile: " + name);
                GameObject instance = UnityEngine.Object.Instantiate(prefab, parent, false);
                instance.name = name;
                child = instance.transform;
            }
            var projectile = child.GetComponent<EnemyProjectile>();
            SetObject(projectile, "trailMaterial", material);
            SetFloat(projectile, "trailWidth", width);
            Apply(projectile, "trailColor", p => p.colorValue = color);
            return projectile;
        }

        private static LineRenderer AttackLine(Transform parent, string name, Material material)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }
            var line = child.GetComponent<LineRenderer>();
            if (line == null) line = child.gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = 64;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        private static void BuildMelee(string name, Material skin, bool jacket, bool backpack)
        {
            GameObject root = NewRoot(name);
            GameObject visual = AddModel(root, "CHR_Melee", "Visual");
            SetMaterials(visual, skin);
            Toggle(visual, "M_Melee_Jacket", jacket);
            Toggle(visual, "M_Melee_Backpack", backpack);
            AttachAnimator(visual, "Melee_Idle", "Melee_Idle", "Melee_Pursuit", "Melee_Contact", "Melee_Hit", "Melee_Death");

            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            var aim = new GameObject("AimPoint").transform;
            aim.SetParent(root.transform, false);
            aim.localPosition = new Vector3(0f, 1.1f, 0f);
            var health = root.AddComponent<EnemyHealth>();
            SetInt(health, "maximumHealth", 60);
            SetObject(health, "aimPoint", aim);
            root.AddComponent<MeleeEnemy>();
            var view = root.AddComponent<MeleeView>();
            SetObject(view, "animator", visual.GetComponent<Animator>());
            Save(root, name);
        }

        private static void BuildWatcher(string name, Material shell, Material eye, EnemyProjectile projectile)
        {
            GameObject root = NewRoot(name);
            GameObject visual = AddModel(root, "CHR_Watcher", "Visual");
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = Fill(renderer, renderer.name == "M_WatcherEye" ? eye : shell);

            var aim = new GameObject("AimPoint").transform;
            aim.SetParent(root.transform, false);
            var collider = root.AddComponent<SphereCollider>();
            collider.radius = 0.4f;
            collider.isTrigger = true;
            var health = root.AddComponent<EnemyHealth>();
            SetInt(health, "maximumHealth", 20);
            SetObject(health, "aimPoint", aim);
            var ranged = root.AddComponent<RangedEnemy>();
            SetObject(ranged, "projectilePrefab", projectile);
            SetObject(ranged, "muzzle", FindDeep(visual.transform, "Socket_ProjectileOrigin"));
            var view = root.AddComponent<WatcherView>();
            SetObject(view, "visual", visual.transform);
            Save(root, name);
        }

        private static void BuildGuilt(Material skin)
        {
            GameObject root = NewRoot("Guilt");
            GameObject visual = AddModel(root, "CHR_Guilt", "Visual");
            SetMaterials(visual, skin);
            AttachAnimator(visual, "Guilt_Warning", "Guilt_Warning", "Guilt_Chase");
            var controller = root.AddComponent<CharacterController>();
            controller.height = 3f;
            controller.radius = 0.7f;
            controller.center = new Vector3(0f, 1.5f, 0f);
            var chaser = root.AddComponent<GuiltChaser>();
            SetObject(chaser, "visualRoot", visual);
            var view = root.AddComponent<GuiltView>();
            SetObject(view, "animator", visual.GetComponent<Animator>());
            Save(root, "Guilt");
        }

        private static void BuildProfessor(Material body, Material props, Material ringMaterial, EnemyProjectile sheet)
        {
            GameObject root = NewRoot("Professor");
            GameObject visual = AddModel(root, "CHR_Professor", "Visual");
            SetMaterials(visual, body);
            AttachAnimator(visual, "Professor_Idle", "Professor_Idle");
            GameObject lectern = AddModel(root, "PROP_Lectern", "Lectern");
            lectern.transform.localPosition = new Vector3(0f, 0f, 0.45f);
            SetMaterials(lectern, props);

            var origin = new GameObject("SheetOrigin").transform;
            origin.SetParent(root.transform, false);
            origin.localPosition = new Vector3(0f, 1.3f, 0.7f);
            var aim = new GameObject("AimPoint").transform;
            aim.SetParent(root.transform, false);
            aim.localPosition = new Vector3(0f, 1.3f, 0f);
            var ring = new GameObject("SlamRing");
            ring.transform.SetParent(root.transform, false);
            GameObject ringModel = AddModel(ring, "PROP_FloorRing", "Model");
            SetMaterials(ringModel, ringMaterial);

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.45f;
            capsule.center = new Vector3(0f, 0.9f, 0f);
            var health = root.AddComponent<EnemyHealth>();
            SetInt(health, "maximumHealth", 4000); // Story §4
            SetObject(health, "aimPoint", aim);
            var boss = root.AddComponent<ProfessorBoss>();
            SetObject(boss, "sheetPrefab", sheet);
            SetObject(boss, "sheetOrigin", origin);
            SetObject(boss, "slamRing", ring.transform);
            Save(root, "Professor");
        }

        private static EnemyProjectile BuildProjectile(string name, string model, Material material, float spin)
        {
            GameObject root = new GameObject(name);
            GameObject visual = AddModel(root, model, "Visual");
            SetMaterials(visual, material);
            var projectile = root.AddComponent<EnemyProjectile>();
            SetFloat(projectile, "spinDegreesPerSecond", spin);
            return Save(root, name).GetComponent<EnemyProjectile>();
        }

        // ---- helpers ----

        private static GameObject NewRoot(string name)
        {
            var root = new GameObject(name) { layer = IgnoreRaycastLayer };
            return root;
        }

        private static GameObject AddModel(GameObject parent, string fbx, string childName)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Export/" + fbx + ".fbx");
            if (model == null) throw new InvalidOperationException("Missing FBX: " + fbx);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent.transform);
            instance.name = childName;
            // Imported axis/unit conversion on the FBX root is preserved; only position is reset.
            instance.transform.localPosition = Vector3.zero;
            return instance;
        }

        private static void SetMaterials(GameObject visual, Material material)
        {
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = Fill(renderer, material);
        }

        private static Material[] Fill(Renderer renderer, Material material)
        {
            var materials = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            return materials;
        }

        private static void Toggle(GameObject visual, string meshName, bool on)
        {
            Transform mesh = FindDeep(visual.transform, meshName);
            if (mesh == null) throw new InvalidOperationException("Missing mesh: " + meshName);
            mesh.gameObject.SetActive(on);
        }

        private static void AttachAnimator(GameObject visual, string defaultClip, params string[] clipNames)
        {
            string fbxPath = null;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Art + "Export" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is AnimationClip clip && clip.name == defaultClip) fbxPath = path;
            }
            if (fbxPath == null) throw new InvalidOperationException("Clip not found: " + defaultClip);
            string controllerPath = fbxPath.Replace("Export/", "").Replace(".fbx", ".controller");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                foreach (string clipName in clipNames)
                    foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                        if (asset is AnimationClip clip && clip.name == clipName)
                            controller.AddMotion(clip).name = clipName;
            }
            var animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
        }

        private static Material Atlas(string name, string texture)
        {
            string path = Art + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
            var material = new Material(shader) { name = name };
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Textures/" + texture + ".png"));
            material.SetFloat("_Smoothness", 0.05f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // No real-time lights: emission only, so the eye, orb and ring stay readable in fog (Story §6).
        private static Material Emissive(string name, string texture, Color emission)
        {
            Material material = Atlas(name, texture);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission * 2f);
            material.SetTexture("_EmissionMap", material.GetTexture("_BaseMap"));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject Save(GameObject root, string name)
        {
            string path = PrefabFolder + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw new InvalidOperationException("Prefab already exists; refusing to overwrite: " + path);
            }
            if (root.layer == IgnoreRaycastLayer)
                foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = IgnoreRaycastLayer;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject Place(string prefabName, Vector3 position, PlayerHealth player)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + prefabName + ".prefab");
            if (prefab == null) throw new InvalidOperationException("Missing prefab: " + prefabName);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = position;
            foreach (var behaviour in instance.GetComponents<MonoBehaviour>())
            {
                var serialized = new SerializedObject(behaviour);
                SerializedProperty target = serialized.FindProperty("target");
                if (target == null) continue;
                target.objectReferenceValue = player;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            return instance;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void SetObject(UnityEngine.Object owner, string field, UnityEngine.Object value) =>
            Apply(owner, field, p => p.objectReferenceValue = value);
        private static void SetInt(UnityEngine.Object owner, string field, int value) =>
            Apply(owner, field, p => p.intValue = value);
        private static void SetFloat(UnityEngine.Object owner, string field, float value) =>
            Apply(owner, field, p => p.floatValue = value);
        private static void SetBool(UnityEngine.Object owner, string field, bool value) =>
            Apply(owner, field, p => p.boolValue = value);
        private static void SetArray(UnityEngine.Object owner, string field, UnityEngine.Object[] values) =>
            Apply(owner, field, p =>
            {
                p.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            });

        private static void Apply(UnityEngine.Object owner, string field, Action<SerializedProperty> assign)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) throw new InvalidOperationException("Missing serialized field: " + field);
            assign(property);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RequireClean()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before building enemy assets.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard dirty scenes before building enemy assets.");
        }
    }
}
