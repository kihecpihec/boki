using UnityEngine;

namespace Boki.Core
{
    public static class AudioVariations
    {
        public static AudioClip Next(AudioClip fallback, AudioClip[] clips, ref int previous)
        {
            if (clips == null || clips.Length == 0) return fallback;
            int index = Random.Range(0, clips.Length > 1 && previous >= 0 ? clips.Length - 1 : clips.Length);
            if (clips.Length > 1 && previous >= 0 && index >= previous) index++;
            previous = index;
            return clips[index] != null ? clips[index] : fallback;
        }
    }
}
