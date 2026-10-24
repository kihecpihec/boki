using System;
using Boki.Combat;
using Boki.Core;
using Boki.Player;
using Boki.UI;
using UnityEngine;

namespace Boki.Enemies
{
    // Final boss (Story §3): patrols the arena and alternates Exam Barrage and Deadline Slam.
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
        [SerializeField] private BoxCollider movementFloor;
        [SerializeField, Min(0f)] private float movementSpeed = 1.5f;
        // Story §3 Final Boss: tougher baseline applies to existing serialized encounters too.
        [SerializeField, Min(1f)] private float combatTempo = 1.6f;
        [SerializeField, Min(1f)] private float attackDamageMultiplier = 1.5f;
        [SerializeField, Min(0f)] private float aimLeadSeconds = 0.35f;
        [SerializeField, Min(1)] private int contactDamage = 20;
        [SerializeField, Min(0.1f)] private float contactInterval = 1f;
        [SerializeField, Min(0f)] private float knockbackSpeed = 10f;
        private BossContactAttack contactAttack;
        private Collider bodyCollider;
        private ScooterController targetMovement;
        private Vector3 previousTargetPosition;
        private Vector3 targetVelocity;
        private int patrolCorner;
        private readonly RaycastHit[] movementHits = new RaycastHit[16];
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
            targetMovement = target.GetComponent<ScooterController>();
            bodyCollider = GetComponent<Collider>();
            contactAttack = new BossContactAttack(contactInterval);
            previousTargetPosition = target.transform.position;
            targetVelocity = Vector3.zero;
            scheduler = new BossAttackScheduler(attackGap / combatTempo, sheetInterval / combatTempo,
                enragedSpeedUp, openingDelay);
            var bar = GetComponentInChildren<ProfessorHealthBar>(true);
            if (bar == null)
            {
                var display = new GameObject("Professor Health HUD");
                display.transform.SetParent(transform, false);
                bar = display.AddComponent<ProfessorHealthBar>();
            }
            bar.Bind(this, health, target);
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
            if (Time.deltaTime <= 0f || (RunSession.Instance != null && !RunSession.Instance.IsGameplayActive))
            {
                previousTargetPosition = target.transform.position;
                targetVelocity = Vector3.zero;
                return;
            }
            targetVelocity = Vector3.ClampMagnitude(
                (target.transform.position - previousTargetPosition) / Time.deltaTime, 18f);
            targetVelocity.y = 0f;
            previousTargetPosition = target.transform.position;
            MoveInArena();
            ApplyContactAttack();
            if (!CanAttack) return;
            float fraction = health.Maximum > 0 ? (float)health.Current / health.Maximum : 0f;
            switch (scheduler.Tick(Time.deltaTime, fraction))
            {
                case BossAction.FireSheet: FireSheet(); break;
                case BossAction.StartSlamTelegraph: StartTelegraph(); break;
                case BossAction.SlamImpact: Slam(); break;
            }
        }

        private void MoveInArena()
        {
            if (movementFloor == null || movementSpeed <= 0f) return;
            Bounds bounds = movementFloor.bounds;
            // Leave room for the capsule and perimeter walls, even at the patrol corners.
            Vector3 destination = new Vector3(
                (patrolCorner == 0 || patrolCorner == 3) ? bounds.min.x + 6f : bounds.max.x - 6f,
                transform.position.y,
                patrolCorner < 2 ? bounds.min.z + 3f : bounds.max.z - 3f);
            Vector3 delta = destination - transform.position;
            if (delta.sqrMagnitude < 0.25f) patrolCorner = (patrolCorner + 1) % 4;
            Vector3 step = Vector3.ClampMagnitude(delta, movementSpeed * Time.deltaTime);
            Vector3 position = transform.position;
            int count = Physics.CapsuleCastNonAlloc(position + Vector3.up * 0.5f,
                position + Vector3.up * 1.4f, 0.45f, step.normalized, movementHits,
                step.magnitude + 0.1f, ~0, QueryTriggerInteraction.Ignore);
            bool blocked = count == movementHits.Length;
            for (int i = 0; i < count; i++)
            {
                Transform hit = movementHits[i].collider.transform;
                if (!hit.IsChildOf(transform) && movementHits[i].collider != movementFloor) blocked = true;
            }
            Vector3 next = position + step;
            bool supported = Physics.Raycast(next + Vector3.up * 0.3f, Vector3.down,
                out RaycastHit ground, 0.6f, groundMask, QueryTriggerInteraction.Ignore)
                && ground.collider == movementFloor;
            if (!blocked && supported && next.x > bounds.min.x + 1f && next.x < bounds.max.x - 1f
                && next.z > bounds.min.z + 1f && next.z < bounds.max.z - 1f)
                transform.position = next;
            else patrolCorner = (patrolCorner + 1) % 4;

            Vector3 facing = target.transform.position - transform.position;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(facing), 180f * Time.deltaTime);
        }

        private void OnDisable()
        {
            IsSlamWarning = false;
            if (slamRing != null) slamRing.gameObject.SetActive(false);
        }

        private void ApplyContactAttack()
        {
            Vector3 center = TargetCenter();
            float reach = (targetController != null ? targetController.radius + targetController.skinWidth : 0.35f) + 0.08f;
            Vector3 closest = bodyCollider != null && bodyCollider.enabled
                ? bodyCollider.ClosestPoint(center) : transform.position + Vector3.up * 0.9f;
            bool touching = (closest - center).sqrMagnitude <= reach * reach;
            if (touching && Physics.Linecast(center, closest, groundMask, QueryTriggerInteraction.Ignore)) touching = false;
            if (!contactAttack.Tick(Time.deltaTime, touching)) return;
            target.TakeDamage(contactDamage);
            Vector3 away = target.transform.position - transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.001f) away = transform.forward;
            if (targetMovement != null) targetMovement.ApplyKnockback(away, knockbackSpeed);
        }

        private Vector3 TargetCenter() => targetController != null
            ? target.transform.TransformPoint(targetController.center) : target.transform.position + Vector3.up * 0.9f;

        private void FireSheet()
        {
            Vector3 origin = sheetOrigin.position;
            Vector3 center = TargetCenter() + Vector3.ClampMagnitude(targetVelocity * aimLeadSeconds, 3f);
            EnemyProjectile sheet = Instantiate(sheetPrefab, origin, Quaternion.LookRotation(center - origin));
            sheet.Launch(target, center - origin, Mathf.RoundToInt(sheetDamage * attackDamageMultiplier));
            BarrageSheetFired?.Invoke();
        }

        // Prediction locks when the warning begins; the marked area never tracks the player.
        private void StartTelegraph()
        {
            Vector3 feet = target.transform.position + Vector3.ClampMagnitude(targetVelocity * 0.25f, 2f);
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
            if (grounded && offset.sqrMagnitude <= slamRadius * slamRadius) target.TakeDamage(Mathf.RoundToInt(slamDamage * attackDamageMultiplier));
            SlamLanded?.Invoke();
        }
    }
}
