#!/usr/bin/env python3
"""Procedurally renders the Bellevue prologue sound effects.

Story §3 PROLOGUE, shot 2: "A short phone beep" announces »PREDAVANJE — 08:00 / FERI«.
A cheap two-tone notification through a small speaker: odd-harmonic tone,
band-limited, two bursts. The eye-pop boing and the panic-dash whoosh are the intro's
cartoon gags. The wake-up cues are the quiet foley around them: Boki's sleeping breaths and waking groan,
the sleeve of the arm rising, and the click and pip of his digital watch. Then the urgency: a heartbeat that
races from the moment he reads 07:58 and a rising whine under the lecture message, and the sprint itself
(gasps, footfalls on the stride beat, the bottle kick, the skid, the scooter clank, the leap). Everything is
synthesized, so there are no samples or licenses.

Usage: python3 music/generate_intro_sfx.py [output_dir]
Default output: Game/Assets/Audio/Sfx
Requires numpy and scipy.
"""
import sys
import wave
from pathlib import Path

import numpy as np
from scipy.signal import butter, sosfilt

from ambience_synth import band as bandpass, highpass, lowpass, smooth_curve
from generate_ending_sfx import formant_filter  # the vowel morph that already shapes the cat's meow

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


def breath(seconds, peak, low, high, rng):
    """One breath of air through the nose or mouth: band-limited noise that swells to `peak` (0..1 of the length) and ebbs."""
    tt = t(seconds)
    x = np.clip(tt / seconds, 0, 1)
    warped = x ** (np.log(0.5) / np.log(peak))
    env = np.sin(np.pi * warped) ** 2
    air = sosfilt(butter(4, [low, high], "band", fs=SR, output="sos"), rng.standard_normal(tt.size))
    return air / np.std(air) * env


def rustle(seconds, low, high, rng, flutter=11.0):
    """Cloth or grass moving: band-limited noise whose level flickers a few times a second."""
    tt = t(seconds)
    x = np.clip(tt / seconds, 0, 1)
    air = sosfilt(butter(2, [low, high], "band", fs=SR, output="sos"), rng.standard_normal(tt.size))
    flicker = 0.35 + 0.65 * smooth_curve(tt.size, flutter, rng)
    return air / np.std(air) * flicker * np.sin(np.pi * x) ** 1.5


def mix_at(out, sig, seconds, gain=1.0):
    i = int(SR * seconds)
    n = min(sig.size, out.size - i)
    out[i:i + n] += sig[:n] * gain


def wake_up():
    """Shot 1 (4.5 s): two slow sleeping breaths matching the rise and fall of his chest (2.86 s a cycle), a shuffle in
    the grass, then a deeper waking breath and a groggy 'mm-uh' just as his eyelids start to lift (3.5 s)."""
    rng = np.random.default_rng(53)
    out = np.zeros(int(SR * 4.5))
    mix_at(out, breath(1.25, 0.55, 300, 1700, rng), 0.1, 0.5)          # in
    mix_at(out, breath(1.3, 0.35, 200, 1200, rng), 1.5, 0.4)           # out
    mix_at(out, rustle(0.4, 1200, 5200, rng, 14), 1.65, 0.28)          # shifts in the grass
    mix_at(out, breath(0.7, 0.6, 300, 2100, rng), 3.1, 0.85)           # deeper, faster: he is surfacing
    mix_at(out, rustle(0.55, 1000, 5000, rng, 13), 3.25, 0.3)          # the shuffle of waking

    tt = t(0.6)
    f0 = 118 - 26 * np.clip(tt / 0.6, 0, 1) ** 0.8
    phase = np.cumsum(f0 * (1 + 0.01 * np.sin(2 * np.pi * 5 * tt))) / SR
    voiced = (2 * (phase % 1) - 1) - 0.5 * np.sin(2 * np.pi * phase) + 0.18 * rng.standard_normal(tt.size)
    groan = formant_filter(voiced, ((290, 1.0), (1000, 0.45), (2300, 0.12)), ((540, 1.0), (1180, 0.6), (2500, 0.2)))
    groan *= np.minimum(1, tt / 0.12) * np.minimum(1, (0.6 - tt) / 0.2)
    mix_at(out, groan / np.max(np.abs(groan)), 3.85, 1.1)
    n = int(SR * 0.02)
    smack = sosfilt(butter(2, [800, 3200], "band", fs=SR, output="sos"), rng.standard_normal(n)) * np.exp(-np.arange(n) / (SR * 0.004))
    mix_at(out, smack / np.max(np.abs(smack)), 3.8, 0.12)
    return out


