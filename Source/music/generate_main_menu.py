#!/usr/bin/env python3
"""Procedurally renders the Boki main menu loop (music/main_menu.ogg).

Concept (Story.md section 6): distant techno bass through a wall, wind, a cold
pad and a music-box melody; the morning after the rave on Pohorje. A heartbeat
closes the loop as a nod to Guilt.

Everything is synthesized, so the track has no external samples or licenses.
Every time-based stage wraps around the loop length, so the loop is seamless.

Usage: python3 music/generate_main_menu.py [output.ogg]
Requires numpy, scipy and ffmpeg (libvorbis) on PATH.
"""
import subprocess
import sys
import tempfile
import wave
from pathlib import Path

import numpy as np
from scipy.signal import butter, sosfilt

SR = 48000
BPM = 120
BARS = 32
BEATS = BARS * 4
SPB = SR * 60 // BPM  # samples per beat (integer at 120 BPM)
N = BEATS * SPB  # 64 s loop
TARGET_LUFS = -16.0  # the boss track sits near -9 LUFS; menu music stays well under it

rng = np.random.default_rng(1998)


def midi(m):
    return 440.0 * 2.0 ** ((np.asarray(m, dtype=float) - 69.0) / 12.0)


def add(buf, beat, sig):
    """Add sig at a beat position, wrapping around the loop end."""
    pos = int(round(beat * SPB))
    idx = (pos + np.arange(sig.shape[-1])) % N
    buf[..., idx] += sig


def lowpass(x, fc, order=2):
    sos = butter(order, fc, "low", fs=SR, output="sos")
    return sosfilt(sos, x)


def highpass(x, fc, order=2):
    sos = butter(order, fc, "high", fs=SR, output="sos")
    return sosfilt(sos, x)


def loop_filter(x, fc, order=2, kind="low"):
    """Filter a full-length buffer without a seam at the loop point."""
    tiled = np.concatenate([x, x, x], axis=-1)
    fn = lowpass if kind == "low" else highpass
    return fn(tiled, fc, order)[..., N:2 * N]


def bar_curve(points):
    """Piecewise-linear gain over the loop; points are (bar, value) pairs."""
    bars, values = zip(*points)
    t = np.arange(N) / (SPB * 4)
    return np.interp(t, bars, values)


def tvec(seconds):
    return np.arange(int(seconds * SR)) / SR


# --- instruments ----------------------------------------------------------

def kick():
    t = tvec(0.45)
    freq = 46.0 + 130.0 * np.exp(-t * 38.0)
    phase = 2 * np.pi * np.cumsum(freq) / SR
    body = np.sin(phase) * np.exp(-t * 7.5)
    click = np.sin(2 * np.pi * 1800 * t) * np.exp(-t * 400.0) * 0.25
    return np.tanh((body + click) * 1.6) * 0.9


def heartbeat():
    """Lub-dub as two soft low thumps."""
    out = np.zeros(int(1.0 * SR))
    for start, gain in ((0.0, 1.0), (0.32, 0.7)):
        t = tvec(0.4)
        freq = 52.0 + 40.0 * np.exp(-t * 25.0)
        thump = np.sin(2 * np.pi * np.cumsum(freq) / SR) * np.exp(-t * 11.0) * gain
        i = int(start * SR)
        out[i:i + len(thump)] += thump
    return out


def hat(open_=False):
    t = tvec(0.25 if open_ else 0.06)
    noise = highpass(rng.standard_normal(len(t)), 7000, 2)
    return noise * np.exp(-t * (22.0 if open_ else 90.0)) * 0.5


def rim():
    t = tvec(0.12)
    noise = highpass(lowpass(rng.standard_normal(len(t)), 3500, 2), 900, 2)
    tone = np.sin(2 * np.pi * 330 * t)
    return (noise * 0.6 + tone * 0.4) * np.exp(-t * 55.0) * 0.5


def bass_note(m, beats=0.38):
    t = tvec(beats * 60 / BPM)
    f = float(midi(m))
    saw = 2 * ((f * t) % 1.0) - 1
    sub = np.sin(2 * np.pi * f * t)
    sig = lowpass(saw, 260, 2) * 0.55 + sub * 0.8
    env = np.minimum(t / 0.006, 1.0) * np.exp(-t * 4.5)
    return np.tanh(sig * env * 1.4)


def pad_chord(notes, beats):
    """Soft detuned saws; returns a stereo (2, n) array with slow attack and release."""
    length = beats * 60 / BPM + 1.5
    t = tvec(length)
    out = np.zeros((2, len(t)))
    for ch, side in enumerate((-1, 1)):
        for m in notes:
            for cents in (-9, 0, 9):
                f = float(midi(m)) * 2 ** ((cents + side * 3) / 1200)
                out[ch] += (2 * ((f * t + rng.random()) % 1.0) - 1) / 9
    env = np.minimum(t / 1.4, 1.0) * np.clip((length - t) / 1.5, 0.0, 1.0)
    # The filter opens over the first bars of each chord: a slow breathing swell.
    swell = lowpass(out, 650, 2) * 0.6 + lowpass(out, 1500, 2) * 0.4 * np.clip(t / 3.0, 0, 1)
    return swell * env


