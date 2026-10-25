using Boki.Core;
using Boki.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Boki.Upgrades
{
    [DisallowMultipleComponent]
    public sealed class XpPickup : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float attractionRadius = 3f;
        [SerializeField, Min(0.1f)] private float attractionSpeed = 12f;
        private Transform visual;
        private PlayerHealth target;
        private PlayerAudio targetAudio;
        private float hoverTime;
        private bool collected;

        public static void Spawn(Vector3 position, Scene scene)
        {
            var pack = new GameObject("XP Orb");
            SceneManager.MoveGameObjectToScene(pack, scene);
            pack.transform.position = position + Vector3.up * 0.65f;
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Orb Visual";
            sphere.transform.SetParent(pack.transform, false);
            sphere.transform.localScale = Vector3.one * 0.3f;
            Collider visualCollider = sphere.GetComponent<Collider>();
            visualCollider.enabled = false;
            Destroy(visualCollider);
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(0.65f, 0.95f, 0.15f));
            var renderer = sphere.GetComponent<Renderer>();
            renderer.SetPropertyBlock(block);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var trigger = pack.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            var body = pack.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var pickup = pack.AddComponent<XpPickup>();
            pickup.visual = sphere.transform;
            pickup.hoverTime = Random.Range(0f, Mathf.PI * 2f);
            trigger.radius = pickup.attractionRadius;
        }

        private void OnTriggerEnter(Collider other) => AcquireTarget(other);
        private void OnTriggerStay(Collider other) => AcquireTarget(other);

        private void AcquireTarget(Collider other)
        {
            var session = RunSession.Instance;
            if (collected || target != null || session == null || !session.IsGameplayActive
                || session.State.Progression.IsMaxLevel) return;
            PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
            if (player == null || !player.UsesRunSession || !player.IsAlive) return;
            target = player;
            targetAudio = player.GetComponent<PlayerAudio>();
        }

        private void Update()
        {
            var session = RunSession.Instance;
            if (collected || session == null || !session.IsGameplayActive) return;
            if (session.State.Progression.IsMaxLevel)
            {
                Destroy(gameObject);
                return;
            }
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;
            if (target == null)
            {
                hoverTime += deltaTime * 2.5f;
                if (visual != null) visual.localPosition = Vector3.up * (Mathf.Sin(hoverTime) * 0.12f);
                return;
            }
            if (!target.isActiveAndEnabled || !target.IsAlive)
            {
                target = null;
                targetAudio = null;
                return;
            }
            if (visual != null) visual.localPosition = Vector3.MoveTowards(visual.localPosition, Vector3.zero, deltaTime);
            Vector3 destination = target.transform.position + Vector3.up * 0.8f;
            transform.position = Vector3.MoveTowards(transform.position, destination, attractionSpeed * deltaTime);
            if ((transform.position - destination).sqrMagnitude > 0.35f * 0.35f) return;
            if (!session.State.Progression.AddXp()) return;
            targetAudio?.PlayXpCollection();
            collected = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
