using System;

namespace Boki.Intro
{
    // Timing for the opening crawl that plays before the Bellevue prologue (Story §3 OPENING CRAWL).
    // Phase order is driven by IntroTimeline; these are the pure curves inside the phases.
    public static class OpeningCrawlRules
    {
        public const int Preface = 0;
        public const int Title = 1;
        public const int Crawl = 2;
        public const int Outro = 3;

        // The text starts with its top edge on the near edge of the plane and is finished once its last line has
        // travelled the whole plane away from the viewer.
        public static float CrawlSeconds(float textHeight, float travelHeight, float speed)
        {
            if (!(textHeight > 0f) || float.IsInfinity(textHeight)) throw new ArgumentOutOfRangeException(nameof(textHeight));
            if (!(travelHeight > 0f) || float.IsInfinity(travelHeight)) throw new ArgumentOutOfRangeException(nameof(travelHeight));
            if (!(speed > 0f) || float.IsInfinity(speed)) throw new ArgumentOutOfRangeException(nameof(speed));
            return (textHeight + travelHeight) / speed;
        }

        public static float ScrollOffset(float seconds, float speed)
        {
            if (!(speed >= 0f) || float.IsInfinity(speed)) throw new ArgumentOutOfRangeException(nameof(speed));
            return seconds > 0f ? seconds * speed : 0f;
        }

        // 0 -> 1 across [from, to], clamped. Smooth at both ends.
        public static float Ramp(float value, float from, float to)
        {
            if (!(to > from)) throw new ArgumentException("to must be greater than from.");
            float t = Math.Min(1f, Math.Max(0f, (value - from) / (to - from)));
            return t * t * (3f - 2f * t);
        }

        // Opacity of a card that fades in, holds, and fades out over a span of the given length.
        // Short spans shrink both fades equally so the card never exceeds 1 or jumps.
        public static float Envelope(float elapsed, float seconds, float fadeIn, float fadeOut)
        {
            if (!(seconds > 0f)) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (fadeIn < 0f || fadeOut < 0f) throw new ArgumentOutOfRangeException(nameof(fadeIn));
            float fades = fadeIn + fadeOut;
            if (fades > seconds)
            {
                float shrink = seconds / fades;
                fadeIn *= shrink;
                fadeOut *= shrink;
            }
            float rise = fadeIn > 0f ? Ramp(elapsed, 0f, fadeIn) : (elapsed >= 0f ? 1f : 0f);
            float fall = fadeOut > 0f ? 1f - Ramp(elapsed, seconds - fadeOut, seconds) : (elapsed <= seconds ? 1f : 0f);
            return Math.Min(rise, fall);
        }
    }
}