def pluck(m, beats=1.0):
    t = tvec(beats * 60 / BPM + 0.2)
    f = float(midi(m))
    sig = np.zeros(len(t))
    for h in range(1, 9):
        sig += (1.0 / h) * np.exp(-t * (7.0 + h * 5.0)) * np.sin(2 * np.pi * f * h * t)
    return sig * np.minimum(t / 0.003, 1.0) * 0.35


def music_box(m):
    t = tvec(3.2)
    f = float(midi(m))
    sig = (np.sin(2 * np.pi * f * t) * np.exp(-t * 2.2)
           + 0.40 * np.sin(2 * np.pi * f * 2.0 * t) * np.exp(-t * 4.0)
           + 0.18 * np.sin(2 * np.pi * f * 3.0 * t) * np.exp(-t * 6.5)
           + 0.12 * np.sin(2 * np.pi * f * 5.43 * t) * np.exp(-t * 14.0))
    return sig * np.minimum(t / 0.002, 1.0) * 0.4


# --- arrangement ----------------------------------------------------------

# Am - F(maj7) - Dm - E(7): two bars each, repeated four times (8-bar cycle).
CHORDS = [
    dict(pad=[57, 60, 64], bass=33, arp=[69, 72, 76, 81]),
    dict(pad=[53, 57, 60, 64], bass=29, arp=[65, 69, 72, 76]),
    dict(pad=[50, 53, 57], bass=38, arp=[62, 65, 69, 74]),
    dict(pad=[52, 56, 59, 62], bass=40, arp=[64, 68, 71, 76]),
]
# Music-box phrase per chord: (beat offset within the 2-bar chord, midi note).
BOX = [
    [(0, 81), (1.5, 79), (2, 76), (3, 72), (4, 76), (5.5, 79), (6, 81)],
    [(0, 77), (1.5, 76), (2, 72), (3, 69), (4, 72), (6, 77)],
    [(0, 74), (1.5, 72), (2, 69), (3, 65), (4, 69), (6, 74), (7, 72)],
    [(0, 76), (1, 80), (2, 83), (4, 80), (5, 76), (6, 71)],
]


