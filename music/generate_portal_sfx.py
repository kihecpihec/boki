#!/usr/bin/env python3
"""Procedurally renders the level portal sound effects.

portal_pull   - the rider is sucked in: a tone and a swirl of noise both climb and speed up, a low swell builds
                underneath, and the sound is cut off at the moment the screen is fully covered.
portal_arrive - the rider lands in the new world: a soft pop, a falling sweep that unwinds the pull, and a few
                bright chime partials as the swirl opens.

Both sit in Resources/Portal because PortalTransit loads them by name at runtime, so no scene needs wiring.
Everything is synthesized from seeded noise and sines, so the files regenerate byte for byte and carry no
samples or licenses.

Usage: python3 music/generate_portal_sfx.py [output_dir]
Default output: Game/Assets/Resources/Portal
Requires numpy and scipy.
"""
import sys
import wave
from pathlib import Path

import numpy as np

from ambience_synth import band, lowpass, highpass

SR = 48000
PEAK = 0.5  # same ceiling as the other effects


def t(seconds):
    return np.arange(int(SR * seconds)) / SR


def fade_edges(sig, head=0.004, tail=0.02):
    n_head, n_tail = int(SR * head), int(SR * tail)
    sig = sig.copy()
    sig[:n_head] *= np.linspace(0, 1, n_head)
    sig[-n_tail:] *= np.linspace(1, 0, n_tail)
    return sig


def portal_pull():
    seconds = 0.9
    tt = t(seconds)
    x = tt / seconds
    rng = np.random.default_rng(31)

    # Rising tone with a slow vibrato, about four octaves over the pull.
    freq = 140 * np.exp(np.log(14) * x ** 1.4) * (1 + 0.012 * np.sin(2 * np.pi * 7 * tt))
    phase = 2 * np.pi * np.cumsum(freq) / SR
    tone = (np.sin(phase) + 0.3 * np.sin(2 * phase)) * x ** 1.5

    # Noise bands whose centres climb with time give a cheap swept filter; faster tremolo is the swirl.
    noise = rng.standard_normal(tt.size)
    swirl = np.zeros_like(noise)
    for centre, at in ((400, 0.25), (800, 0.4), (1600, 0.55), (3200, 0.72)):
        bandpassed = band(noise, centre * 0.65, centre * 1.45)
        swirl += bandpassed * np.exp(-((x - at) / 0.17) ** 2)
    tremolo_phase = 2 * np.pi * np.cumsum(6 + 16 * x) / SR
    swirl *= 0.75 + 0.25 * np.sin(tremolo_phase)

    # Low swell: the pull you feel in the chest, strongest right before the cut.
    sub = np.sin(2 * np.pi * np.cumsum(55 + 40 * x) / SR) * x ** 2.2

    out = 0.55 * tone + 0.9 * swirl + 1.1 * sub
    out *= np.minimum(1, tt / 0.03)
    out = highpass(out, 40)
    return fade_edges(out, tail=0.012)


def portal_arrive():
    seconds = 1.15
    tt = t(seconds)
    rng = np.random.default_rng(37)

    # Soft pop on landing: a low sine plus a short, dark noise puff.
    pop = np.sin(2 * np.pi * 95 * tt) * np.exp(-tt * 22)
    n = int(SR * 0.03)
    puff = np.zeros_like(tt)
    puff[:n] = lowpass(rng.standard_normal(n), 900) * np.exp(-np.arange(n) / (SR * 0.008))

    # The pull unwinding: a falling sweep and falling noise bands.
    freq = 220 + 1580 * np.exp(-tt * 3.4)
    sweep = (np.sin(2 * np.pi * np.cumsum(freq) / SR) + 0.25 * np.sin(4 * np.pi * np.cumsum(freq) / SR))
    sweep *= np.minimum(1, tt / 0.02) * np.exp(-tt * 3.2)
    noise = rng.standard_normal(tt.size)
    wash = np.zeros_like(noise)
    for centre, at in ((3200, 0.08), (1600, 0.2), (800, 0.34), (400, 0.5)):
        wash += band(noise, centre * 0.65, centre * 1.45) * np.exp(-((tt - at) / 0.16) ** 2)
    wash *= 0.75 + 0.25 * np.sin(2 * np.pi * np.cumsum(20 - 12 * np.minimum(1, tt / seconds)) / SR)

    # Chime: three partials of a major triad, staggered so they read as a sparkle.
    chime = np.zeros_like(tt)
    for start, f, gain in ((0.05, 1318.5, 1.0), (0.12, 1760.0, 0.8), (0.2, 2637.0, 0.6)):
        local = np.clip(tt - start, 0, None)
        chime += gain * np.sin(2 * np.pi * f * tt) * np.where(tt >= start, np.exp(-local * 6.5), 0) \
            * np.minimum(1, local / 0.004)

    out = 0.9 * pop + 0.6 * puff + 0.5 * sweep + 0.8 * wash + 0.28 * chime
    return fade_edges(highpass(out, 40), tail=0.06)


def write_wav(path, sig):
    sig = sig / np.max(np.abs(sig)) * PEAK
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((sig * 32767).astype("<i2").tobytes())
    print(f"Wrote {path} ({sig.size / SR * 1000:.0f} ms)")


def main():
    out_dir = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[1] / "Game/Assets/Resources/Portal"
    out_dir.mkdir(parents=True, exist_ok=True)
    write_wav(out_dir / "portal_pull.wav", portal_pull())
    write_wav(out_dir / "portal_arrive.wav", portal_arrive())


if __name__ == "__main__":
    main()
