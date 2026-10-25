#!/usr/bin/env python3
"""Procedurally renders the Bellevue prologue ambience (Game/Assets/Audio/Sfx/intro_ambience.ogg).

Story §3 PROLOGUE: "Distant techno bass slowly gives way to wind." Boki wakes on Pohorje the morning after
the rave: the last muffled kicks and a sub bass thud through the fog fade out over the first ten seconds
while wind in the spruces swells in, and a few distant forest birds call. The bed lasts as long as the
intro (IntroActing.ShotSeconds, 20.5 s) and sinks away at the end; IntroCutscene fades it on skip.

Usage: python3 music/generate_intro_ambience.py [output.ogg]
Default output: Game/Assets/Audio/Sfx/intro_ambience.ogg
Requires numpy, scipy and ffmpeg (libvorbis) on PATH.
"""
import sys
from pathlib import Path

import numpy as np

from ambience_synth import (SR, curve, distant, encode_ogg, great_tit, lowpass, place, reverb,
                            robin, tvec, wind)

SECONDS = 21.0
BPM = 120  # the menu track's tempo, so the party Boki left sounds like the same night


def kick():
    t = tvec(0.4)
    freq = 52.0 + 120.0 * np.exp(-t * 34.0)
    body = np.sin(2 * np.pi * np.cumsum(freq) / SR) * np.exp(-t * 8.0)
    return np.tanh(body * 1.6)


def sub_note():
    t = tvec(0.28)
    return np.sin(2 * np.pi * 55.0 * t) * np.exp(-t * 9.0) * np.minimum(1, t / 0.006)


def party_through_the_fog(rng):
    """Four-on-the-floor kicks and an off-beat sub, low-passed so only the thud reaches the hill."""
    n = int(SR * SECONDS)
    beat = 60.0 / BPM
    raw = np.zeros(n)
    k, s = kick(), sub_note()
    for step in range(int(SECONDS / beat)):
        at = int(SR * (0.2 + step * beat))
        for sig, offset, gain in ((k, 0.0, 1.0), (s, beat / 2, 0.45)):
            i = at + int(SR * offset)
            m = min(sig.size, n - i)
            if m > 0:
                raw[i:i + m] += sig[:m] * gain
    muffled = lowpass(raw, 160, 4)
    # Full at the start, gone by ten seconds, dying off in steps like a party fading over the horizon.
    level = curve(SECONDS, ((0, 1.0), (2.5, 0.95), (6.0, 0.35), (9.5, 0.0), (SECONDS, 0.0)))
    return muffled * level


def render():
    rng = np.random.default_rng(2026)
    mix = np.zeros((2, int(SR * SECONDS)))

    # The wind is a soft presence as the bass leaves, peaks while he is still calm, and is gone from under the
    # panic: from the lecture message (9.6 s) the heartbeat and the dread carry the scene, not the weather.
    wind_level = curve(SECONDS, ((0, 0.16), (4, 0.22), (8, 0.34), (9.6, 0.25), (12, 0.08), (SECONDS, 0.0)))
    mix += wind(SECONDS, rng) * wind_level * 0.1

    bass = party_through_the_fog(rng)
    mix += np.stack([bass, bass]) * 0.3

    # Two forest birds while it is still calm and the party is fading: far away, so rolled off and put in the air.
    # None once the panic starts.
    birds = np.zeros_like(mix)
    for at, voice, pan, gain in (
        (5.2, robin(rng), 0.6, 0.11),
        (7.0, great_tit(), -0.55, 0.13),
    ):
        place(birds, distant(voice), at, pan, gain)
    mix += reverb(birds, 1.8, 0.45, rng)

    mix *= curve(SECONDS, ((0, 1.0), (17.5, 1.0), (SECONDS, 0.0)))
    # Fade in over a few milliseconds so a replay does not click.
    mix *= np.minimum(1, tvec(SECONDS) / 0.05)
    return mix


def main():
    out_path = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[1] / "Game/Assets/Audio/Sfx/intro_ambience.ogg"
    out_path.parent.mkdir(parents=True, exist_ok=True)
    # The peak is the first kick; 0.43 keeps the bass at the level it had when the wind was louder.
    encode_ogg(render(), out_path, peak=0.43)


if __name__ == "__main__":
    main()
