#!/usr/bin/env python3
"""Procedurally renders the FERI ending sound effects.

Story §3 FINAL BOSS victory: the professor shrinks back to normal size, says it is Saturday, points at the
calendar, and turns out to be a cat. Five cartoon gags, all synthesized, so there are no samples or licenses:
the deflating boss, the camera zoom, the calendar thump, the meow and the arm licking.

Usage: python3 music/generate_ending_sfx.py [output_dir]
Default output: Game/Assets/Audio/Sfx
Requires numpy and scipy.
"""
import sys
import wave
from pathlib import Path

import numpy as np
from scipy.signal import butter, sosfilt

SR = 48000
PEAK = 0.5  # same ceiling as the menu and intro effects

# Seconds between lick strokes. EndingActing.LickSeconds must match.
LICK_PERIOD = 0.5


def t(seconds):
    return np.arange(int(SR * seconds)) / SR


def band(signal, low, high):
    return sosfilt(butter(2, [low, high], "band", fs=SR, output="sos"), signal)


def deflate():
    """The boss shrinking: a balloon letting go. A whistle that sags in pitch over a hiss, with a wobble."""
    seconds = 0.8
    tt = t(seconds)
    freq = 1050 * np.exp(-2.1 * tt) + 160 + 38 * np.sin(2 * np.pi * 17 * tt) * np.exp(-1.5 * tt)
    phase = 2 * np.pi * np.cumsum(freq) / SR
    tone = np.sin(phase) + 0.4 * np.sin(2 * phase) + 0.15 * np.sin(3 * phase)
    rng = np.random.default_rng(23)  # fixed seed: the file regenerates byte for byte
    hiss = band(rng.standard_normal(tt.size), 1800, 6500) * 0.22
    env = np.minimum(1, tt / 0.01) * np.exp(-1.6 * tt) * np.minimum(1, (seconds - tt) / 0.06)
    return (tone + hiss) * env


def zoom():
    """Camera zoom: a short rising sweep of filtered noise with a soft tone underneath."""
    seconds = 0.7
    tt = t(seconds)
    rng = np.random.default_rng(29)
    noise = rng.standard_normal(tt.size)
    out = np.zeros_like(noise)
    for centre, weight in ((500, 1.0), (1100, 0.8), (2400, 0.5), (4200, 0.25)):
        out += weight * band(noise, centre * 0.65, centre * 1.35) * np.sin(np.pi * np.clip((tt - centre / 9000) / (seconds * 0.85), 0, 1)) ** 2
    freq = 180 + 520 * (tt / seconds) ** 2
    out += 0.45 * np.sin(2 * np.pi * np.cumsum(freq) / SR)
    return out * np.minimum(1, tt / 0.03) * np.minimum(1, (seconds - tt) / 0.1)


def thump():
    """The date lands: a low dramatic hit with a short noisy front and a long soft tail."""
    seconds = 1.1
    tt = t(seconds)
    freq = 46 + 110 * np.exp(-14 * tt)
    body = np.sin(2 * np.pi * np.cumsum(freq) / SR) * np.exp(-3.2 * tt)
    rng = np.random.default_rng(31)
    n = int(SR * 0.05)
    knock = band(rng.standard_normal(n), 300, 2400) * np.exp(-np.arange(n) / (SR * 0.012)) * np.minimum(1, np.arange(n) / (SR * 0.0008))
    out = body * np.minimum(1, tt / 0.002)
    out[:n] += knock * 0.8
    return out * np.minimum(1, (seconds - tt) / 0.15)


# Vowel formants (centre Hz, relative gain): "ee" at the start of a meow, "ow" at its end.
EE = ((330, 1.0), (2450, 0.55), (3100, 0.3))
OW = ((690, 1.0), (1050, 0.6), (2600, 0.2))


def formant_filter(source, start, end):
    """Time-varying vowel: overlapping windows of the source, each shaped by formants that morph from start to end."""
    chunk = int(SR * 0.04)
    hop = chunk // 2
    window = np.hanning(chunk)
    out = np.zeros(source.size + chunk)
    for i in range(0, source.size - chunk, hop):
        morph = i / max(1, source.size - chunk)
        piece = source[i:i + chunk] * window
        shaped = np.zeros(chunk)
        for (f0, g0), (f1, g1) in zip(start, end):
            centre = f0 + (f1 - f0) * morph
            shaped += (g0 + (g1 - g0) * morph) * band(piece, centre * 0.82, centre * 1.2)
        out[i:i + chunk] += shaped
    return out[:source.size]


def meow():
    """A cartoon 'mee-ow': a glottal buzz whose pitch climbs then falls, with the vowel sliding from 'ee' to 'ow'."""
    seconds = 0.95
    tt = t(seconds)
    rise = np.clip(tt / 0.28, 0, 1)
    fall = np.clip((tt - 0.28) / 0.62, 0, 1)
    freq = 430 + 330 * np.sin(rise * np.pi / 2) - 380 * fall ** 1.4
    freq *= 1 + 0.012 * np.sin(2 * np.pi * 6.5 * tt)  # slight vibrato
    phase = np.cumsum(freq) / SR
    saw = 2 * (phase % 1) - 1
    buzz = saw - 0.5 * np.sin(2 * np.pi * phase)  # a little less harsh than a bare sawtooth
    rng = np.random.default_rng(37)
    voiced = buzz + 0.05 * rng.standard_normal(tt.size)
    voice = formant_filter(voiced, EE, OW)
    attack = np.minimum(1, tt / 0.03)
    env = attack * (0.55 + 0.45 * np.sin(np.pi * np.clip(tt / 0.4, 0, 1))) * np.minimum(1, (seconds - tt) / 0.18)
    return voice * env


def lick():
    """Three wet strokes of a tongue on skin, one every LICK_PERIOD seconds: a swept breathy swish and a soft smack."""
    seconds = LICK_PERIOD * 2 + 0.55
    out = np.zeros(int(SR * seconds))
    rng = np.random.default_rng(41)
    for k in range(3):
        stroke = 0.36
        tt = t(stroke)
        noise = rng.standard_normal(tt.size)
        sweep = np.zeros_like(noise)
        for centre, weight in ((1400, 1.0), (2600, 0.7), (4200, 0.4)):
            sweep += weight * band(noise, centre * 0.7, centre * 1.3) * np.sin(np.pi * np.clip((tt - 0.02 * k) / stroke, 0, 1)) ** 2
        sweep *= np.minimum(1, tt / 0.02) * np.minimum(1, (stroke - tt) / 0.05)
        n = int(SR * 0.02)
        smack = band(rng.standard_normal(n), 700, 3200) * np.exp(-np.arange(n) / (SR * 0.004))
        i = int(SR * LICK_PERIOD * k)
        out[i:i + sweep.size] += sweep * (0.9 - 0.12 * k)
        j = i + int(SR * 0.3)
        out[j:j + n] += smack * 0.9
    return out


def write_wav(path, sig):
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
    write_wav(out_dir / "ending_deflate.wav", deflate())
    write_wav(out_dir / "ending_zoom.wav", zoom())
    write_wav(out_dir / "ending_thump.wav", thump())
    write_wav(out_dir / "ending_meow.wav", meow())
    write_wav(out_dir / "ending_lick.wav", lick())


if __name__ == "__main__":
    main()