def arm_rustle():
    """Shot 2 (1.8 s): the left arm lifting: the sleeve sliding over the jacket, with a squeak of the cuff."""
    rng = np.random.default_rng(59)
    out = np.zeros(int(SR * 1.8))
    mix_at(out, rustle(1.25, 600, 4200, rng, 9), 0.1, 1.0)
    mix_at(out, rustle(0.5, 2200, 6500, rng, 16), 0.65, 0.35)
    for at, freq in ((0.5, 2650), (0.95, 3100)):
        n = int(SR * 0.05)
        tt = np.arange(n) / SR
        squeak = np.sin(2 * np.pi * (freq + 400 * tt / 0.05) * tt) * np.sin(np.pi * tt / 0.05)
        mix_at(out, squeak, at, 0.04)
    return out


def watch_check():
    """Shot 3 (1.6 s): the wrist stops in front of his face: a strap click, a settling rustle, then the soft pip of a
    digital watch lighting its display."""
    rng = np.random.default_rng(61)
    out = np.zeros(int(SR * 1.6))
    n = int(SR * 0.012)
    click = sosfilt(butter(2, [1500, 5000], "band", fs=SR, output="sos"), rng.standard_normal(n)) * np.exp(-np.arange(n) / (SR * 0.003))
    mix_at(out, click / np.max(np.abs(click)), 0.04, 0.5)
    mix_at(out, rustle(0.22, 900, 4500, rng, 15), 0.1, 0.22)
    pip = burst(2960, 0.06) * 0.55
    mix_at(out, sosfilt(butter(2, [1800, 5000], "band", fs=SR, output="sos"), pip), 0.38, 0.5)
    mix_at(out, click / np.max(np.abs(click)), 0.375, 0.3)
    return out


def heart_thump():
    """One heart thump: a short low body with a soft noisy front so small speakers still carry it."""
    tt = t(0.3)
    freq = 70 + 95 * np.exp(-30 * tt)
    body = np.sin(2 * np.pi * np.cumsum(freq) / SR) * np.exp(-tt * 16)
    rng = np.random.default_rng(67)
    front = lowpass(rng.standard_normal(tt.size), 450) * np.exp(-tt / 0.02)
    return (body + 0.5 * front) * np.minimum(1, tt / 0.003)


HEART_SECONDS = 11.9


def heartbeat():
    """Starts with shot 3 (his eyes land on 07:58) and runs to the mount: a pulse that races from 62 to 150 bpm, jolts
    on each eye pop (2.05 s and 3.85 s in, matching the pop times in IntroActing), with a rising, trembling whine that
    builds under the lecture message and the line, then cuts the instant he bolts (8.8 s in, the Scramble shot)."""
    out = np.zeros(int(SR * HEART_SECONDS))
    tt = t(HEART_SECONDS)
    pace = ((0, 62), (1.6, 70), (2.05, 72), (2.4, 100), (3.3, 110), (5.7, 120), (8.8, 140), (11.2, 150), (HEART_SECONDS, 150))
    thump = heart_thump()
    now = 0.2
    while now < HEART_SECONDS - 0.4:
        beat = 60 / np.interp(now, *zip(*pace))
        grow = np.interp(now, (0, 2.4, 8.8, HEART_SECONDS), (0.45, 0.7, 1.0, 1.0))
        mix_at(out, thump, now, grow)
        mix_at(out, thump, now + 0.3 * beat, grow * 0.65)
        now += beat
    for at in (2.05, 3.85):
        mix_at(out, thump, at, 1.4)

    x = np.clip((tt - 3.3) / 5.5, 0, 1)
    freq = 440 * 2 ** (1.2 * x)
    p1 = 2 * np.pi * np.cumsum(freq) / SR
    p2 = 2 * np.pi * np.cumsum(freq * 1.006) / SR
    flutter = 0.6 + 0.4 * np.sin(2 * np.pi * np.cumsum(4 + 10 * x) / SR)
    whine = (np.sin(p1) + np.sin(p2) + 0.3 * np.sin(2 * p1)) * flutter
    whine = lowpass(whine, 3200) * x ** 1.6 * np.where(tt < 8.8, 1.0, np.exp(-(tt - 8.8) * 14))
    out += whine * 0.14
    return out * np.minimum(1, (HEART_SECONDS - tt) / 0.7)


def step(rng, weight):
    """A foot in dewy grass: a dull thud and a quick swish of blades."""
    tt = t(0.12)
    thud = lowpass(rng.standard_normal(tt.size), 700) * np.exp(-tt / 0.025)
    swish = bandpass(rng.standard_normal(tt.size), 2000, 6500) * np.exp(-tt / 0.018) * 0.5
    return (thud / np.std(thud) * 0.9 + swish / np.std(swish) * 0.25) * weight * np.minimum(1, tt / 0.001)


