using Boki.Core;
using Boki.Enemies;
using Boki.Player;
using Boki.Routes;
using UnityEngine;
using UnityEngine.UI;

namespace Boki.UI
{
    [DisallowMultipleComponent]
    public sealed class PohorjeHudPreview : MonoBehaviour
    {
        [SerializeField] private PlayerHealth health;
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
        // Display milestones only; selecting upgrades belongs to the upgrade system (Story §5).
        [SerializeField] private int[] xpMilestones = { 3, 7, 12, 18, 25, 33 };
        [SerializeField, Min(1f)] private float guiltWarningDistance = 15f;
        private Texture2D edgeTexture;
        private int lastHealth = -1, lastMaximum = -1, lastXp = -2;
        private RouteLevel? lastLevel;
        public bool ScreenEffectsEnabled { get => screenEffectsEnabled; set => screenEffectsEnabled = value; }

        private void OnEnable()
        {
            if (health == null || movement == null || healthText == null
                || batteryText == null || xpText == null || boostText == null || locationText == null
                || healthFill == null || batteryFill == null || xpFill == null || guiltEdges == null)
            {
                Debug.LogError("Assign the player and all display references on PlayerHud. Guilt is optional.", this);
                enabled = false;
                return;
            }
            lastHealth = lastMaximum = -1;
            lastXp = -2;
            lastLevel = null;
            edgeTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "HUD Guilt Edges", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float edge = Mathf.Max(Mathf.Abs(x / 63f * 2f - 1f), Mathf.Abs(y / 63f * 2f - 1f));
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1f, edge));
                    edgeTexture.SetPixel(x, y, new Color(0.3f, 0.015f, 0.015f, alpha * 0.35f));
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
            var state = RunSession.Instance != null ? RunSession.Instance.State : null;
            // This older standalone preview has no light-battery or progression adapter.
            batteryText.text = state != null ? $"BAT {state.Battery}/100" : "BAT --";
            batteryFill.fillAmount = state != null ? Mathf.Clamp01(state.Battery / 100f) : 0f;
            int xp = state != null ? state.XP : -1;
            if (xp != lastXp)
            {
                lastXp = xp;
                int next = 0, previous = 0;
                foreach (int milestone in xpMilestones)
                {
                    if (xp < milestone) { next = milestone; break; }
                    previous = milestone;
                }
                xpText.text = state == null ? "XP --" : next > 0 ? $"XP {xp}/{next}" : "MAX LEVEL";
                xpFill.fillAmount = state == null ? 0f : next > previous ? Mathf.Clamp01((float)(xp - previous) / (next - previous)) : 1f;
            }
            RouteLevel level = state != null ? state.ActiveLevel : RouteLevel.Forest;
            if (lastLevel != level)
            {
                lastLevel = level;
                locationText.text = level == RouteLevel.Forest ? "POHORJE" : level == RouteLevel.City ? "MARIBOR" : "FERI";
            }
            string boost = !health.IsAlive ? "BOOST UNAVAILABLE" : movement.BoostRemaining > 0f
                ? "SHIFT: BOOST ACTIVE" : movement.CooldownRemaining > 0f ? "SHIFT: BOOST RECHARGING" : "SHIFT: BOOST READY";
            if (boostText.text != boost) boostText.text = boost;
            float warning = screenEffectsEnabled && health.IsAlive && guilt != null && guilt.isActiveAndEnabled
                && (guilt.Phase == GuiltPhase.Warning || guilt.Phase == GuiltPhase.Chase)
                ? 1f - Mathf.Clamp01(Vector3.Distance(health.transform.position, guilt.transform.position) / guiltWarningDistance) : 0f;
            float opacity = screenEffectsEnabled ? Mathf.MoveTowards(guiltEdges.color.a, warning, Time.unscaledDeltaTime * 2f) : 0f;
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
