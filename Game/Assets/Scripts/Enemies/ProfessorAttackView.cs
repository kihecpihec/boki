using Boki.Core;
using UnityEngine;

namespace Boki.Enemies
{
    // Presentation follows attack events; the boss remains the only authority for timing and damage.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProfessorBoss))]
    public sealed class ProfessorAttackView : MonoBehaviour
    {
        private const int Segments = 64;
        [SerializeField] private Transform visual;
        [SerializeField] private LineRenderer warning;
        [SerializeField] private LineRenderer countdown;
        [SerializeField] private LineRenderer shockwave;
        [SerializeField] private LineRenderer castPulse;
        private ProfessorBoss boss;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private float warningAge;
        private float impactAge = 1f;
        private float castAge = 1f;
        private static readonly Color Red = new Color(1f, 0.08f, 0.03f);
        private static readonly Color Amber = new Color(1f, 0.65f, 0.12f);

        private void OnEnable()
        {
            boss = GetComponent<ProfessorBoss>();
            if (visual == null || warning == null || countdown == null || shockwave == null || castPulse == null)
            {
                Debug.LogError("Assign the visual and four attack lines on ProfessorAttackView.", this);
                enabled = false;
                return;
            }
            restPosition = visual.localPosition;
            restRotation = visual.localRotation;
            boss.BarrageSheetFired += OnSheet;
            boss.SlamTelegraphStarted += OnWarning;
            boss.SlamLanded += OnImpact;
            Hide();
        }

        private void OnDisable()
        {
            boss.BarrageSheetFired -= OnSheet;
            boss.SlamTelegraphStarted -= OnWarning;
            boss.SlamLanded -= OnImpact;
            Hide();
        }

        private void OnSheet() => castAge = 0f;
        private void OnWarning() => warningAge = 0f;
        private void OnImpact() => impactAge = 0f;

        private void Update()
        {
            if (!boss.CanAttack) { Hide(); return; }
            if (RunSession.Instance != null && !RunSession.Instance.IsGameplayActive) return;
            float dt = Time.deltaTime;
            castAge += dt;
            impactAge += dt;
            float cast = Mathf.Clamp01(1f - castAge / 0.3f);
            float impact = Mathf.Clamp01(1f - impactAge / 0.45f);
            float windup = 0f;
            if (boss.IsSlamWarning)
            {
                warningAge += dt;
                windup = Mathf.Clamp01(warningAge / BossAttackScheduler.SlamTelegraphSeconds);
                Vector3 center = boss.SlamCenter + Vector3.up * 0.05f;
                Ring(warning, center, boss.SlamRadius, Red, 0.12f + 0.06f * windup);
                Ring(countdown, center + Vector3.up * 0.01f, boss.SlamRadius * (1f - windup), Amber, 0.08f);
            }
            else
            {
                warning.enabled = false;
                countdown.enabled = false;
            }
            if (impact > 0f)
                Ring(shockwave, boss.SlamCenter + Vector3.up * 0.07f,
                    boss.SlamRadius * Mathf.Lerp(1.35f, 0.15f, impact),
                    new Color(1f, 0.3f, 0.05f, impact), 0.22f * impact);
            else shockwave.enabled = false;
            if (cast > 0f)
                Ring(castPulse, transform.position + transform.forward * 0.8f + Vector3.up * 1.3f,
                    Mathf.Lerp(0.65f, 0.12f, cast), new Color(1f, 0.65f, 0.12f, cast), 0.08f);
            else castPulse.enabled = false;
            visual.localPosition = restPosition + Vector3.up * (windup * 0.16f - impact * 0.12f)
                - Vector3.forward * (cast * 0.12f);
            visual.localRotation = restRotation * Quaternion.Euler(-windup * 14f + impact * 18f, cast * -12f, 0f);
        }

        private static void Ring(LineRenderer line, Vector3 center, float radius, Color color, float width)
        {
            line.enabled = true;
            line.startColor = line.endColor = color;
            line.startWidth = line.endWidth = width;
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * (Mathf.PI * 2f / Segments);
                line.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }
        }

        private void Hide()
        {
            if (visual != null) { visual.localPosition = restPosition; visual.localRotation = restRotation; }
            if (warning != null) warning.enabled = false;
            if (countdown != null) countdown.enabled = false;
            if (shockwave != null) shockwave.enabled = false;
            if (castPulse != null) castPulse.enabled = false;
            warningAge = 0f;
            castAge = impactAge = 1f;
        }
    }
}
