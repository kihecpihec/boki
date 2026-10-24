using Boki.Combat;
using Boki.Core;
using Boki.Enemies;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Boki.Player
{
    // Scene-local playtest controls; never used by production routing or progression.
    [DisallowMultipleComponent]
    public sealed class PlayerTestArena : MonoBehaviour
    {
        [SerializeField] private PlayerHealth player;
        [SerializeField] private ScooterController movement;
        [SerializeField] private AutoLaser laser;
        [SerializeField] private Transform hub;
        [SerializeField] private Transform movementEntry;
        [SerializeField] private EnemyTestStation[] stations;
        [SerializeField] private Text status;
        [SerializeField] private Text laserLabel;
        [SerializeField] private Boki.UI.PlayerHud hud;
        [SerializeField] private ThirdPersonCamera followCamera;
        private CharacterController controller;
        private EnemyTestStation active;
        private string previousStatus;
        public EnemyTestStation ActiveStation => active;

        private void Awake() => controller = player.GetComponent<CharacterController>();

        private void OnEnable()
        {
            if (player != null && movement != null && laser != null && hub != null && movementEntry != null
                && stations != null && stations.Length > 0 && status != null && laserLabel != null && hud != null
                && followCamera != null)
            {
                player.Died += OnPlayerDied;
                return;
            }
            Debug.LogError("Assign the player, movement, laser, entries, stations, HUD and panel labels on PlayerTestArena.", this);
            enabled = false;
        }

        private void OnPlayerDied()
        {
            movement.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void FixedUpdate()
        {
            if (!player.IsAlive || (RunSession.Instance != null && !RunSession.Instance.IsGameplayActive)) return;
            EnemyTestStation inside = null;
            foreach (var station in stations)
                if (station.Contains(player.transform.position)) { inside = station; break; }
            if (inside != active) Activate(inside);
        }

        private void Update()
        {
            string message = !player.IsAlive ? "ABSENT — Restart sandbox to try again."
                : active != null ? active.Description : "SAFE HUB — Choose a station or ride through a marked entrance.";
            if (message != previousStatus) { status.text = message; previousStatus = message; }
        }

        public void Visit(EnemyTestStation station)
        {
            if (!player.IsAlive) return;
            Teleport(station.Entry);
            Activate(station);
        }

        public void ReturnToHub()
        {
            if (!player.IsAlive) return;
            Activate(null);
            Teleport(hub);
        }

        public void ReturnToMovement()
        {
            if (!player.IsAlive) return;
            Activate(null);
            Teleport(movementEntry);
        }

        public void ResetEncounter()
        {
            if (active == null || !player.IsAlive) return;
            var station = active;
            Activate(null);
            Teleport(station.Entry);
            Activate(station);
        }

        public void ToggleLaser()
        {
            laser.enabled = !laser.enabled;
            laserLabel.text = laser.enabled ? "Laser: ON" : "Laser: OFF (observe attacks)";
        }

        private void Activate(EnemyTestStation station)
        {
            if (active != null) active.End();
            foreach (var projectile in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
                if (projectile.gameObject.scene == gameObject.scene)
                {
                    projectile.gameObject.SetActive(false);
                    Destroy(projectile.gameObject);
                }
            RunSession.Ensure().State.Guilt.Reset();
            active = station;
            if (active != null) active.Begin();
            hud.BindGuilt(active != null ? active.Enemy.GetComponent<GuiltChaser>() : null);
        }

        private void Teleport(Transform destination)
        {
            movement.enabled = false;
            controller.enabled = false;
            player.transform.SetPositionAndRotation(destination.position, destination.rotation);
            controller.enabled = true;
            movement.enabled = true;
            followCamera.SnapHeading(destination.eulerAngles.y);
            player.GetComponent<BokiPlayerEffects>().ClearTrails();
        }

        public void RestartSandbox()
        {
            Activate(null);
            var session = RunSession.Ensure();
            session.State.NewRun(session.State.AvailableUnlocks, Time.realtimeSinceStartupAsDouble);
            string path = gameObject.scene.path;
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            if (Application.CanStreamedLevelBeLoaded(path)) SceneManager.LoadScene(path);
            else Debug.LogError("Add Player_Model_Test to a test build before using Restart sandbox.", this);
#endif
        }

        private void OnDisable()
        {
            if (player != null) player.Died -= OnPlayerDied;
            if (active != null) active.End();
        }
    }
}
