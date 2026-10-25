"""Shared building blocks for the cutscene ambience generators (wind, birds, room, reverb).

Everything is synthesized from seeded noise and sine partials, so the files regenerate byte for byte and
carry no samples or licenses. Imported by generate_intro_ambience.py, generate_intro_sfx.py and
generate_ending_ambience.py.
"""
import subprocess
import tempfile
import wave
from pathlib import Path

import numpy as np
from scipy.signal import butter, fftconvolve, sosfilt

SR = 48000


def tvec(seconds):
    return np.arange(int(SR * seconds)) / SR


def band(x, low, high, order=2):
    return sosfilt(butter(order, [low, high], "band", fs=SR, output="sos"), x)


def lowpass(x, cutoff, order=2):
    return sosfilt(butter(order, cutoff, "low", fs=SR, output="sos"), x)


def highpass(x, cutoff, order=2):
    return sosfilt(butter(order, cutoff, "high", fs=SR, output="sos"), x)


def smooth_curve(n, rate, rng):
    """Slowly wandering 0..1 curve with about `rate` new targets per second, cosine-eased between them."""
    x = np.arange(n) / SR * rate
    knots = rng.random(int(x[-1]) + 3)
    i = x.astype(int)
    f = (1 - np.cos(np.pi * (x - i))) / 2
    return knots[i] * (1 - f) + knots[i + 1] * f


def unit(x):
    return x / (np.std(x) + 1e-12)


def wind(seconds, rng):
    """Wind through spruces, stereo: layered bands that swell and ebb on their own slow gusts,
    needle hiss that rides the strongest gusts, and a faint rumble underneath."""
    n = int(SR * seconds)
    shared = smooth_curve(n, 0.2, rng)
    out = np.zeros((2, n))
    for ch in range(2):
        gust = 0.55 * shared + 0.45 * smooth_curve(n, 0.27, rng)
        gust = 0.15 + 0.85 * np.clip((gust - 0.15) / 0.7, 0, 1)
        body = np.zeros(n)
        for centre, weight, rate in ((170, 1.0, 0.17), (340, 0.9, 0.23), (640, 0.7, 0.29), (1100, 0.4, 0.37)):
            layer = unit(band(rng.standard_normal(n), centre * 0.6, centre * 1.5))
            body += weight * layer * (0.25 + 0.75 * smooth_curve(n, rate, rng) ** 1.4)
        needles = unit(band(rng.standard_normal(n), 2500, 6500)) * gust ** 2.6 * 0.28
        rumble = unit(lowpass(rng.standard_normal(n), 90)) * gust * 0.5
        out[ch] = body * gust ** 1.3 + needles + rumble
    return out / np.std(out)


def note(f0, f1, seconds, curve=1.0, wobble=0.0, rate=0.0):
    """One bird note: a whistle gliding from f0 to f1 Hz with a faint second partial."""
    tt = tvec(seconds)
    x = np.clip(tt / seconds, 0, 1)
    freq = f0 + (f1 - f0) * x ** curve
    if wobble:
        freq = freq * (1 + wobble * np.sin(2 * np.pi * rate * tt))
    phase = 2 * np.pi * np.cumsum(freq) / SR
    return (np.sin(phase) + 0.12 * np.sin(2 * phase)) * np.sin(np.pi * x) ** 0.7


def phrase(notes):
    """notes: (start seconds, note signal) pairs mixed into one phrase."""
    end = max(start + sig.size / SR for start, sig in notes)
    out = np.zeros(int(SR * end) + 1)
    for start, sig in notes:
        i = int(SR * start)
        out[i:i + sig.size] += sig
    return out


def great_tit():
    """The two-note 'tea-cher' repeated: a low then a high whistle."""
    parts = []
    for k in range(3):
        base = 0.34 * k
        parts.append((base, note(3600, 3350, 0.1)))
        parts.append((base + 0.15, note(5500, 5100, 0.09)))
    return phrase(parts)


def chaffinch():
    """A tumbling run of short notes that slides down the scale, then a small flourish."""
    parts = [(0.058 * k, note(5400 - 190 * k, 5000 - 190 * k, 0.045)) for k in range(11)]
    parts.append((0.78, note(3000, 5300, 0.12)))
    parts.append((0.93, note(5400, 3900, 0.1)))
    return phrase(parts)


def robin(rng):
    """The thin, wistful autumn song: a few falling whistles at uneven spacing."""
    parts = []
    start = 0.0
    for _ in range(6):
        high = rng.uniform(4400, 6800)
        parts.append((start, note(high, high * rng.uniform(0.62, 0.85), rng.uniform(0.07, 0.14), 1.3, 0.02, 38)))
        start += rng.uniform(0.11, 0.24)
    return phrase(parts)


def reverb(stereo, seconds, wet, rng, damp=4800):
    """Outdoor or room tail: decaying, low-passed noise convolved in; returns dry plus wet at the same length."""
    ir_len = int(SR * seconds)
    ir_t = np.arange(ir_len) / SR
    out = np.zeros_like(stereo)
    for ch in range(stereo.shape[0]):
        ir = lowpass(rng.standard_normal(ir_len) * np.exp(-ir_t * 6.9 / seconds), damp) * np.minimum(ir_t / 0.01, 1)
        ir /= np.sqrt(np.sum(ir ** 2))
        out[ch] = fftconvolve(stereo[ch], ir)[:stereo.shape[1]]
    return stereo * (1 - wet) + out * wet


def place(buffer, signal, at, pan, gain):
    """Mix a mono signal into a stereo buffer at `at` seconds; pan -1 left .. 1 right."""
    i = int(SR * at)
    n = min(signal.size, buffer.shape[1] - i)
    if n <= 0:
        return
    angle = (pan + 1) * np.pi / 4
    buffer[0, i:i + n] += signal[:n] * gain * np.cos(angle)
    buffer[1, i:i + n] += signal[:n] * gain * np.sin(angle)


def distant(signal, cutoff=6200):
    """Air and distance take the top off a close recording."""
    return lowpass(signal, cutoff)


def curve(seconds, points):
    """Piecewise-linear gain over a clip; points are (seconds, value) pairs."""
    times, values = zip(*points)
    return np.interp(tvec(seconds), times, values)


def write_stereo_wav(path, stereo):
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


def encode_ogg(stereo, path, lufs=None, peak=0.5):
    """Scale to a loudness target (or a peak ceiling), keep headroom for the Vorbis encode, write an .ogg."""
    stereo = stereo / np.max(np.abs(stereo)) * 0.5
    with tempfile.TemporaryDirectory() as tmp:
        wav = Path(tmp) / "mix.wav"
        if lufs is not None:
            write_stereo_wav(wav, stereo)
            stereo = stereo * 10 ** ((lufs - measure_lufs(wav)) / 20)
        else:
            stereo = stereo / np.max(np.abs(stereo)) * peak
        top = np.max(np.abs(stereo))
        if top > 0.89:
            stereo = stereo * 0.89 / top
        write_stereo_wav(wav, stereo)
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(wav),
                        "-c:a", "libvorbis", "-q:a", "5", str(path)], check=True)
    print(f"Wrote {path} ({stereo.shape[1] / SR:.1f} s, peak {20 * np.log10(np.max(np.abs(stereo))):.1f} dBFS)")
    return stereo
