using Boki.Core;
using UnityEngine;

namespace Boki.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProfessorBoss))]
    public sealed class ProfessorAttackAudio : MonoBehaviour
    {
        private ProfessorBoss boss;
        private AudioSource source;
        private AudioClip paper;
        private AudioClip slam;
        private bool paused;
        [SerializeField, Range(0f, 1f)] private float volume = 0.4f;
        [SerializeField, Range(0f, 1f)] private float paperGain = 0.3f;
        [SerializeField, Range(0f, 1f)] private float slamGain = 0.75f;
        [SerializeField, Min(0f)] private float paperSoundInterval = 0.7f;
        private float paperCooldown;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = volume;
            // Short synthesized foley keeps every existing encounter audible without asset wiring.
            paper = CreateClip("Professor paper flutter", 0.28f, false);
            slam = CreateClip("Professor deadline impact", 0.55f, true);
        }

        private void OnEnable()
        {
            boss = GetComponent<ProfessorBoss>();
            paperCooldown = 0f;
            boss.BarrageSheetFired += OnPaper;
            boss.SlamLanded += OnSlam;
        }

        private void OnDisable()
        {
            boss.BarrageSheetFired -= OnPaper;
            boss.SlamLanded -= OnSlam;
            source.Stop();
            paused = false;
        }

        private void Update()
        {
            if (!boss.isActiveAndEnabled || !boss.CanAttack) { source.Stop(); return; }
            bool shouldPause = Time.deltaTime <= 0f || (RunSession.Instance != null && !RunSession.Instance.IsGameplayActive);
            if (!shouldPause) paperCooldown = Mathf.Max(0f, paperCooldown - Time.deltaTime);
            if (shouldPause == paused) return;
            paused = shouldPause;
            if (paused) source.Pause();
            else source.UnPause();
        }

        private void OnPaper()
        {
            if (paperCooldown > 0f) return;
            source.PlayOneShot(paper, paperGain);
            paperCooldown = paperSoundInterval;
        }
        private void OnSlam() => source.PlayOneShot(slam, slamGain);

        private static AudioClip CreateClip(string name, float duration, bool impact)
        {
            const int rate = 22050;
            var samples = new float[Mathf.CeilToInt(rate * duration)];
            var random = new System.Random(impact ? 71 : 23);
            float low = 0f;
            float phase = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate;
                float noise = (float)random.NextDouble() * 2f - 1f;
                low += (noise - low) * 0.12f;
                float envelope = Mathf.Min(1f, t / 0.005f) * Mathf.Exp(-t * (impact ? 9f : 14f));
                phase += 2f * Mathf.PI * (45f + 95f * Mathf.Exp(-t * 22f)) / rate;
                samples[i] = impact
                    ? (Mathf.Sin(phase) * 0.65f + low * 0.35f + noise * Mathf.Exp(-t * 65f) * 0.2f) * envelope
                    : (noise - low) * (0.3f + 0.7f * Mathf.Pow(Mathf.Sin(t * 65f), 2f)) * envelope * 0.7f;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            if (paper != null) Destroy(paper);
            if (slam != null) Destroy(slam);
        }
    }
}
