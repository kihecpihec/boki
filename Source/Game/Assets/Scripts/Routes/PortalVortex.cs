using Boki.Player;
using UnityEngine;

namespace Boki.Routes
{
    // Whirlpool that grows as the rider nears a portal, then flares while the rider is pulled in.
    // Presentation only; it reads distance alone, never speed, so it cannot hint at Gameplay §1.1.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PortalVortex : MonoBehaviour
    {
        private const float MaximumStep = 0.05f;

        [SerializeField, Min(0.1f)] private float nearDistance = 9f;
        [SerializeField, Min(0.1f)] private float farDistance = 32f;
        [Tooltip("Matches the 4 x 4.4 ring that LevelPortal portals draw, plus room for the rim glow.")]
        [SerializeField] private Vector2 discSize = new Vector2(4.9f, 5.4f);
        [SerializeField] private float discHeight = 2.2f;
        [SerializeField, Min(0f)] private float spinRate = 2.4f;
        private VortexDisc disc;
        private BoxCollider trigger;
        private Transform rider;
        private float spin, entryElapsed, entrySeconds;
        private bool entering;

        private void OnEnable()
        {
            trigger = GetComponent<BoxCollider>();
            disc = VortexDisc.Create("Portal Vortex", transform);
            if (disc == null)
            {
                Debug.LogError("Missing Resources/PortalVortex shader.", this);
                enabled = false;
                return;
            }
            disc.Transform.localPosition = Vector3.up * discHeight;
            disc.Transform.localScale = new Vector3(discSize.x, discSize.y, 1f);
        }

        private void Start()
        {
            var health = FindAnyObjectByType<PlayerHealth>();
            if (health != null) rider = health.transform;
        }

        private void OnDisable()
        {
            if (disc != null) disc.Destroy();
            disc = null;
        }

        // The run is frozen while the rider is pulled in, so the flare advances on unscaled time.
        public void PlayEntry(float seconds)
        {
            entering = true;
            entrySeconds = Mathf.Max(seconds, 0.01f);
            entryElapsed = 0f;
        }

        private void Update()
        {
            float step = entering ? Mathf.Min(Time.unscaledDeltaTime, MaximumStep) : Time.deltaTime;
            float flare = 0f;
            if (entering)
            {
                entryElapsed += step;
                flare = Mathf.SmoothStep(0f, 1f, entryElapsed / entrySeconds);
            }
            float proximity = 0f;
            if (rider != null)
            {
                Vector3 position = rider.position;
                proximity = PortalProximity.Intensity(Vector3.Distance(trigger.ClosestPoint(position), position),
                    nearDistance, farDistance);
            }
            spin += step * spinRate * (1f + 3f * flare);
            float swell = 1f + 0.3f * flare;
            disc.Transform.localScale = new Vector3(discSize.x * swell, discSize.y * swell, 1f);
            disc.Show(proximity * (1f + flare), spin);
        }
    }
}
