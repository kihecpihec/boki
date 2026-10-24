#!/usr/bin/env python3
"""Procedurally renders the Bellevue prologue sound effects.

Story §3 PROLOGUE, shot 2: "A short phone beep" announces »PREDAVANJE — 08:00 / FERI«.
A cheap two-tone notification through a small speaker: odd-harmonic tone,
band-limited, two bursts. The eye-pop boing and the panic-dash whoosh are the intro's
cartoon gags. Everything is synthesized, so there are no samples or licenses.

Usage: python3 music/generate_intro_sfx.py [output_dir]
Default output: Game/Assets/Audio/Sfx
Requires numpy and scipy.
"""
import sys
import wave
from pathlib import Path

import numpy as np
from scipy.signal import butter, sosfilt

SR = 48000
PEAK = 0.5  # same ceiling as the menu effects


def t(seconds):
    return np.arange(int(SR * seconds)) / SR


def burst(freq, seconds):
    """Square-ish beep (odd harmonics) with a short attack and release so it does not click."""
    tt = t(seconds)
    tone = sum(np.sin(2 * np.pi * freq * k * tt) / k for k in (1, 3, 5))
    env = np.minimum(1, tt / 0.004) * np.minimum(1, (seconds - tt) / 0.012)
    return tone * env


def phone_beep():
    out = np.zeros(int(SR * 0.46))
    for start, freq in ((0.0, 1318.5), (0.17, 1760.0)):
        b = burst(freq, 0.13)
        i = int(SR * start)
        out[i:i + b.size] += b
    # Small phone speaker: nothing below ~500 Hz or above ~4.5 kHz.
    out = sosfilt(butter(2, [500, 4500], "band", fs=SR, output="sos"), out)
    return out


def eye_pop():
    """Cartoon eye pop: a wet click, then a coil-spring boing whose pitch snaps up and wobbles down."""
    seconds = 0.6
    tt = t(seconds)
    freq = 240 + 320 * np.exp(-9 * tt) + 45 * np.exp(-5 * tt) * np.sin(2 * np.pi * 14 * tt)
    phase = 2 * np.pi * np.cumsum(freq) / SR
    boing = (np.sin(phase) + 0.35 * np.sin(2 * phase)) * np.exp(-5.5 * tt) * np.minimum(1, tt / 0.003)
    n = int(SR * 0.025)
    rng = np.random.default_rng(7)  # fixed seed: the file regenerates byte for byte
    click = rng.standard_normal(n) * np.exp(-np.arange(n) / (SR * 0.004))
    out = boing
    out[:n] += click * 0.9
    out = sosfilt(butter(2, 90, "high", fs=SR, output="sos"), out)
    return out * np.minimum(1, (seconds - tt) / 0.03)


def dash_whoosh():
    """Short breathy sweep for Boki bolting to the scooter: band-passed noise that rises, then dies."""
    seconds = 0.55
    tt = t(seconds)
    rng = np.random.default_rng(11)
    noise = rng.standard_normal(tt.size)
    out = np.zeros_like(noise)
    # Three overlapping bands whose centres climb with time give a cheap swept filter.
    for centre, weight in ((700, 1.0), (1400, 0.8), (2600, 0.5)):
        band = sosfilt(butter(2, [centre * 0.6, centre * 1.4], "band", fs=SR, output="sos"), noise)
        out += weight * band * np.sin(np.pi * np.clip((tt - centre / 6000) / (seconds * 0.8), 0, 1)) ** 2
    out *= np.minimum(1, tt / 0.02) * np.minimum(1, (seconds - tt) / 0.08)
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
    write_wav(out_dir / "intro_phone_beep.wav", phone_beep())
    write_wav(out_dir / "intro_eye_pop.wav", eye_pop())
    write_wav(out_dir / "intro_dash.wav", dash_whoosh())


if __name__ == "__main__":
    main()
