using Boki.Battery;
using Boki.Core;
using Boki.Enemies;
using Boki.Player;
using Boki.Routes;
using UnityEngine;
using UnityEngine.UI;

namespace Boki.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerHud : MonoBehaviour
    {
        [SerializeField] private PlayerHealth health;
        [SerializeField] private LightBattery battery;
        [SerializeField] private ScooterController movement;
        [SerializeField] private GuiltChaser guilt;
        [SerializeField] private Text healthText;
        [SerializeField] private Text batteryText;
        [SerializeField] private Text xpText;
        [SerializeField] private Text boostText;
        [SerializeField] private Text locationText;
        [SerializeField] private Image healthFill;
        [SerializeField] private Image batteryFill;
        [SerializeField] private Image xpFill;
        [SerializeField] private RawImage guiltEdges;
        [SerializeField] private bool screenEffectsEnabled = true;
        [SerializeField, Min(1f)] private float guiltWarningDistance = 15f;
        private Texture2D edgeTexture;
        private float healthPulseAge;
        private int lastHealth = -1, lastMaximum = -1, lastCharge = -1, lastCapacity = -1, lastXp = -1, lastSelections = -1;
        private RouteLevel? lastLevel;
        public bool ScreenEffectsEnabled { get => screenEffectsEnabled; set => screenEffectsEnabled = value; }
        public void BindGuilt(GuiltChaser chaser) => guilt = chaser;

        private void OnEnable()
        {
            if (health == null || battery == null || movement == null || healthText == null
                || batteryText == null || xpText == null || boostText == null || locationText == null
                || healthFill == null || batteryFill == null || xpFill == null || guiltEdges == null)
            {
                Debug.LogError("Assign the player and all display references on PlayerHud. Guilt is optional.", this);
                enabled = false;
                return;
            }
            lastHealth = lastMaximum = lastCharge = lastCapacity = lastXp = lastSelections = -1;
            lastLevel = null;
            edgeTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "HUD Warning Edges", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float edge = Mathf.Max(Mathf.Abs(x / 63f * 2f - 1f), Mathf.Abs(y / 63f * 2f - 1f));
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1f, edge));
                    edgeTexture.SetPixel(x, y, new Color(0.75f, 0.015f, 0.015f, alpha * 0.55f));
                }
            edgeTexture.Apply(false, true);
            guiltEdges.texture = edgeTexture;
            guiltEdges.color = Color.clear;
        }

        private void LateUpdate()
        {
            if (health.Current != lastHealth || health.Maximum != lastMaximum)
            {
                lastHealth = health.Current;
                lastMaximum = health.Maximum;
                healthText.text = $"HP {lastHealth}/{lastMaximum}";
            }
            healthFill.fillAmount = (float)health.Current / Mathf.Max(1, health.Maximum);
            int charge = Mathf.CeilToInt(battery.State.Current);
            int capacity = Mathf.CeilToInt(battery.State.Capacity);
            if (charge != lastCharge || capacity != lastCapacity)
            {
                lastCharge = charge;
                lastCapacity = capacity;
                batteryText.text = charge == 0 ? $"BAT 0/{capacity}  EMPTY" : $"BAT {charge}/{capacity}";
            }
            batteryFill.fillAmount = battery.State.Current / Mathf.Max(1f, battery.State.Capacity);
            var state = RunSession.Instance != null ? RunSession.Instance.State : null;
            int xp = state != null ? state.XP : 0;
            if (xp != lastXp || (state != null && state.Progression.Selections != lastSelections))
            {
                lastXp = xp;
                lastSelections = state != null ? state.Progression.Selections : 0;
                int next = state != null ? state.Progression.NextThreshold : 3;
                int previous = state != null ? state.Progression.PreviousThreshold : 0;
                xpText.text = next > 0 ? $"XP {xp}/{next}" : "MAX LEVEL";
                xpFill.fillAmount = next > previous ? Mathf.Clamp01((float)(xp - previous) / (next - previous)) : 1f;
            }
            RouteLevel level = state != null ? state.ActiveLevel : RouteLevel.Forest;
            if (lastLevel != level)
            {
                lastLevel = level;
                locationText.text = level == RouteLevel.Forest ? "POHORJE" : level == RouteLevel.City ? "MARIBOR" : "FERI";
            }
            string boost = !health.IsAlive ? "BOOST UNAVAILABLE" : movement.BoostRemaining > 0f
                ? "SHIFT: BOOST ACTIVE" : movement.CooldownRemaining > 0f ? "SHIFT: BOOST RECHARGING" : movement.PanicReady ? "PANIC READY" : "SHIFT: BOOST READY";
            if (boostText.text != boost) boostText.text = boost;
            float warning = screenEffectsEnabled && health.IsAlive && guilt != null && guilt.isActiveAndEnabled
                && guilt.Pressure.IsActive && !guilt.Pressure.AwaitingPlacement
                && (guilt.Phase == GuiltPhase.Warning || guilt.Phase == GuiltPhase.Chase)
                ? 1f - Mathf.Clamp01(Vector3.Distance(health.transform.position, guilt.transform.position) / guiltWarningDistance) : 0f;
            bool active = RunSession.Instance == null || RunSession.Instance.IsGameplayActive;
            float healthFraction = (float)health.Current / Mathf.Max(1, health.Maximum);
            float lowHealth = 0f;
            if (screenEffectsEnabled && health.IsAlive && healthFraction <= 0.3f)
            {
                if (active) healthPulseAge += Time.deltaTime;
                float severity = Mathf.InverseLerp(0.3f, 0f, healthFraction);
                float pulse = 0.5f + 0.5f * Mathf.Sin(healthPulseAge * Mathf.PI * 2f * 1.2f);
                lowHealth = Mathf.Lerp(0.3f, 0.65f, severity) + pulse * 0.35f;
            }
            else healthPulseAge = 0f;
            warning = Mathf.Max(warning, lowHealth);
            if (!health.IsAlive) warning = 0f;
            float opacity = screenEffectsEnabled && health.IsAlive ? Mathf.MoveTowards(guiltEdges.color.a, warning, (active ? Time.deltaTime : 0f) * 4f) : 0f;
            guiltEdges.color = new Color(1f, 1f, 1f, opacity);
            guiltEdges.enabled = opacity > 0f;
        }

        private void OnDisable()
        {
            if (guiltEdges != null) { guiltEdges.texture = null; guiltEdges.enabled = false; }
            if (edgeTexture != null) Destroy(edgeTexture);
        }
    }
}
