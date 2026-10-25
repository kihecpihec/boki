using System;
using Boki.Combat;
using Boki.Core;
using Boki.Enemies;
using Boki.Player;
using Boki.Upgrades;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Boki.UI
{
    public sealed class V1GameFlow : MonoBehaviour
    {
        [SerializeField] private MenuScreen menu;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private ScooterController movement;
        [SerializeField] private ThirdPersonCamera followCamera;
        [SerializeField] private AutoLaser laser;
        [SerializeField] private GuiltChaser guilt;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text title;
        [SerializeField] private Text details;
        [SerializeField] private Text objective;
        [SerializeField] private Button resume;
        [SerializeField] private Button retry;
        [SerializeField] private UpgradeScreen upgrades;
        [SerializeField] private InputActionReference pauseAction;
        private readonly System.Random random = new System.Random();
        private UpgradeId[] offer;
        private int offerVersion;
        private RunSession session;
        private bool terminal, paused, choosing;
        private bool ownsPause;
        public bool IsTerminal => terminal;
        public bool IsChoosing => choosing;

        private void OnEnable()
        {
            if (health == null || movement == null || followCamera == null || laser == null || panel == null
                || title == null || details == null || objective == null || resume == null || retry == null
                || upgrades == null || pauseAction == null || pauseAction.action == null)
            {
                Debug.LogError("Assign player, pause/death UI, upgrade screen and Pause action on V1GameFlow.", this);
                enabled = false;
                return;
            }
            session = RunSession.Ensure();
            health.Died += Died;
            resume.onClick.AddListener(Resume);
            retry.onClick.AddListener(Retry);
            upgrades.ChoiceRequested.AddListener(Choose);
            if (menu != null)
            {
                menu.PauseRequested.AddListener(PauseMenu);
                menu.ResumeRequested.AddListener(Resume);
                menu.RestartRequested.AddListener(Retry);
                menu.NewRunRequested.AddListener(Retry);
                menu.MainMenuRequested.AddListener(MainMenu);
                menu.QuitRequested.AddListener(Quit);
            }
            else
            {
                pauseAction.action.performed += Pause;
                ownsPause = !pauseAction.action.enabled;
                if (ownsPause) pauseAction.action.Enable();
            }
            panel.SetActive(false);
        }
        private void Start() => movement.BindGuilt(guilt);
        private void OnDisable()
        {
            if (menu != null)
            {
                menu.PauseRequested.RemoveListener(PauseMenu);
                menu.ResumeRequested.RemoveListener(Resume);
                menu.RestartRequested.RemoveListener(Retry);
                menu.NewRunRequested.RemoveListener(Retry);
                menu.MainMenuRequested.RemoveListener(MainMenu);
                menu.QuitRequested.RemoveListener(Quit);
            }
            if (health != null) health.Died -= Died;
            if (resume != null) resume.onClick.RemoveListener(Resume);
            if (retry != null) retry.onClick.RemoveListener(Retry);
            if (upgrades != null) upgrades.ChoiceRequested.RemoveListener(Choose);
            if (pauseAction != null && pauseAction.action != null)
            {
                pauseAction.action.performed -= Pause;
                if (ownsPause) pauseAction.action.Disable();
            }
        }
        private void Update()
        {
            if (terminal || session == null) return;
            if (!paused && !choosing && session.State.Progression.HasPendingSelection) ShowUpgrade();
            if (!session.IsGameplayActive) return;
            if (health.transform.position.y < -30f) health.TakeDamage(health.Current);
        }
        public void SetObjective(string message) { if (objective != null && objective.text != message) objective.text = message; }
        private void Pause(InputAction.CallbackContext context) => PauseMenu();
        private void PauseMenu()
        {
            // A portal pull-in already holds the frozen run until its scene loads.
            if (terminal || choosing || session.IsLoading || session.State.Pause.Contains(PauseReason.ScriptedSequence)) return;
            if (paused) Resume();
            else
            {
                paused = true;
                if (menu == null) ShowPanel("PAUSED", "Ride, fight, collect XP, and reach the next portal.", true);
                else
                {
                    Suspend(true);
                    var state = session.State;
                    menu.SetRunSummary($"HP  {health.Current:0}\nBATTERY  {state.LightBattery.Current:0}\nXP  {state.XP}\n\nUPGRADES  {state.Progression.Selections}\nKILLS  {state.KillCount}");
                    menu.ShowPauseMenu();
                }
            }
        }
        private void Suspend(bool value, PauseReason reason = PauseReason.Menu)
        {
            session.SetPaused(reason, value);
            bool suspended = session.State.Pause.IsPaused;
            followCamera.enabled = !suspended;
            Cursor.lockState = suspended ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = suspended;
        }
        private void ShowPanel(string heading, string message, bool canResume)
        {
            Suspend(true);
            title.text = heading; details.text = message;
            resume.gameObject.SetActive(canResume);
            panel.SetActive(true);
            if (canResume) resume.Select(); else retry.Select();
        }
        public void Resume()
        {
            if (terminal || choosing) return;
            paused = false;
            if (menu != null) menu.Hide();
            panel.SetActive(false);
            Suspend(false);
        }
        private void MainMenu()
        {
            if (terminal || choosing) return;
            session.ReturnToMainMenu();
        }
        private void Quit() => Application.Quit();
        public void Retry() { if (session != null) session.Retry(); }
        private void Died()
        {
            terminal = true;
            ShowPanel("Try again", $"Your run has ended.\n\nKILLS  {session.State.KillCount}   XP  {session.State.XP}\nUPGRADES  {session.State.Progression.Selections}\n\nRoute discoveries are kept.\nStart again at Pohorje.", false);
        }
        public void Victory()
        {
            if (terminal) return;
            terminal = true;
            ShowPanel("Danes je sobota.", $"V1 RUN COMPLETE\nKills: {session.State.KillCount}   XP: {session.State.XP}\nUpgrades: {session.State.Progression.Selections}\nRetry starts a new run with your discovered C routes.", false);
        }
        private void ShowUpgrade()
        {
            var state = session.State;
            if (!state.Progression.CreateOffer(random)) return;
            offerVersion = state.Progression.OfferVersion;
            offer = new UpgradeId[state.Progression.OfferCount];
            for (int i = 0; i < offer.Length; i++) offer[i] = state.Progression.Offer(i);
            if (offer.Length == 0) { Debug.LogError("Expected at least one eligible V1 upgrade.", this); return; }
            choosing = true;
            Suspend(true, PauseReason.Upgrade);
            for (int i = 0; i < offer.Length; i++)
            {
                var kind = offer[i];
                bool recovery = kind == UpgradeId.EmergencySnack || kind == UpgradeId.BatteryCharge;
                upgrades.SetCard(i, Name(kind), Description(kind), recovery ? "RECOVERY" : "UPGRADE", recovery);
            }
            upgrades.ShowOffer(offer.Length);
        }
        public void Choose(int index)
        {
            if (!choosing || index < 0 || index >= offer.Length || !session.State.Progression.TrySelect(index, offerVersion)) return;
            upgrades.Hide();
            choosing = false;
            if (session.State.Progression.HasPendingSelection) ShowUpgrade();
            else Suspend(false, PauseReason.Upgrade);
        }
        private static string Name(UpgradeId kind)
        {
            switch (kind)
            {
                case UpgradeId.RaveOverdrive: return "RAVE OVERDRIVE";
                case UpgradeId.TurboSkiro: return "TURBO SKIRO";
                case UpgradeId.BiggerBattery: return "BIGGER BATTERY";
                case UpgradeId.DoubleTrouble: return "DOUBLE TROUBLE";
                case UpgradeId.PanicMode: return "PANIC MODE";
                case UpgradeId.EmergencySnack: return "EMERGENCY SNACK";
                default: return "BATTERY CHARGE";
            }
        }
        private static string Description(UpgradeId kind)
        {
            switch (kind)
            {
                case UpgradeId.RaveOverdrive: return "+30% original laser damage. Stacks up to three times.";
                case UpgradeId.TurboSkiro: return "+20% original riding speed. Boost speed stays the same.";
                case UpgradeId.BiggerBattery: return "+40 battery capacity. Keeps the current charge percentage.";
                case UpgradeId.DoubleTrouble: return "A second full-damage laser hit each firing cycle.";
                case UpgradeId.PanicMode: return "Boost near chasing Guilt: +25% boost speed and +50% duration.";
                case UpgradeId.EmergencySnack: return "Restore 30 HP.";
                default: return "Restore 25 battery charge.";
            }
        }
    }
}
