#!/usr/bin/env python3
"""Procedurally renders the FERI ending room tone (Game/Assets/Audio/Sfx/ending_ambience.ogg).

Story §3 FINAL BOSS victory plus the "unusually quiet space" of §6: the boss music is gone and the classroom
is nearly silent. While the red light is still on (EndingActing.ShrinkAt, 0.9 s) a low dissonant drone
hangs in the room; when the professor deflates it drains away and the plain quiet of the classroom takes over:
the hum of the fluorescent tubes and a wall clock ticking. Nothing from outdoors: no wind and no birds.
The bed is 25 s, a little longer than the ending (24.4 s). EndingCutscene fades it out when the ending finishes.

Usage: python3 music/generate_ending_ambience.py [output.ogg]
Default output: Game/Assets/Audio/Sfx/ending_ambience.ogg
Requires numpy, scipy and ffmpeg (libvorbis) on PATH.
"""
import sys
from pathlib import Path

import numpy as np

from ambience_synth import SR, band, curve, encode_ogg, lowpass, place, reverb, smooth_curve, tvec, unit

SECONDS = 25.0


def drone(rng):
    """The boss's last breath in the room: a sub tritone that beats slowly, with a dark buzzing saw on top."""
    n = int(SR * SECONDS)
    tt = tvec(SECONDS)
    sub = np.sin(2 * np.pi * 55.0 * tt) + 0.8 * np.sin(2 * np.pi * 77.8 * tt + 0.7) + 0.5 * np.sin(2 * np.pi * 58.2 * tt)
    saw = 2 * ((110.0 * tt) % 1) - 1
    buzz = lowpass(saw, 320, 2) * (0.55 + 0.45 * np.sin(2 * np.pi * 5.5 * tt))
    level = curve(SECONDS, ((0, 0.0), (0.05, 0.7), (0.8, 1.0), (1.3, 0.55), (2.4, 0.0), (SECONDS, 0.0)))
    return (sub * 0.5 + buzz * 0.55) * level


def fluorescent_hum(rng):
    """Mains hum at 100 Hz (50 Hz supply, full-wave) with a few harmonics and a faint ballast buzz that wanders."""
    n = int(SR * SECONDS)
    tt = tvec(SECONDS)
    hum = np.zeros(n)
    for k, gain in ((1, 1.0), (2, 0.42), (3, 0.3), (4, 0.12), (6, 0.07)):
        hum += gain * np.sin(2 * np.pi * 100.0 * k * tt + k)
    buzz = unit(band(rng.standard_normal(n), 2400, 5200)) * (0.04 + 0.05 * smooth_curve(n, 0.4, rng))
    return hum * 0.04 + buzz * 0.01


def tick(accent, rng):
    """A wall clock: a dry click with a small wooden knock; the 'tock' is a little lower."""
    n = int(SR * 0.12)
    tt = np.arange(n) / SR
    click = band(rng.standard_normal(n), 1800, 5200) * np.exp(-tt / 0.0025)
    knock = np.sin(2 * np.pi * (1000 if accent else 820) * tt) * np.exp(-tt / 0.012)
    return (click * 0.5 + knock * 0.7) * np.minimum(1, tt / 0.0006)


def render():
    rng = np.random.default_rng(2031)
    n = int(SR * SECONDS)
    mix = np.zeros((2, n))

    d = drone(rng)
    mix += np.stack([d, d]) * 0.3

    # The room tone is there under the drone, and comes up as it drains away.
    room_level = curve(SECONDS, ((0, 0.35), (1.0, 0.45), (2.6, 1.0), (SECONDS, 1.0)))
    hum = fluorescent_hum(rng) * room_level
    mix += np.stack([hum, hum])

    # The clock starts ticking once it is quiet: one tick a second, left of the room.
    ticks = np.zeros((2, n))
    for k, at in enumerate(np.arange(2.7, SECONDS - 0.2, 1.0)):
        place(ticks, tick(k % 2 == 0, rng), at, -0.35, 0.16)
    mix += reverb(ticks, 0.45, 0.3, rng, damp=6000)

    mix *= np.minimum(1, tvec(SECONDS) / 0.05)
    mix *= np.minimum(1, (SECONDS - tvec(SECONDS)) / 0.5)
    return mix


def main():
    out_path = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[1] / "Game/Assets/Audio/Sfx/ending_ambience.ogg"
    out_path.parent.mkdir(parents=True, exist_ok=True)
    encode_ogg(render(), out_path, peak=0.5)


if __name__ == "__main__":
    main()
