#!/usr/bin/env python3
"""Procedurally renders the Boki menu sound effects.

Same palette as the main menu loop (generate_main_menu.py): a cold music-box
pluck for selecting and clicking, and a low "power down" for pausing. Everything
is synthesized, so there are no external samples or licenses.

Usage: python3 music/generate_menu_sfx.py [output_dir]
Default output: Game/Assets/Audio/Sfx
Requires numpy and scipy.
"""
import sys
import wave
from pathlib import Path

import numpy as np
from scipy.signal import butter, sosfilt

SR = 48000
PEAK = 0.5  # about -6 dBFS; the menu loop sits near -16 LUFS and these must stay under it

rng = np.random.default_rng(1998)


def midi(m):
    return 440.0 * 2.0 ** ((m - 69.0) / 12.0)


def t(seconds):
    return np.arange(int(SR * seconds)) / SR


def fade_edges(x, fade=0.002):
    n = int(SR * fade)
    x[:n] *= np.linspace(0, 1, n)
    x[-n:] *= np.linspace(1, 0, n)
    return x


def pluck(m, seconds, decay):
    """Music-box tone: a sine with a bright 2nd/3rd partial that dies faster."""
    tt = t(seconds)
    f = midi(m)
    body = np.sin(2 * np.pi * f * tt) + 0.35 * np.sin(2 * np.pi * 2 * f * tt) * np.exp(-tt * decay * 2) \
        + 0.12 * np.sin(2 * np.pi * 3.01 * f * tt) * np.exp(-tt * decay * 3)
    return body * np.exp(-tt * decay)


def tick(seconds=0.006):
    tt = t(seconds)
    return sosfilt(butter(2, 2500, "high", fs=SR, output="sos"), rng.standard_normal(tt.size)) * np.exp(-tt * 700)


def place(buf, start, sig):
    i = int(SR * start)
    buf[i:i + sig.size] += sig


def mix(seconds, *layers):
    out = np.zeros(int(SR * seconds))
    for start, sig, gain in layers:
        place(out, start, sig * gain)
    return out


def select():
    return mix(0.12, (0.0, pluck(91, 0.12, 45), 1.0), (0.0, tick(), 0.25))


def click():
    return mix(0.30, (0.0, pluck(88, 0.30, 22), 0.8), (0.055, pluck(95, 0.24, 24), 1.0), (0.0, tick(), 0.35))


def back():
    return mix(0.30, (0.0, pluck(95, 0.30, 22), 0.8), (0.055, pluck(88, 0.24, 24), 1.0), (0.0, tick(), 0.25))


def pause():
    """Falling sine plus a muffled thump and a closing noise wash, like the scooter light dropping out."""
    seconds = 0.55
    tt = t(seconds)
    freq = 70 + 330 * np.exp(-tt * 9)
    sweep = np.sin(2 * np.pi * np.cumsum(freq) / SR) * np.exp(-tt * 6)
    thump = np.sin(2 * np.pi * 52 * tt) * np.exp(-tt * 14)
    wash = rng.standard_normal(tt.size) * np.exp(-tt * 10)
    wash = sosfilt(butter(2, 1800, "low", fs=SR, output="sos"), wash)
    return 0.7 * sweep + 0.8 * thump + 0.25 * wash


def write_wav(path, sig):
    sig = fade_edges(sig)
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
    for name, fn in (("menu_select", select), ("menu_click", click), ("menu_back", back), ("menu_pause", pause)):
        write_wav(out_dir / f"{name}.wav", fn())


if __name__ == "__main__":
    main()
