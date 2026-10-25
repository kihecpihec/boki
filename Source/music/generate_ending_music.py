#!/usr/bin/env python3
"""Procedurally renders the FERI ending's relief tune (Game/Assets/Audio/Music/ending_music.ogg).

Story §3 FINAL BOSS victory: it was only Saturday, and the professor is a cat. After the boss track and the
dread, a small, bright Saturday-morning tune: pizzicato bass, a plucked chord on the off-beats, a shaker,
and a marimba melody that resolves on C as the closing card appears. 96 BPM, C major, four bars (10 s).
EndingCutscene starts it as the zoom out reveals the cat (EndingActing.ZoomOut, 14.4 s into the ending), so it
ends with the 24.4 s ending and its last chord lands on the closing card (22.0 s).

Usage: python3 music/generate_ending_music.py [output.ogg]
Default output: Game/Assets/Audio/Music/ending_music.ogg
Requires numpy, scipy and ffmpeg (libvorbis) on PATH.
"""
import sys
from pathlib import Path

import numpy as np

from ambience_synth import SR, curve, encode_ogg, highpass, lowpass, reverb, tvec

BPM = 96
BEATS = 16
SPB = SR * 60 // BPM  # 30000 samples: integer at 96 BPM
N = BEATS * SPB  # 10 s
TARGET_LUFS = -16.0  # the menu track's loudness; EndingCutscene sets the playback gain

rng = np.random.default_rng(1031)


def midi(m):
    return 440.0 * 2.0 ** ((m - 69.0) / 12.0)


def add(buf, beat, sig, pan=0.0, gain=1.0):
    """Mix a mono signal into the stereo buffer at a beat position; pan -1 left .. 1 right."""
    i = int(round(beat * SPB))
    n = min(sig.size, N - i)
    if n <= 0:
        return
    angle = (pan + 1) * np.pi / 4
    buf[0, i:i + n] += sig[:n] * gain * np.cos(angle)
    buf[1, i:i + n] += sig[:n] * gain * np.sin(angle)


def marimba(m, seconds=1.1):
    t = tvec(seconds)
    f = midi(m)
    sig = (np.sin(2 * np.pi * f * t) * np.exp(-t * 6.0)
           + 0.28 * np.sin(2 * np.pi * f * 4.0 * t) * np.exp(-t * 15.0)
           + 0.07 * np.sin(2 * np.pi * f * 9.9 * t) * np.exp(-t * 32.0))
    return sig * np.minimum(1, t / 0.002)


def bell(m, seconds=3.5):
    t = tvec(seconds)
    f = midi(m)
    sig = (np.sin(2 * np.pi * f * t) * np.exp(-t * 1.6)
           + 0.35 * np.sin(2 * np.pi * f * 2.76 * t) * np.exp(-t * 3.2)
           + 0.15 * np.sin(2 * np.pi * f * 5.4 * t) * np.exp(-t * 7.0))
    return sig * np.minimum(1, t / 0.002) * np.minimum(1, (seconds - t) / 0.4)


def pizzicato(m, seconds=0.5):
    t = tvec(seconds)
    f = midi(m)
    sig = np.sin(2 * np.pi * f * t) * np.exp(-t * 7.5) + 0.35 * np.sin(2 * np.pi * f * 2.0 * t) * np.exp(-t * 13.0)
    return lowpass(sig, 900) * np.minimum(1, t / 0.004)


def strum(notes, seconds=0.45):
    """A plucked chord (guitar or ukulele): harmonic partials with the upper ones dying first, strummed a few ms apart."""
    out = np.zeros(int(SR * seconds))
    for k, m in enumerate(notes):
        t = tvec(seconds - 0.012 * k)
        f = midi(m)
        sig = sum((1.0 / h) * np.exp(-t * (5.0 + 3.0 * h)) * np.sin(2 * np.pi * f * h * t) for h in range(1, 7))
        i = int(SR * 0.012 * k)
        out[i:i + sig.size] += sig * np.minimum(1, t / 0.003)
    return out / len(notes)


def shaker(accent):
    t = tvec(0.06)
    return highpass(rng.standard_normal(t.size), 5500) * np.exp(-t * 55.0) * (0.5 if accent else 0.28)


def woodblock():
    t = tvec(0.1)
    return np.sin(2 * np.pi * 1180 * t) * np.exp(-t * 55.0) * 0.5 + highpass(rng.standard_normal(t.size), 2500) * np.exp(-t * 120.0) * 0.18


def pad(notes, seconds):
    """A soft string-like bed: detuned saws behind a low-pass, slow attack, no abrupt end."""
    t = tvec(seconds)
    out = np.zeros(t.size)
    for m in notes:
        for cents in (-7, 7):
            f = midi(m) * 2 ** (cents / 1200)
            out += (2 * ((f * t + rng.random()) % 1) - 1) / (2 * len(notes))
    return lowpass(out, 1100, 2) * np.minimum(1, t / 0.9) * np.clip((seconds - t) / 0.6, 0, 1)


# (bass root, bass fifth, strummed triad) for each two-beat chord, then the closing bar.
C = (36, 43, [60, 64, 67])
AM = (33, 40, [60, 64, 69])
F = (41, 48, [60, 65, 69])
DM = (38, 45, [62, 65, 69])
G = (43, 50, [62, 67, 71])
PROGRESSION = [C, AM, F, C, DM, G]

# (beat, midi note) for the marimba. Half-beats are eighth notes.
MELODY = [
    (0, 76), (0.5, 79), (1, 84), (2, 83), (2.5, 81), (3, 76),
    (4, 77), (4.5, 81), (5, 84), (6, 83), (6.5, 79), (7, 76),
    (8, 74), (8.5, 77), (9, 81), (10, 79), (10.5, 83), (11, 86),
    (12, 84), (13, 88), (13.5, 91),
]


def render():
    mix = np.zeros((2, N))
    for i, (root, fifth, triad) in enumerate(PROGRESSION):
        b = i * 2
        add(mix, b, pizzicato(root), 0.0, 0.9)
        add(mix, b + 1, pizzicato(fifth), 0.0, 0.7)
        for off in (0.5, 1.5):
            add(mix, b + off, strum(triad), 0.3 if off == 0.5 else -0.3, 0.22)
    add(mix, 12, pizzicato(36, 1.2), 0.0, 0.9)
    add(mix, 12, strum([60, 64, 67, 72], 1.4), 0.0, 0.3)

    for step in range(24):  # eighth notes up to the last bar
        add(mix, step * 0.5, shaker(step % 2 == 0), 0.45, 0.22)
    for beat in (1, 3, 5, 7, 9, 11):
        add(mix, beat, woodblock(), -0.2, 0.3)

    for beat, m in MELODY:
        add(mix, beat, marimba(m), -0.1, 0.5)
    add(mix, 14, bell(96), 0.2, 0.28)
    add(mix, 14, bell(91), -0.2, 0.18)

    final = pad([48, 55, 60, 64], 4.0 * 60 / BPM + 1.2)
    add(mix, 12, final, 0.0, 0.5)

    mix = reverb(mix, 1.5, 0.22, rng, damp=6500)
    mix *= curve(N / SR, ((0, 1.0), (N / SR - 0.35, 1.0), (N / SR, 0.0)))
    return np.tanh(mix * 1.1)


def main():
    out_path = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[1] / "Game/Assets/Audio/Music/ending_music.ogg"
    out_path.parent.mkdir(parents=True, exist_ok=True)
    encode_ogg(render(), out_path, lufs=TARGET_LUFS)


if __name__ == "__main__":
    main()
