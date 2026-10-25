using System;
using Boki.Combat;
using Boki.Core;
using Boki.Enemies;
using Boki.Player;
using Boki.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    [InitializeOnLoad]
    public static class M2SmokeChecks
    {
        private const string Key = "Boki.M2.SmokeRunning";
        private static bool failed;
        private static int stage;
        private static int retries;
        private static double deadline;
        private static double started;
        private static PlayerHealth player;
        private static EnemyHealth victim;
        private static MeleeEnemy attacker;
        private static CombatSession oldSession;
        private static int previousHp;

        static M2SmokeChecks()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += StateChanged;
            Application.logMessageReceived += LogReceived;
        }

        [MenuItem("Boki/Run M2 Smoke Checks")]
        public static void Run()
        {
            EditorApplication.delayCall += () => EditorApplication.delayCall += Begin;
        }
        private static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Require(!SceneManager.GetSceneAt(i).isDirty, "Save open scenes before checking M2.");
            EditorSceneManager.OpenScene(M2SceneBuilder.ScenePath);
            Require(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1, "One camera required.");
            Require(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 1, "One listener required.");
            Require(Object.FindObjectsByType<MeleeEnemy>(FindObjectsSortMode.None).Length == 4, "Four enemies required.");
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }
        private static void StateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                stage = retries = 0;
                failed = false;
                started = EditorApplication.timeSinceStartup;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                Debug.Log(failed ? "M2_SMOKE_FAILED" : "M2_SMOKE_PASSED: targeting, occlusion, kill, melee damage, death shutdown, and three retries.");
                if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
            }
        }
        private static void LogReceived(string message, string trace, LogType type)
        {
            if (SessionState.GetBool(Key, false) && (type == LogType.Exception || type == LogType.Error)) failed = true;
        }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                Require(EditorApplication.timeSinceStartup - started < 60, "M2 smoke timeout.");
                if (stage == 0) CheckTargeting();
                else if (stage == 1 && Time.realtimeSinceStartupAsDouble >= deadline)
                {
                    Require(victim == null, "Automatic laser must kill the nearest enemy.");
                    var laser = Object.FindFirstObjectByType<AutoLaser>();
                    laser.enabled = false;
                    attacker = Object.FindFirstObjectByType<MeleeEnemy>();
                    Teleport(attacker.gameObject, player.transform.position + Vector3.forward * 0.8f);
                    previousHp = player.Current;
                    attacker.enabled = true;
                    deadline = Time.realtimeSinceStartupAsDouble + 1.1;
                    stage = 2;
                }
                else if (stage == 2 && Time.realtimeSinceStartupAsDouble >= deadline)
                {
                    Require(player.Current < previousHp && player.Current >= previousHp - 20,
                        "Melee must damage at its interval, rather than every frame.");
                    KillPlayerAndRetry();
                }
                else if (stage == 3)
                {
                    var session = Object.FindFirstObjectByType<CombatSession>();
                    if (session == null || session == oldSession) return;
                    player = Object.FindFirstObjectByType<PlayerHealth>();
                    Require(player.Current == 100 && !session.IsDead, "Retry must reset player health and session.");
                    Require(Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None).Length == 4, "Retry must restore enemies.");
                    Require(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1, "Retry must not duplicate cameras.");
                    Require(Object.FindFirstObjectByType<ScooterController>().enabled
                        && Object.FindFirstObjectByType<ThirdPersonCamera>().enabled
                        && Object.FindFirstObjectByType<AutoLaser>().enabled, "Retry must restore controls and combat.");
                    Require(!Object.FindFirstObjectByType<CombatView>().DeathVisible, "Retry must hide death UI.");
                    if (retries >= 3) EditorApplication.ExitPlaymode();
                    else KillPlayerAndRetry();
                }
            }
            catch (Exception exception)
            {
                failed = true;
                Debug.LogException(exception);
                EditorApplication.ExitPlaymode();
            }
        }

        private static void CheckTargeting()
        {
            player = Object.FindFirstObjectByType<PlayerHealth>();
            player.GetComponent<ScooterController>().enabled = false;
            var laser = player.GetComponent<AutoLaser>();
            laser.enabled = false;
            var enemies = Object.FindObjectsByType<MeleeEnemy>(FindObjectsSortMode.InstanceID);
            for (int i = 0; i < enemies.Length; i++)
            {
                enemies[i].enabled = false;
                Teleport(enemies[i].gameObject, new Vector3(-12f + i, 0.05f, 12f));
            }
            Teleport(player.gameObject, new Vector3(0f, 0.05f, -6f));
            Require(laser.FindTarget() == null, "Out-of-range enemies must be ignored.");
            Teleport(enemies[0].gameObject, new Vector3(0f, 0.05f, -2f));
            Teleport(enemies[1].gameObject, new Vector3(3f, 0.05f, -2f));
            victim = enemies[0].GetComponent<EnemyHealth>();
            Require(laser.FindTarget() == victim, "Laser must choose nearest living target.");
            // Additional colliders do not create duplicate registry entries.
            var extraCollider = enemies[0].gameObject.AddComponent<BoxCollider>();
            extraCollider.isTrigger = true;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1.5f, -4f);
            wall.transform.localScale = new Vector3(1f, 3f, 0.2f);
            Physics.SyncTransforms();
            Require(laser.FindTarget() == enemies[1].GetComponent<EnemyHealth>(), "Nearest occluded target must yield to visible enemy.");
            wall.SetActive(false);
            Physics.SyncTransforms();
            victim.enabled = false;
            Require(laser.FindTarget() == enemies[1].GetComponent<EnemyHealth>(), "Disabled targets must be skipped.");
            victim.enabled = true;
            Teleport(enemies[1].gameObject, new Vector3(-10f, 0.05f, 12f));
            laser.enabled = true;
            deadline = Time.realtimeSinceStartupAsDouble + 1.5;
            stage = 1;
        }

        private static void KillPlayerAndRetry()
        {
            oldSession = Object.FindFirstObjectByType<CombatSession>();
            player.TakeDamage(int.MaxValue);
            Require(oldSession.IsDead && player.Current == 0, "Lethal damage must end combat.");
            Require(!player.GetComponent<ScooterController>().enabled
                && !player.GetComponent<AutoLaser>().enabled
                && !Object.FindFirstObjectByType<ThirdPersonCamera>().enabled, "Death must disable movement, laser, and camera.");
            foreach (var enemy in Object.FindObjectsByType<MeleeEnemy>(FindObjectsSortMode.None))
                Require(!enemy.enabled, "Death must stop melee AI.");
            var view = Object.FindFirstObjectByType<CombatView>();
            Require(view.DeathVisible && Cursor.lockState == CursorLockMode.None, "Death UI needs an unlocked cursor.");
            player.TakeDamage(10);
            view.RetryButton.onClick.Invoke();
            retries++;
            stage = 3;
        }

        private static void Teleport(GameObject item, Vector3 position)
        {
            var controller = item.GetComponent<CharacterController>();
            controller.enabled = false;
            item.transform.position = position;
            controller.enabled = true;
            Physics.SyncTransforms();
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
