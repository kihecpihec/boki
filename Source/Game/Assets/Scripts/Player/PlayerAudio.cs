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
        [Header("Recorded voices (optional on legacy scenes)")]
        [SerializeField] private AudioClip[] damageVariations;
        [SerializeField] private AudioClip[] shotVariations;
        [SerializeField] private AudioClip[] guiltVariations;
        [SerializeField] private AudioClip guiltReaction;
        [SerializeField] private AudioClip snack;
        [SerializeField] private AudioClip departureLine;
        [SerializeField, Range(0f, 1f)] private float volume = 0.65f;
        [Header("Mix and repetition")]
        [SerializeField, Range(0f, 1f)] private float mixGain = 0.75f;
        [SerializeField, Range(0f, 1f)] private float motorGain = 0.35f;
        [SerializeField, Range(0f, 1f)] private float laserGain = 0.18f;
        [SerializeField, Range(0f, 1f)] private float damageGain = 0.65f;
        [SerializeField, Range(0f, 1f)] private float warningGain = 0.8f;
        [SerializeField, Range(0f, 1f)] private float guiltGain = 0.5f;
        [SerializeField, Min(0f)] private float laserSoundInterval = 0.5f;
        [SerializeField, Min(0f)] private float xpSoundInterval = 0.35f;
        [SerializeField, Min(0f)] private float guiltVoiceInterval = 3.5f;
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
        private int lastDamage = -1, lastShot = -1, lastGuilt = -1;
        private float guiltVoiceCooldown;
        private bool reactedToGuilt;
        private bool departurePending;
        private float shotCooldown, xpCooldown, damageCooldown;

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
            guiltVoiceCooldown = 0f;
            shotCooldown = xpCooldown = damageCooldown = 0f;
            reactedToGuilt = false;
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
            departurePending = false;
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
            float target = speed > 0.3f ? volume * mixGain * motorGain * Mathf.Lerp(0.12f, 0.45f, speedFraction)
                * (movement.IsGrounded ? 1f : 0.2f) : 0f;
            motorSource.volume = active ? Mathf.MoveTowards(motorSource.volume, target, Time.deltaTime * 2f) : 0f;
            motorSource.pitch = Mathf.Lerp(0.7f, 1.65f, speedFraction);
            if (!active) return;
            if (departurePending && speed > 0.3f)
            {
                departurePending = false;
                PlayVoice(rewardSource, departureLine, 0.85f);
            }
            shotCooldown = Mathf.Max(0f, shotCooldown - Time.deltaTime);
            xpCooldown = Mathf.Max(0f, xpCooldown - Time.deltaTime);
            damageCooldown = Mathf.Max(0f, damageCooldown - Time.deltaTime);
            impactCooldown = Mathf.Max(0f, impactCooldown - Time.deltaTime);
            airTime = movement.IsGrounded ? 0f : airTime + Time.deltaTime;
            int band = BatteryBand();
            if (band < batteryBand) Play(actionSource, band == 0 ? batteryEmpty : batteryLow, warningGain);
            batteryBand = band;
            warningCooldown = Mathf.Max(0f, warningCooldown - Time.deltaTime);
            guiltVoiceCooldown = Mathf.Max(0f, guiltVoiceCooldown - Time.deltaTime);
            if (battery.WarningStrength > 0.1f && warningCooldown <= 0f)
            {
                Play(actionSource, guiltHeartbeat, 0.28f);
                warningCooldown = 1.8f;
            }
            if (battery.WarningStrength > 0.1f && !reactedToGuilt && guiltReaction != null)
            {
                reactedToGuilt = true;
                PlayVoice(rewardSource, guiltReaction, 0.65f);
                guiltVoiceCooldown = guiltReaction.length + 0.2f;
            }
            if (battery.WarningStrength > 0.1f && guiltVoiceCooldown <= 0f)
            {
                AudioClip voice = AudioVariations.Next(null, guiltVariations, ref lastGuilt);
                if (voice != null)
                {
                    PlayVoice(actionSource, voice, guiltGain);
                    guiltVoiceCooldown = Mathf.Max(guiltVoiceInterval, voice.length + 0.35f);
                }
            }
        }

        public void QueueDepartureLine() => departurePending = departureLine != null;

        private int BatteryBand() => battery.State.Current <= 0f ? 0
            : battery.State.Current / battery.State.Capacity <= 0.2f ? 1 : 2;
        private void Play(AudioSource source, AudioClip clip, float gain = 1f)
        {
            if (isActiveAndEnabled && focused && clip != null) source.PlayOneShot(clip, volume * mixGain * gain);
        }
        private void PlayVoice(AudioSource source, AudioClip clip, float gain)
        {
            // Lift recorded Boki voices by about 8 dB without raising movement and weapon effects.
            Play(source, clip, gain * 2.5f);
        }
        private void PlayJump() => Play(actionSource, jump, 0.5f);
        private void PlayBoost() => Play(actionSource, boost, 0.65f);
        private void PlayLanding()
        {
            if (airTime >= 0.1f) Play(actionSource, landing, Mathf.Clamp(airTime, 0.3f, 1f) * 0.5f);
            airTime = 0f;
        }
        private void PlayShot()
        {
            if (shotCooldown > 0f || !focused) return;
            AudioClip clip = AudioVariations.Next(shot, shotVariations, ref lastShot);
            Play(actionSource, clip, laserGain);
            // Only thin the sound events; every laser pulse still fires and deals its normal damage.
            shotCooldown = Mathf.Max(laserSoundInterval, clip.length + 0.08f);
        }
        private void PlayWallImpact(float speed)
        {
            if (impactCooldown > 0f) return;
            Play(actionSource, landing, Mathf.Clamp(speed / 12f, 0.2f, 0.7f) * 0.5f);
            impactCooldown = 0.5f;
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
                    PlayVoice(actionSource, death, 0.9f);
                }
                else if (damageCooldown <= 0f)
                {
                    AudioClip clip = AudioVariations.Next(damage, damageVariations, ref lastDamage);
                    PlayVoice(actionSource, clip, damageGain);
                    damageCooldown = Mathf.Max(0.55f, clip.length + 0.1f);
                }
            }
            else if (health.Current > previousHealth && snack != null) PlayVoice(rewardSource, snack, 0.65f);
            previousHealth = health.Current;
        }
        public void PlayBatteryCollection() => Play(rewardSource, batteryPickup, 0.5f);
        public void PlayXpCollection()
        {
            // A cluster collected in one physics step needs one chime, not dozens of voices.
            if (lastXpFrame == Time.frameCount || xpCooldown > 0f) return;
            lastXpFrame = Time.frameCount;
            Play(rewardSource, xpPickup, 0.25f);
            xpCooldown = xpSoundInterval;
        }
    }
}
