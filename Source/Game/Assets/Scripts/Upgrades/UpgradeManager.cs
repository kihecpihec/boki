using System;
using Boki.Core;
using Boki.Player;
using Boki.UI;
using UnityEngine;

namespace Boki.Upgrades
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class UpgradeManager : MonoBehaviour
    {
        [SerializeField] private UpgradeScreen screen;
        [SerializeField] private PlayerHealth player;
        [SerializeField] private GameObject[] batteryModules;
        [SerializeField] private GameObject[] laserModules;
        [SerializeField] private GameObject secondEmitter;
        private RunSession session;
        private readonly System.Random random = new System.Random();
        private CursorLockMode previousLock;
        private bool previousVisible;
        private bool showing;
        private int shownVersion;
        private int lastChoiceFrame = -1;

        private void OnEnable()
        {
            if (screen == null || player == null || !player.UsesRunSession)
            {
                Debug.LogError("Assign UpgradeManager screen and a run-bound player.", this);
                enabled = false;
                return;
            }
            session = RunSession.Ensure();
            session.State.Changed += Refresh;
            player.Died += Refresh;
            screen.ChoiceRequested.AddListener(Choose);
        }

        private void Start() => Refresh();
        private void OnDisable()
        {
            if (session != null) session.State.Changed -= Refresh;
            if (player != null) player.Died -= Refresh;
            if (screen != null) screen.ChoiceRequested.RemoveListener(Choose);
            Close();
        }

        private void Refresh()
        {
            var state = session.State.Progression;
            ApplyModules(batteryModules, state.Count(UpgradeId.BiggerBattery));
            ApplyModules(laserModules, state.Count(UpgradeId.RaveOverdrive));
            if (secondEmitter != null) secondEmitter.SetActive(state.HasDoubleTrouble);
            if (!player.IsAlive || !state.HasPendingSelection) { Close(); return; }
            if (!showing)
            {
                previousLock = Cursor.lockState;
                previousVisible = Cursor.visible;
                showing = true;
            }
            // No clear between queued selections: combat and pressure stay frozen.
            session.SetPaused(PauseReason.Upgrade, true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (!state.CreateOffer(random)) return;
            shownVersion = state.OfferVersion;
            for (int i = 0; i < state.OfferCount; i++)
            {
                UpgradeId id = state.Offer(i);
                int cap = UpgradeState.Cap(id);
                bool recovery = id == UpgradeId.EmergencySnack || id == UpgradeId.BatteryCharge;
                string tag = recovery ? "RECOVERY" : $"UPGRADE · {state.Count(id)}/{cap}";
                screen.SetCard(i, Title(id), Description(id), tag, recovery);
            }
            screen.ShowOffer(state.OfferCount);
        }

        private static void ApplyModules(GameObject[] modules, int count)
        {
            if (modules == null) return;
            for (int i = 0; i < modules.Length; i++)
                if (modules[i] != null) modules[i].SetActive(i < count);
        }

        private void Choose(int index)
        {
            if (!showing || lastChoiceFrame == Time.frameCount) return;
            lastChoiceFrame = Time.frameCount;
            // TrySelect emits Changed after applying the effect; Refresh presents the next queue item.
            if (!session.State.Progression.TrySelect(index, shownVersion)) Refresh();
        }

        private void Close()
        {
            if (!showing) return;
            showing = false;
            if (screen != null) screen.Hide();
            if (session != null)
            {
                session.SetPaused(PauseReason.Upgrade, false);
                if (session.State.Pause.IsPaused) return;
            }
            Cursor.lockState = player != null && player.IsAlive ? previousLock : CursorLockMode.None;
            Cursor.visible = player == null || !player.IsAlive || previousVisible;
        }

        private static string Title(UpgradeId id)
        {
            switch (id)
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

        private string Description(UpgradeId id)
        {
            var state = session.State;
            switch (id)
            {
                case UpgradeId.RaveOverdrive: return $"+30% base laser damage (total +{30 * (state.Progression.Count(id) + 1)}%)";
                case UpgradeId.TurboSkiro: return "+20% base riding speed. Boost speed unchanged.";
                case UpgradeId.BiggerBattery: return "+40% base capacity. Keep current charge percentage.";
                case UpgradeId.DoubleTrouble: return "A second full-damage hit each firing cycle.";
                case UpgradeId.PanicMode: return "Boost near chasing Guilt: +25% speed, +50% duration.";
                case UpgradeId.EmergencySnack: return $"Restore 30 HP ({state.Health.Current} → {Math.Min(state.Health.Maximum, state.Health.Current + 30)})";
                default: return "+25 battery charge, up to capacity.";
            }
        }
    }
}
