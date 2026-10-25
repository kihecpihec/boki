using Boki.Core;
using Boki.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Boki.Upgrades
{
    [DisallowMultipleComponent]
    public sealed class XpPickup : MonoBehaviour
    {
        private bool collected;

        public static void Spawn(Vector3 position, Scene scene)
        {
            GameObject pack = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pack.name = "XP Pack";
            SceneManager.MoveGameObjectToScene(pack, scene);
            pack.transform.position = position + Vector3.up * 0.35f;
            pack.transform.localScale = Vector3.one * 0.45f;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(0.65f, 0.95f, 0.15f));
            pack.GetComponent<Renderer>().SetPropertyBlock(block);
            pack.GetComponent<SphereCollider>().isTrigger = true;
            var body = pack.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            pack.AddComponent<XpPickup>();
        }

        private void OnTriggerEnter(Collider other) => Collect(other);
        private void OnTriggerStay(Collider other) => Collect(other);
        private void Collect(Collider other)
        {
            var session = RunSession.Instance;
            if (collected || session == null || !session.IsGameplayActive || session.State.Progression.IsMaxLevel) return;
            PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
            if (player == null || !player.UsesRunSession || !player.IsAlive) return;
            if (!session.State.Progression.AddXp()) return;
            player.GetComponent<PlayerAudio>()?.PlayXpCollection();
            collected = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
