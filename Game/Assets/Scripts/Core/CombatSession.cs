using Boki.Combat;
using Boki.Enemies;
using Boki.Player;
using Boki.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Boki.Core
{
    // Scene-local shutdown/UI; M3 optionally delegates new-run initialization to RunSession.
    public sealed class CombatSession : MonoBehaviour
    {
        [SerializeField] private bool useRunSession;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private ScooterController movement;
        [SerializeField] private ThirdPersonCamera cameraController;
        [SerializeField] private AutoLaser weapon;
        [SerializeField] private MeleeEnemy[] enemies;
        [SerializeField] private CombatView view;
        private bool dead;
        private bool retrying;
        public bool IsDead => dead;

        private void OnEnable()
        {
            if (playerHealth == null || movement == null || cameraController == null || weapon == null
                || view == null || view.RetryButton == null)
            {
                Debug.LogError("Assign all player, camera, weapon, and UI references on CombatSession.", this);
                enabled = false;
                return;
            }
            playerHealth.Changed += RefreshHealth;
            playerHealth.Died += Die;
            view.RetryButton.onClick.AddListener(Retry);
            view.ShowDeath(false);
            RefreshHealth();
            if (!playerHealth.IsAlive) Die();
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.Changed -= RefreshHealth;
                playerHealth.Died -= Die;
            }
            if (view != null && view.RetryButton != null) view.RetryButton.onClick.RemoveListener(Retry);
        }

        private void RefreshHealth() => view.ShowHealth(playerHealth);
        private void Die()
        {
            if (dead) return;
            dead = true;
            movement.enabled = false;
            cameraController.enabled = false;
            weapon.enabled = false;
            if (enemies != null)
                foreach (var enemy in enemies)
                    if (enemy != null) enemy.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            view.ShowDeath(true);
        }

        public void Retry()
        {
            if (!dead || retrying) return;
            if (useRunSession)
            {
                retrying = true;
                if (!RunSession.Ensure().Retry()) retrying = false;
                return;
            }
            string path = gameObject.scene.path;
#if UNITY_EDITOR
            retrying = true;
            view.RetryButton.interactable = false;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            if (!Application.CanStreamedLevelBeLoaded(path))
            {
                Debug.LogError("Add the combat scene to the active build scene list before using Retry.", this);
                return;
            }
            retrying = true;
            view.RetryButton.interactable = false;
            SceneManager.LoadScene(path);
#endif
        }
    }
}
