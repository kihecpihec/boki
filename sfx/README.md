# Recorded SFX

The 13 original recordings are preserved at the top of this folder. The four
QuickTime (`.qt`) recordings also have full-length MP3 conversions beside them.
Use the isolated clips below for individual playback.

## Isolated clips

- `variations/enemy_damage/`: 10 short enemy damage grunts.
- `variations/enemy_death/`: 4 longer enemy death groans.
- `variations/guilt/`: 2 Guilt variations.
- `variations/laser/`: 9 laser variations.
- `variations/boki_damage/`: 3 Boki damage variations.
- `variations/boki_reaction/`: 2 reactions from `A.mp3`.
- `variations/teleport/`: 2 teleport variations.
- `dialogue/boki_koliko_je_ura.mp3`: the professor's opening question.
- `dialogue/boki_danes_je_sobota.mp3`: the professor's Saturday reveal.

There are **34 isolated clips** in total. Numbering within each category follows
the source recording order. Enemy damage/death categories use the approved
short-grunt/long-groan grouping; they are suggested uses rather than spoken labels
in the recording. The manifest retains the original enemy take numbers, including
the longer seventh take assigned to death.

## Processing and provenance

`manifest.json` records original file hashes, full conversions, each isolated
clip's source recording, and its exact start/end times in source seconds.

MP3s are encoded with FFmpeg's `libmp3lame` at VBR quality 2, directly from the
original recordings. Sample rates and channel counts are preserved. Boki damage
exports are reduced by 7.4 dB and teleport exports by 2.1 dB to leave decoding
headroom: their unattenuated exports had peaks above full scale. The same gain is
used across each recording's variations, preserving their relative dynamics.
Other exports retain their recording gain. Original files are unchanged; this
attenuation does not repair any distortion already present in a source.
Isolated clips have short fades (3 ms in, 8 ms out) to avoid edge
clicks. Leading/trailing gaps are trimmed with room for attacks and tails;
internal pauses within dialogue lines and multi-part effects are retained.
Very brief isolated handling clicks are excluded. No upward normalization, pitch
effects, or denoising has been applied. The manifest records each clip's gain.

This folder retains the prepared source recordings. Runtime copies are imported
as mono PCM WAV assets in `Game/Assets/Audio/Sfx/Recorded/` and wired into the V1
scenes and `Player_Model_Test`. See
[recorded SFX wiring and verification](../docs/plans/recorded-sfx-verification.md).
Decode, duration, and original-hash checks validate these MP3 exports; final
listening and mix review remain separate.
