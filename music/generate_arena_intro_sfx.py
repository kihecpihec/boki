#!/usr/bin/env python3
"""Procedurally renders the first-arena cutscene's sound effects.

Story §3 LEVEL 1 and the first Pohorje arena: Boki rides in, hears something in the dark and freezes, then an enemy
creeps out of the dark toward him. Three cues here: the twig snap and rustle that Boki hears, one dragging footfall
of the creeper (the cutscene plays it on every step, pitch-varied), and the stinger when it lunges. The voices are
the recorded kaj_je_to_blo.wav and the enemies' greeting.wav. Everything is synthesized from seeded noise and sine
partials, so the files regenerate byte for byte and carry no samples or licenses.

Usage: python3 music/generate_arena_intro_sfx.py [output_dir]
Default output: Game/Assets/Audio/Sfx
Requires numpy and scipy.
"""
import sys
import wave
from pathlib import Path

import numpy as np

from ambience_synth import band, lowpass, smooth_curve

SR = 48000
PEAK = 0.5  # same ceiling as the other cutscene effects


def t(seconds):
    return np.arange(int(SR * seconds)) / SR


def twig_snap(rng):
    """A dry twig breaking: a sharp crack and two splinter ticks after it."""
    n = int(SR * 0.12)
    tt = np.arange(n) / SR
    out = band(rng.standard_normal(n), 1200, 7000) * np.exp(-tt / 0.006) * np.minimum(1, tt / 0.0005)
    for at, gain in ((0.018, 0.5), (0.041, 0.3)):
        i = int(SR * at)
        k = n - i
        out[i:] += band(rng.standard_normal(k), 2500, 8000) * np.exp(-np.arange(k) / (SR * 0.004)) * gain
    return out


def rustle():
    """What Boki hears: a twig snaps off in the dark and something moves through the undergrowth, far off."""
    seconds = 1.5
    rng = np.random.default_rng(11)
    n = int(SR * seconds)
    tt = t(seconds)
    out = np.zeros(n)
    snap = twig_snap(rng)
    out[:snap.size] += snap
    undergrowth = band(rng.standard_normal(n), 180, 1500) * smooth_curve(n, 9, rng) ** 2
    out += 0.42 * undergrowth * np.clip((tt - 0.12) / 0.35, 0, 1) * np.clip((seconds - tt) / 0.6, 0, 1)
    # The snap would otherwise set the level and bury the rustle; squash the peaks, then soften the top for distance.
    return lowpass(np.tanh(1.6 * out), 5200)


def footstep():
    """One creeping footfall: a dull thud, damp ground crunching under it, then the foot dragging a little."""
    seconds = 0.55
    rng = np.random.default_rng(23)
    tt = t(seconds)
    thud = np.sin(2 * np.pi * np.cumsum(52 + 38 * np.exp(-30 * tt)) / SR) * np.exp(-26 * tt)
    crunch = band(rng.standard_normal(tt.size), 250, 1900) * np.exp(-tt / 0.045) * np.minimum(1, tt / 0.002)
    swell = (np.clip((tt - 0.1) / 0.1, 0, 1) * np.clip((0.42 - tt) / 0.2, 0, 1)) ** 1.5
    drag = band(rng.standard_normal(tt.size), 600, 3600) * swell * smooth_curve(tt.size, 30, rng)
    out = 0.9 * thud + 0.55 * crunch + 0.22 * drag
    return out * np.minimum(1, (seconds - tt) / 0.05)


def stinger():
    """The lunge: a sharp inhale, then the hit (boom and crack) and a harsh falling growl over a long tail."""
    seconds = 1.9
    hit = 0.3  # seconds into the clip; the cutscene's flash lands on it
    rng = np.random.default_rng(41)
    n = int(SR * seconds)
    tt = t(seconds)
    after = np.clip(tt - hit, 0, None)
    live = tt >= hit
    inhale = band(rng.standard_normal(n), 500, 3200) * np.clip(tt / hit, 0, 1) ** 2 * ~live
    boom = np.sin(2 * np.pi * np.cumsum(48 + 120 * np.exp(-12 * after)) / SR) * np.exp(-3.0 * after) * live
    crack = band(rng.standard_normal(n), 400, 5000) * np.exp(-after / 0.05) * live
    phase = 2 * np.pi * np.cumsum(150 - 60 * np.clip(after, 0, 1)) / SR
    rough = 0.65 + 0.35 * np.sin(2 * np.pi * 31 * tt)
    growl = lowpass(np.tanh(2.2 * (np.sin(phase) + 0.5 * np.sin(2 * phase + 0.6))), 1800) * rough * np.exp(-after / 0.55) * live
    out = 0.55 * inhale + 1.0 * boom + 0.5 * crack + 0.5 * growl
    return out * np.minimum(1, (seconds - tt) / 0.15)


def write_wav(path, sig):
    sig = sig - np.mean(sig)
    sig = sig / np.max(np.abs(sig)) * PEAK
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((sig * 32767).astype("<i2").tobytes())
    print(f"Wrote {path} ({sig.size / SR * 1000:.0f} ms)")


def main():
    out_dir = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[1] / "Game/Assets/Audio/Sfx"
    out_dir.mkdir(parents=True, exist_ok=True)
    write_wav(out_dir / "arena_intro_rustle.wav", rustle())
    write_wav(out_dir / "arena_intro_step.wav", footstep())
    write_wav(out_dir / "arena_intro_stinger.wav", stinger())


if __name__ == "__main__":
    main()
