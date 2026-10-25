"""Original, seeded environmental audio. No external samples or dependencies.

Run with Python 3, then import Assets/Audio/Ambience in Unity 6000.6.7f1.
Mono 24 kHz PCM; loops use periodic modulation and warmed circular noise filters.
"""
import math
import random
import sys
import wave
from array import array
from pathlib import Path

RATE = 24000
OUT = Path(__file__).resolve().parents[2] / "Game/Assets/Audio/Ambience"


def noise(seconds, cutoff, seed):
    rng = random.Random(seed)
    raw = [rng.uniform(-1, 1) for _ in range(int(seconds * RATE))]
    alpha = 1 - math.exp(-math.tau * cutoff / RATE)
    state = 0.0
    # Warm the filter with the same periodic input before capturing the loop.
    for value in raw:
        state += alpha * (value - state)
    result = []
    for value in raw:
        state += alpha * (value - state)
        result.append(state)
    rms = math.sqrt(sum(x * x for x in result) / len(result))
    return [x / rms for x in result]


def write(name, samples, loop=False):
    mean = sum(samples) / len(samples)
    samples = [x - mean for x in samples]
    gain = 0.55 / max(abs(x) for x in samples)
    pcm = array("h")
    for i, value in enumerate(samples):
        edge = 1 if loop else min(1, i / (RATE * .08), (len(samples) - 1 - i) / (RATE * .25))
        pcm.append(round(value * gain * edge * 32767))
    if sys.byteorder != "little":
        pcm.byteswap()
    with wave.open(str(OUT / (name + ".wav")), "wb") as wav:
        wav.setparams((1, 2, RATE, len(pcm), "NONE", "not compressed"))
        wav.writeframes(pcm.tobytes())
    print(f"{name}: {len(pcm) / RATE:.1f}s, peak={max(abs(x) for x in pcm) / 32768:.3f}")


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    seconds = 24
    low = noise(seconds, 160, 311)
    air = noise(seconds, 950, 312)
    for name in ("forest_wind", "city_air", "feri_ventilation"):
        samples = []
        for i in range(len(low)):
            t = i / RATE
            swell = .7 + .18 * math.sin(math.tau * t / 24) + .12 * math.sin(math.tau * t / 8)
            if name == "forest_wind":
                value = swell * (.75 * low[i] + .2 * air[i])
            elif name == "city_air":
                rumble = .18 * math.sin(math.tau * 47 * t) + .08 * math.sin(math.tau * 73 * t)
                value = swell * (.75 * low[i] + .08 * air[i]) + rumble
            else:
                fan = .26 * math.sin(math.tau * 100 * t) + .08 * math.sin(math.tau * 150 * t)
                value = .65 * low[i] + .12 * air[i] + fan * swell
            samples.append(value)
        write(name, samples, loop=True)

    air = noise(5, 1800, 313)
    leaf = noise(5, 5000, 314)
    write("foliage_rustle", [(air[i] * .25 + leaf[i] * .08)
          * math.sin(math.pi * i / len(air)) ** 2
          * (.6 + .4 * math.sin(math.tau * 5.4 * i / RATE) ** 2) for i in range(len(air))])

    bird = []
    for i in range(3 * RATE):
        t = i / RATE
        value = 0
        for start, hz in ((.4, 1550), (.72, 1750), (1.8, 1420)):
            age = t - start
            if 0 < age < .22:
                value += math.sin(math.pi * age / .22) ** 2 * math.sin(
                    math.tau * (hz * age + 600 * age * age + .9 * math.sin(22 * age)))
        bird.append(value)
    write("distant_bird", bird)

    gust = noise(6, 280, 315)
    write("street_gust", [x * math.sin(math.pi * i / len(gust)) ** 2 for i, x in enumerate(gust)])
    metal = []
    pipe = []
    for i in range(4 * RATE):
        t = i / RATE
        metal.append(sum(math.sin(math.tau * hz * t) * math.exp(-t * decay)
                         for hz, decay in ((387, 2.4), (613, 3.8), (1031, 5.2))))
        pipe.append(math.sin(math.pi * t / 4) ** 2 * (
            math.sin(math.tau * (128 * t + 7 * t * t))
            + .3 * math.sin(math.tau * (267 * t + 3 * t * t))))
    write("distant_metal", metal)
    write("pipe_creak", pipe)
    tick = noise(2, 2300, 316)
    write("electrical_tick", [x * sum(math.exp(-(i / RATE - start) * 55)
          for start in (.12, .46) if i / RATE >= start) for i, x in enumerate(tick)])


if __name__ == "__main__":
    main()
