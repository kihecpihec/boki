using System;
using Boki.Combat;
using Boki.Core;
using Boki.Player;
using UnityEngine;

namespace Boki.Enemies
{
    // Final boss (Story §3): stands at the lectern and alternates Exam Barrage and Deadline Slam.
    // Sequencing and the 0.8 s telegraph come from BossAttackScheduler; this component spawns and damages.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class ProfessorBoss : MonoBehaviour
    {
        [SerializeField] private PlayerHealth target;
        [SerializeField] private EnemyProjectile sheetPrefab;
        [SerializeField] private Transform sheetOrigin;
        [SerializeField] private Transform slamRing;
        [SerializeField] private bool startOnEnable;
        [SerializeField, Min(0.1f)] private float attackGap = 2.5f;
        [SerializeField, Min(0.1f)] private float sheetInterval = 0.6f;
        [SerializeField, Min(1f)] private float enragedSpeedUp = 1.25f;
        [SerializeField, Min(0f)] private float openingDelay = 1.5f;
        [SerializeField, Min(1)] private int sheetDamage = 12;
        [SerializeField, Min(1)] private int slamDamage = 25;
        [SerializeField, Min(0.5f)] private float slamRadius = 4f;
        [SerializeField] private LayerMask groundMask = 1;
        private EnemyHealth health;
        private BossAttackScheduler scheduler;
        private CharacterController targetController;
        private bool fighting;
        private Vector3 slamCenter;
        public bool IsFighting => fighting;
        public bool CanAttack => fighting && health != null && health.IsAlive
            && target != null && target.isActiveAndEnabled && target.IsAlive;
        public bool IsSlamWarning { get; private set; }
        public Vector3 SlamCenter => slamCenter;
        public float SlamRadius => slamRadius;
        public event Action BarrageSheetFired;
        public event Action SlamTelegraphStarted;
        public event Action SlamLanded;

        // Boss entry calls this once; repeated calls are ignored so the fight cannot start twice.
        public void BeginFight()
        {
            if (fighting || !isActiveAndEnabled) return;
            fighting = true;
        }

        private void OnEnable()
        {
            health = GetComponent<EnemyHealth>();
            if (target == null || sheetPrefab == null || slamRing == null)
            {
                Debug.LogError("Assign target, sheet prefab and slam ring on ProfessorBoss.", this);
                enabled = false;
                return;
            }
            if (sheetOrigin == null) sheetOrigin = transform;
            targetController = target.GetComponent<CharacterController>();
            scheduler = new BossAttackScheduler(attackGap, sheetInterval, enragedSpeedUp, openingDelay);
            slamRing.gameObject.SetActive(false);
            fighting = startOnEnable;
        }

        private void Update()
        {
            if (!CanAttack)
            {
                IsSlamWarning = false;
                slamRing.gameObject.SetActive(false);
                return;
            }
            if (RunSession.Instance != null && !RunSession.Instance.IsGameplayActive) return;
            float fraction = health.Maximum > 0 ? (float)health.Current / health.Maximum : 0f;
            switch (scheduler.Tick(Time.deltaTime, fraction))
            {
                case BossAction.FireSheet: FireSheet(); break;
                case BossAction.StartSlamTelegraph: StartTelegraph(); break;
                case BossAction.SlamImpact: Slam(); break;
            }
        }

        private void OnDisable()
        {
            IsSlamWarning = false;
            if (slamRing != null) slamRing.gameObject.SetActive(false);
        }

        private Vector3 TargetCenter() => targetController != null
            ? target.transform.TransformPoint(targetController.center) : target.transform.position + Vector3.up * 0.9f;

        private void FireSheet()
        {
            Vector3 origin = sheetOrigin.position;
            Vector3 center = TargetCenter();
            EnemyProjectile sheet = Instantiate(sheetPrefab, origin, Quaternion.LookRotation(center - origin));
            sheet.Launch(target, center - origin, sheetDamage);
            BarrageSheetFired?.Invoke();
        }

        // The warning marks the ground where Boki stood when it began; moving or boosting out avoids the hit.
        private void StartTelegraph()
        {
            Vector3 feet = target.transform.position;
            slamCenter = Physics.Raycast(feet + Vector3.up, Vector3.down, out RaycastHit hit, 5f, groundMask, QueryTriggerInteraction.Ignore)
                ? hit.point : feet;
            slamRing.position = slamCenter + Vector3.up * 0.03f;
            slamRing.localScale = new Vector3(slamRadius, 1f, slamRadius);
            slamRing.gameObject.SetActive(true);
            IsSlamWarning = true;
            SlamTelegraphStarted?.Invoke();
        }

        private void Slam()
        {
            IsSlamWarning = false;
            slamRing.gameObject.SetActive(false);
            Vector3 offset = target.transform.position - slamCenter;
            // Airborne Boki clears the ring: damage only when close to the ground plane as well as inside the radius.
            bool grounded = offset.y < 1f;
            offset.y = 0f;
            if (grounded && offset.sqrMagnitude <= slamRadius * slamRadius) target.TakeDamage(slamDamage);
            SlamLanded?.Invoke();
        }
    }
}
