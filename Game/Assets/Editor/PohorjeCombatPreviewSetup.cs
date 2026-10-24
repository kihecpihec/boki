using System;
using Boki.Combat;
using Boki.Enemies;
using Boki.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Boki.Editor
{
    public static class PohorjeCombatPreviewSetup
    {
        [MenuItem("Boki/Pohorje Production/Add Combat Preview")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Pohorje_Production_Preview.unity")
                throw new InvalidOperationException("Open the existing Pohorje production preview first.");
            ScooterController player = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<ScooterController>() is ScooterController found)
                    player = found;
            if (player == null) throw new InvalidOperationException("Preview player is missing.");
            var melee = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Melee_Forest.prefab");
            var watcher = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Watcher_Forest.prefab");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/M2/Laser.mat");
            if (melee == null || watcher == null || material == null)
                throw new InvalidOperationException("Existing enemy prefabs or laser material are missing.");
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Combat Preview Enemies") Undo.DestroyObjectImmediate(root);
            var health = player.GetComponent<PlayerHealth>();
            if (health == null) health = Undo.AddComponent<PlayerHealth>(player.gameObject);
            if (player.GetComponent<AutoLaser>() == null)
            {
                var origin = new GameObject("Combat Preview Laser Origin");
                Undo.RegisterCreatedObjectUndo(origin, "Add preview laser");
                origin.transform.SetParent(player.transform, false);
                origin.transform.localPosition = new Vector3(0f, 0.7f, 0.8f);
                var beamObject = new GameObject("Combat Preview Laser Beam");
                Undo.RegisterCreatedObjectUndo(beamObject, "Add preview laser");
                beamObject.transform.SetParent(player.transform, false);
                var beam = beamObject.AddComponent<LineRenderer>();
                beam.sharedMaterial = material;
                beam.startWidth = beam.endWidth = 0.04f;
                beam.positionCount = 2;
                beam.enabled = false;
                beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beam.receiveShadows = false;
                var laser = Undo.AddComponent<AutoLaser>(player.gameObject);
                Reference(laser, "playerHealth", health);
                Reference(laser, "beamOrigin", origin.transform);
                Reference(laser, "beam", beam);
            }
            var group = new GameObject("Combat Preview Enemies");
            Undo.RegisterCreatedObjectUndo(group, "Add preview enemies");
            Physics.SyncTransforms();
            var templates = new[]
            {
                Spawn(melee, new Vector3(-2f, 0f, 8f)),
                Spawn(melee, new Vector3(2f, 0f, 12f)),
                Spawn(watcher, new Vector3(0f, 1.7f, 16f))
            };
            var preview = Undo.AddComponent<EnemyCombatPreview>(group);
            Reference(preview, "player", health);
            var settings = new SerializedObject(preview);
            var items = settings.FindProperty("templates");
            items.arraySize = templates.Length;
            for (int i = 0; i < templates.Length; i++)
                items.GetArrayElementAtIndex(i).objectReferenceValue = templates[i];
            settings.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("POHORJE_COMBAT_PREVIEW_READY: Boki, two melee enemies, one Watcher. Press Play.");

            GameObject Spawn(GameObject prefab, Vector3 offset)
            {
                var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(enemy, "Add preview enemy");
                enemy.transform.SetParent(group.transform, false);
                var position = player.transform.position + player.transform.rotation * new Vector3(offset.x, 0f, offset.z);
                if (Physics.Raycast(position + Vector3.up * 20f, Vector3.down, out var hit, 60f, 1, QueryTriggerInteraction.Ignore))
                    position.y = hit.point.y;
                enemy.transform.position = position + Vector3.up * offset.y;
                if (enemy.TryGetComponent<MeleeEnemy>(out var chaser)) Reference(chaser, "target", health);
                if (enemy.TryGetComponent<RangedEnemy>(out var ranged)) Reference(ranged, "target", health);
                enemy.name += " Spawn Template";
                enemy.SetActive(false);
                return enemy;
            }
        }

        private static void Reference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }
    }
}