def metal_clunk(rng):
    """The scooter: a few damped resonances of a frame plus a short click."""
    tt = t(0.4)
    ring = sum(np.sin(2 * np.pi * f * tt) * np.exp(-tt / tau) * g for f, tau, g in ((410, 0.09, 1.0), (980, 0.06, 0.6), (1900, 0.04, 0.35)))
    n = int(SR * 0.01)
    click = bandpass(rng.standard_normal(n), 1500, 6000) * np.exp(-np.arange(n) / (SR * 0.002))
    ring[:n] += click / np.max(np.abs(click)) * 0.8
    return ring * np.minimum(1, tt / 0.001)


def bottle_tok(rng):
    """An empty plastic bottle struck or bouncing: a hollow tok."""
    tt = t(0.2)
    ring = np.sin(2 * np.pi * 1250 * tt) * np.exp(-tt / 0.05) + 0.5 * np.sin(2 * np.pi * 2100 * tt) * np.exp(-tt / 0.03)
    n = int(SR * 0.008)
    ring[:n] += bandpass(rng.standard_normal(n), 600, 3000) * np.exp(-np.arange(n) / (SR * 0.002)) * 0.8
    return ring * np.minimum(1, tt / 0.0008)


STRIDE_HERTZ = 3.4          # IntroActing.StrideHertz: a footfall every half stride
SCRAMBLE_START = 15.1       # seconds into the intro (the first six shots)
SCRAMBLE_SECONDS = 2.9      # the 2.4 s shot plus the landing, which sounds just inside the Mount shot


def scramble():
    """Shot 7 (2.4 s) and the landing: Boki gasps awake and sits up, kicks the bottle, springs up and runs at the city,
    skids, runs back, the scooter clanks upright, and he leaps on. Run and skid times are IntroActing's (0.72-1.17 s,
    1.17-1.37 s, 1.37-1.95 s); footfalls land on the stride beat of the global clock, as his legs do."""
    rng = np.random.default_rng(71)
    out = np.zeros(int(SR * SCRAMBLE_SECONDS))
    mix_at(out, breath(0.28, 0.35, 400, 3000, rng), 0.0, 1.1)          # the gasp as he sits bolt upright
    mix_at(out, rustle(0.35, 900, 5000, rng, 15), 0.02, 0.5)
    mix_at(out, rustle(0.4, 900, 5200, rng, 17), 0.4, 0.45)            # springing to his feet
    mix_at(out, bottle_tok(rng), 0.3, 0.9)                             # the sit-up kicks the bottle
    for at, gain in ((1.02, 0.55), (1.2, 0.35), (1.31, 0.2)):          # it hops, lands and bounces
        mix_at(out, bottle_tok(rng), at, gain)
    mix_at(out, rustle(0.55, 400, 2500, rng, 24), 1.35, 0.16)          # then rolls to rest

    for k in range(100, 130):
        at = k / (2 * STRIDE_HERTZ) - SCRAMBLE_START
        if 0.72 <= at < 1.17 or 1.37 <= at < 1.95:
            mix_at(out, step(rng, 1.0 if k % 2 else 0.9), at, 0.9)
    skid = rustle(0.2, 800, 4200, rng, 30) * np.linspace(1.0, 0.4, int(SR * 0.2))
    mix_at(out, skid, 1.17, 0.9)                                       # the slide as he turns
    mix_at(out, metal_clunk(rng), 1.27, 0.9)                           # the scooter swings upright
    for at in (0.95, 1.55, 1.85):
        mix_at(out, breath(0.17, 0.4, 500, 3200, rng), at, 0.55)       # panting
    mix_at(out, rustle(0.35, 1500, 6000, rng, 18), 2.05, 0.4)          # the leap
    thump = heart_thump()[: int(SR * 0.16)]
    mix_at(out, thump, 2.4, 2.2)                                       # landing on the deck
    mix_at(out, metal_clunk(rng), 2.4, 0.75)
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
    write_wav(out_dir / "intro_wake_up.wav", wake_up())
    write_wav(out_dir / "intro_arm_rustle.wav", arm_rustle())
    write_wav(out_dir / "intro_watch_check.wav", watch_check())
    write_wav(out_dir / "intro_heartbeat.wav", heartbeat())
    write_wav(out_dir / "intro_scramble.wav", scramble())


if __name__ == "__main__":
    main()
