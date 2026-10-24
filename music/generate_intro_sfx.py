#!/usr/bin/env python3
"""Procedurally renders the Bellevue prologue phone beep.

Story §3 PROLOGUE, shot 2: "A short phone beep" announces »PREDAVANJE — 08:00 / FERI«.
A cheap two-tone notification through a small speaker: odd-harmonic tone,
band-limited, two bursts. Everything is synthesized, so there are no samples or licenses.

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


if __name__ == "__main__":
    main()