def render():
    kick_full = np.zeros(N)
    thump = np.zeros(N)
    drums = np.zeros(N)
    bass = np.zeros(N)
    pad = np.zeros((2, N))
    arp = np.zeros((2, N))
    box = np.zeros((2, N))

    k, hb, h_closed, h_open, r = kick(), heartbeat(), hat(), hat(True), rim()

    for bar in range(BARS):
        b0 = bar * 4
        if bar < 28:
            for beat in range(4):
                add(kick_full, b0 + beat, k * (0.75 if bar < 8 else 1.0))
        else:
            add(thump, b0, hb * (0.5 + 0.12 * (bar - 28)))

        if 8 <= bar < 28:
            for beat in range(4):
                add(drums, b0 + beat + 0.5, h_open * (0.55 if bar < 16 else 0.8))
        if 16 <= bar < 28:
            for step in range(16):
                if step % 4 != 2:
                    add(drums, b0 + step / 4, h_closed * (0.35 if step % 2 else 0.2))
            add(drums, b0 + 1, r * 0.7)
            add(drums, b0 + 3, r * 0.7)

    for cycle_bar in range(0, BARS, 2):
        chord = CHORDS[(cycle_bar // 2) % 4]
        b0 = cycle_bar * 4
        add(pad, b0, pad_chord(chord["pad"], 8) * 0.55)

        if 8 <= cycle_bar < 28:
            for beat in range(8):
                add(bass, b0 + beat + 0.5, bass_note(chord["bass"]))

        if 12 <= cycle_bar < 28:
            level = 0.5 if cycle_bar < 16 else 1.0
            order = [0, 1, 2, 3, 2, 1, 2, 1]
            for step in range(32):
                if step % 8 in (1, 6) and cycle_bar >= 16:
                    continue  # leave holes so the arpeggio breathes
                note = chord["arp"][order[step % 8]]
                pan = 0.5 + 0.35 * np.sin(step * 0.9)
                p = pluck(note, 0.5) * level * (0.6 if step % 4 else 1.0)
                add(arp, b0 + step / 4, np.stack([p * (1 - pan), p * pan]) * 1.4)

        if cycle_bar >= 16:
            for offset, note in BOX[(cycle_bar // 2) % 4]:
                b = music_box(note)
                add(box, b0 + offset, np.stack([b * 0.85, b * 1.0]) * 0.9)

    # Distant kick: heavily low-passed through the intro, opening over bars 8-15.
    kick_muffled = loop_filter(kick_full, 130, 4) * 1.5
    open_amount = bar_curve([(0, 0), (8, 0), (16, 1), (28, 1), (29, 0), (32, 0)])
    kick_bus = kick_muffled * (1 - open_amount) + kick_full * open_amount
    thump_bus = loop_filter(thump, 140, 4) * 1.4

    # Sidechain-style pump on the pad and the arpeggio while the kick is present.
    phase = (np.arange(N) % SPB) / SR
    pump_depth = bar_curve([(0, 0), (4, 0.2), (8, 0.2), (14, 0.6), (28, 0.6), (29, 0), (32, 0)])
    duck = 1.0 - pump_depth * np.exp(-phase * 8.0)
    pad *= duck
    arp *= duck * 0.5 + 0.5

    # Wind built in the frequency domain: periodic by construction, so it never clicks.
    freqs = np.fft.rfftfreq(N, 1 / SR)
    shape = np.zeros_like(freqs)
    live = freqs > 60
    shape[live] = np.exp(-((np.log(freqs[live] / 520.0)) / 0.85) ** 2)
    wind = np.zeros((2, N))
    for ch in range(2):
        spec = shape * np.exp(2j * np.pi * rng.random(len(freqs)))
        sig = np.fft.irfft(spec, N)
        wind[ch] = sig / np.std(sig)
    t_loop = np.arange(N) / N
    gust = 0.6 + 0.25 * np.sin(2 * np.pi * 5 * t_loop + 0.7) + 0.15 * np.sin(2 * np.pi * 13 * t_loop)
    wind_level = bar_curve([(0, 0.9), (6, 1.0), (12, 0.25), (24, 0.25), (29, 0.8), (32, 0.9)])
    wind *= gust * wind_level * 0.05

    # Reverb: exponentially decaying noise, applied as a circular convolution.
    ir_len = int(2.6 * SR)
    ir_t = np.arange(ir_len) / SR
    wet = np.zeros((2, N))
    send = pad * 0.5 + arp * 0.45 + box * 0.9
    send_spec = np.fft.rfft(send, axis=-1)
    for ch in range(2):
        ir = rng.standard_normal(ir_len) * np.exp(-ir_t * 2.3)
        ir = lowpass(ir, 5500, 2) * np.minimum(ir_t / 0.02, 1.0)
        ir /= np.sqrt(np.sum(ir ** 2))
        ir_full = np.zeros(N)
        ir_full[:ir_len] = ir
        wet[ch] = np.fft.irfft(send_spec[ch] * np.fft.rfft(ir_full), N)

    # Ping-pong delay (dotted eighth) on the arpeggio and the music box.
    delay = int(0.75 * SPB)
    echo_src = arp * 0.9 + box * 0.7
    echo = np.zeros((2, N))
    for tap, gain in ((1, 0.42), (2, 0.24), (3, 0.12)):
        flip = tap % 2
        echo[0] += np.roll(echo_src[flip], delay * tap) * gain
        echo[1] += np.roll(echo_src[1 - flip], delay * tap) * gain
    echo = loop_filter(echo, 4500, 2)

    mono_low = kick_bus * 0.95 + thump_bus + bass * 0.7
    mix = np.stack([mono_low, mono_low])
    mix += np.stack([drums, drums]) * 0.55
    mix += pad * 0.8 + arp + box + echo * 0.9 + wet * 0.75 + wind
    # Tape-ish finish: gentle saturation and a rolled-off top end.
    mix = np.tanh(mix * 1.15)
    mix = loop_filter(mix, 13000, 2)
    return mix


def write_wav(path, stereo):
    data = (np.clip(stereo, -1, 1).T * 32767).astype("<i2")
    with wave.open(str(path), "wb") as f:
        f.setnchannels(2)
        f.setsampwidth(2)
        f.setframerate(SR)
        f.writeframes(data.tobytes())


def measure_lufs(path):
    result = subprocess.run(
        ["ffmpeg", "-hide_banner", "-nostats", "-i", str(path), "-af", "ebur128", "-f", "null", "-"],
        capture_output=True, text=True, check=True)
    summary = result.stderr.rsplit("Integrated loudness:", 1)[1]
    return float(summary.split("I:")[1].split("LUFS")[0])


def main():
    out_path = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).with_name("main_menu.ogg")
    mix = render()
    mix /= np.max(np.abs(mix))
    with tempfile.TemporaryDirectory() as tmp:
        wav = Path(tmp) / "menu.wav"
        write_wav(wav, mix * 0.5)
        gain = 10 ** ((TARGET_LUFS - measure_lufs(wav)) / 20)
        mix *= 0.5 * gain
        peak = np.max(np.abs(mix))
        if peak > 0.89:  # keep roughly -1 dBFS headroom for the Vorbis encode
            mix *= 0.89 / peak
        write_wav(wav, mix)
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(wav),
                        "-c:a", "libvorbis", "-q:a", "6", str(out_path)], check=True)
    seam = np.abs(mix[:, 0] - mix[:, -1]).max()
    print(f"Wrote {out_path} ({N / SR:.1f} s, {BPM} BPM, seam step {seam:.4f}, "
          f"peak {20 * np.log10(np.max(np.abs(mix))):.1f} dBFS)")


if __name__ == "__main__":
    main()
