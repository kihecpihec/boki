using Boki.Battery;
using Boki.Combat;
using Boki.Core;
using UnityEngine;

namespace Boki.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerAudio : MonoBehaviour
    {
        [SerializeField] private ScooterController movement;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private AutoLaser laser;
        [SerializeField] private LightBattery battery;
        [SerializeField] private AudioSource motorSource;
        [SerializeField] private AudioSource actionSource;
        [SerializeField] private AudioSource rewardSource;
        [SerializeField] private AudioClip motor;
        [SerializeField] private AudioClip jump;
        [SerializeField] private AudioClip landing;
        [SerializeField] private AudioClip boost;
        [SerializeField] private AudioClip shot;
        [SerializeField] private AudioClip batteryPickup;
        [SerializeField] private AudioClip xpPickup;
        [SerializeField] private AudioClip damage;
        [SerializeField] private AudioClip death;
        [SerializeField] private AudioClip batteryLow;
        [SerializeField] private AudioClip batteryEmpty;
        [SerializeField] private AudioClip guiltHeartbeat;
        [SerializeField, Range(0f, 1f)] private float volume = 0.65f;
        private int previousHealth;
        private int batteryBand;
        private float warningCooldown;
        private float airTime;
        private float impactCooldown;
        private bool subscribed;
        private bool paused;
        private bool rewardsPaused;
        private int lastXpFrame = -1;
        private bool focused = true;

        private void OnEnable()
        {
            if (laser == null) laser = GetComponent<AutoLaser>();
            if (movement == null || health == null || battery == null
                || motorSource == null || actionSource == null || rewardSource == null
                || motor == null || jump == null || landing == null || boost == null || shot == null
                || batteryPickup == null || xpPickup == null || damage == null || death == null
                || batteryLow == null || batteryEmpty == null || guiltHeartbeat == null)
            {
                Debug.LogError("Assign PlayerAudio gameplay references, three sources and all sound clips.", this);
                enabled = false;
                return;
            }
            previousHealth = health.Current;
            batteryBand = BatteryBand();
            warningCooldown = airTime = impactCooldown = 0f;
            paused = false;
            rewardsPaused = false;
            lastXpFrame = -1;
            focused = Application.isFocused;
            motorSource.clip = motor;
            motorSource.loop = true;
            motorSource.volume = 0f;
            motorSource.Play();
            movement.Jumped += PlayJump;
            movement.Boosted += PlayBoost;
            movement.Landed += PlayLanding;
            movement.WallImpacted += PlayWallImpact;
            if (laser != null) laser.Fired += PlayShot;
            health.Changed += OnHealthChanged;
            subscribed = true;
        }

        private void OnDisable()
        {
            if (subscribed)
            {
                movement.Jumped -= PlayJump;
                movement.Boosted -= PlayBoost;
                movement.Landed -= PlayLanding;
                movement.WallImpacted -= PlayWallImpact;
                if (laser != null) laser.Fired -= PlayShot;
                health.Changed -= OnHealthChanged;
            }
            subscribed = false;
            if (motorSource != null) motorSource.Stop();
            if (actionSource != null) actionSource.Stop();
            if (rewardSource != null) rewardSource.Stop();
        }

        private void OnApplicationFocus(bool value) => focused = value;

        private void LateUpdate()
        {
            var session = RunSession.Instance;
            bool active = focused && Time.deltaTime > 0f
                && (session == null || session.IsGameplayActive) && health.IsAlive;
            // A death cue must finish even if the death screen pauses the run.
            bool shouldPause = !focused || (health.IsAlive && (Time.deltaTime <= 0f
                || (session != null && !session.IsGameplayActive)));
            if (shouldPause != paused)
            {
                if (shouldPause) { motorSource.Pause(); actionSource.Pause(); }
                else { motorSource.UnPause(); actionSource.UnPause(); }
                paused = shouldPause;
            }
            // Let the final XP chime finish when that pickup opens an upgrade offer.
            bool pauseRewards = !focused || (session != null && (session.IsLoading
                || session.State.Pause.Contains(PauseReason.Menu)
                || session.State.Pause.Contains(PauseReason.ScriptedSequence)));
            if (pauseRewards != rewardsPaused)
            {
                if (pauseRewards) rewardSource.Pause(); else rewardSource.UnPause();
                rewardsPaused = pauseRewards;
            }
            float speed = active && movement.isActiveAndEnabled ? movement.HorizontalSpeed : 0f;
            float speedFraction = Mathf.Clamp01(speed / Mathf.Max(1f, movement.BoostSpeed));
            float target = speed > 0.3f ? volume * Mathf.Lerp(0.12f, 0.45f, speedFraction)
                * (movement.IsGrounded ? 1f : 0.2f) : 0f;
            motorSource.volume = active ? Mathf.MoveTowards(motorSource.volume, target, Time.deltaTime * 2f) : 0f;
            motorSource.pitch = Mathf.Lerp(0.7f, 1.65f, speedFraction);
            if (!active) return;
            impactCooldown = Mathf.Max(0f, impactCooldown - Time.deltaTime);
            airTime = movement.IsGrounded ? 0f : airTime + Time.deltaTime;
            int band = BatteryBand();
            if (band < batteryBand) Play(actionSource, band == 0 ? batteryEmpty : batteryLow, 0.7f);
            batteryBand = band;
            warningCooldown = Mathf.Max(0f, warningCooldown - Time.deltaTime);
            if (battery.WarningStrength > 0.1f && warningCooldown <= 0f)
            {
                Play(actionSource, guiltHeartbeat, 0.7f);
                warningCooldown = 1.2f;
            }
        }

        private int BatteryBand() => battery.State.Current <= 0f ? 0
            : battery.State.Current / battery.State.Capacity <= 0.2f ? 1 : 2;
        private void Play(AudioSource source, AudioClip clip, float gain = 1f)
        {
            if (isActiveAndEnabled && focused) source.PlayOneShot(clip, volume * gain);
        }
        private void PlayJump() => Play(actionSource, jump, 0.8f);
        private void PlayBoost() => Play(actionSource, boost, 0.8f);
        private void PlayLanding()
        {
            if (airTime >= 0.1f) Play(actionSource, landing, Mathf.Clamp(airTime, 0.3f, 1f));
            airTime = 0f;
        }
        private void PlayShot() => Play(actionSource, shot, 0.45f);
        private void PlayWallImpact(float speed)
        {
            if (impactCooldown > 0f) return;
            Play(actionSource, landing, Mathf.Clamp(speed / 12f, 0.2f, 0.7f));
            impactCooldown = 0.2f;
        }
        private void OnHealthChanged()
        {
            if (health.Current < previousHealth)
            {
                if (!health.IsAlive)
                {
                    motorSource.Stop();
                    actionSource.Stop();
                    rewardSource.Stop();
                    Play(actionSource, death);
                }
                else Play(actionSource, damage, 0.8f);
            }
            previousHealth = health.Current;
        }
        public void PlayBatteryCollection() => Play(rewardSource, batteryPickup, 0.8f);
        public void PlayXpCollection()
        {
            // A cluster collected in one physics step needs one chime, not dozens of voices.
            if (lastXpFrame == Time.frameCount) return;
            lastXpFrame = Time.frameCount;
            Play(rewardSource, xpPickup, 0.65f);
        }
    }
}
