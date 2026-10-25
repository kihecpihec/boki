"""Generate original Boki gameplay sounds using only Python's standard library.

Run from any directory. Assets are mono 24 kHz / 16-bit PCM; no third-party samples.
"""
import math
import random
import wave
from array import array
from pathlib import Path
import sys

RATE = 24000
OUT = Path(__file__).resolve().parents[2] / 'Game/Assets/Audio/Sfx/Player'
OUT.mkdir(parents=True, exist_ok=True)
rng = random.Random(2401)


def write(name, seconds, sample, loop=False):
    count = int(seconds * RATE)
    pcm = array('h')
    for i in range(count):
        t = i / RATE
        envelope = 1 if loop else min(1, t / 0.008) * min(1, (seconds - t) / 0.025)
        value = max(-0.95, min(0.95, sample(t) * envelope))
        pcm.append(int(value * 32767))
    if sys.byteorder != 'little':
        pcm.byteswap()
    with wave.open(str(OUT / (name + '.wav')), 'wb') as wav:
        wav.setparams((1, 2, RATE, count, 'NONE', 'not compressed'))
        wav.writeframes(pcm.tobytes())


def sine(hz, t):
    return math.sin(math.tau * hz * t)


def noise():
    return rng.uniform(-1, 1)


def sweep(start, end, duration, t):
    return math.sin(math.tau * (start * t + (end - start) * t * t / (2 * duration)))


def chime(notes, step, t):
    n = min(len(notes) - 1, int(t / step))
    age = t - n * step
    return (sine(notes[n], age) + 0.25 * sine(notes[n] * 2, age)) * math.exp(-age * 14) * 0.32


# Integer harmonics over one second make a seamless motor loop.
write('motor', 1, lambda t: 0.26 * sine(90, t) + 0.12 * sine(180, t)
      + 0.05 * sine(360, t) * (0.6 + 0.4 * sine(15, t)), loop=True)
write('jump', .22, lambda t: .38 * sweep(240, 850, .22, t) * math.exp(-t * 9) + .07 * noise() * math.exp(-t * 18))
write('landing', .24, lambda t: (.5 * sweep(130, 45, .24, t) + .21 * noise()) * math.exp(-t * 20))
write('boost', .4, lambda t: (.22 * sweep(140, 950, .4, t) + .2 * noise()) * math.sin(math.pi * t / .4))
write('shot', .13, lambda t: (.38 * sweep(1800, 180, .13, t) + .07 * noise()) * math.exp(-t * 22))
write('batteryPickup', .48, lambda t: chime([440, 660, 880, 1320], .12, t))
write('xpPickup', .18, lambda t: chime([1046.5, 1568], .09, t))
write('damage', .25, lambda t: (.22 * noise() + .3 * sweep(180, 65, .25, t)) * math.exp(-t * 12))
write('death', .85, lambda t: (.32 * sweep(320, 35, .85, t) + .1 * noise()) * math.exp(-t * 3))
write('batteryLow', .48, lambda t: .27 * sine(660, t) if t < .12 or .24 < t < .36 else 0)
write('batteryEmpty', .6, lambda t: chime([440, 330, 220], .2, t))
write('guiltHeartbeat', .65, lambda t: sum(.6 * sine(65, t - onset) * math.exp(-(t - onset) * 22)
      for onset in [0, .24] if t >= onset))
print(f'Generated 12 original sounds in {OUT}')
