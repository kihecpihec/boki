#!/usr/bin/env python3
"""Original sample-free, seamless 16-bar gameplay synth loops. Requires numpy."""
from pathlib import Path
import wave
import numpy as np

SR = 32000
OUT = Path(__file__).resolve().parents[1] / 'Game/Assets/Audio/Music'


def render(name, bpm, root, intensity, seed):
    beat = 60 / bpm
    n = round(64 * beat * SR)
    mix = np.zeros((n, 2))
    rng = np.random.default_rng(seed)

    def note(at, duration, frequency, gain, pan=0, kind='tone'):
        t = np.arange(round(duration * SR)) / SR
        envelope = np.minimum(t / .012, 1) * np.exp(-t / (duration * .28))
        if kind == 'kick':
            sound = np.sin(2 * np.pi * (42 * t + 7 * (1 - np.exp(-30 * t))))
        elif kind == 'hat':
            noise = rng.uniform(-1, 1, len(t))
            sound = noise - np.roll(noise, 1)
        else:
            sound = np.sin(2 * np.pi * frequency * t) + .2 * np.sin(4 * np.pi * frequency * t)
        signal = sound * envelope * gain
        indexes = (round(at * beat * SR) + np.arange(len(t))) % n
        mix[indexes, 0] += signal * (1 - pan * .4)
        mix[indexes, 1] += signal * (1 + pan * .4)

    def hz(midi):
        return 440 * 2 ** ((midi - 69) / 12)

    progression = [0, -3, -5, -2]
    melody = [12, 19, 15, 22, 19, 10, 15, 14]
    for bar in range(16):
        tonic = root + progression[(bar // 4) % 4]
        for step in range(4):
            at = bar * 4 + step
            note(at, .32, 0, .28 * intensity, kind='kick')
            note(at + .5, .07, 0, .025 * intensity, .35, 'hat')
            note(at + .5, beat * .8, hz(tonic), .18)
        for step in range(8):
            if (step + bar) % 3 == 0:
                continue
            at = bar * 4 + step * .5
            pitch = hz(tonic + melody[(step + bar // 4) % 8])
            note(at, beat * 1.4, pitch, .055, (-1) ** step * .6)
            note(at + .75, beat * 1.4, pitch, .016, (-1) ** (step + 1) * .6)
        for interval in [12, 15, 19]:
            note(bar * 4, beat * 5, hz(tonic + interval), .035, .15)
    mix = np.tanh(mix * 1.2)
    assert np.isfinite(mix).all() and np.max(np.abs(mix)) < 1
    pcm = (mix * 32767).astype('<i2').tobytes()
    with wave.open(str(OUT / (name + '.wav')), 'wb') as output:
        output.setnchannels(2)
        output.setsampwidth(2)
        output.setframerate(SR)
        output.writeframes(pcm)
    print(name, f'{n / SR:.2f}s', f'peak={np.max(np.abs(mix)):.3f}')


if __name__ == '__main__':
    render('pohorje_gameplay', 96, 38, .65, 101)
    render('maribor_gameplay', 120, 40, 1, 102)
    render('feri_gameplay', 80, 35, .3, 103)
