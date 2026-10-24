# Bellevue prologue: minimum slice

Implements [Story.md §3 PROLOGUE](../Story.md#prologue--the-morning-after-2030-seconds-skippable)
shots 1, 2 and 4 as a skippable ~16 s sequence. Shot 3 (the VHS glitch with the
overturned cars and the black figure) is **not built**; neither is the bass-to-wind
audio. The full prologue stays M10 in [Architecture.md](../Architecture.md#completion-of-the-documented-game).

| Beat | Shot | Seconds | What plays |
|---|---|---:|---|
| Wake | 1 | 5.0 | Fade in from black. The camera starts low beside Boki, his scooter and a bottle, then rises and pulls back to a foggy skyline. |
| Arm raise | 2 | 1.8 | Boki raises his left arm. |
| Watch | 2 | 1.7 | The camera closes on the watch; the LCD fills the screen with **07:58**. |
| Message | 2 | 2.2 | Phone beep, subtitle »PREDAVANJE — 08:00 / FERI«. |
| Line | 2 | 2.0 | Subtitle »Fak ... ne spet.« over a close shot of Boki's face, then fade to black. |
| Mount | 4 | 3.0 | Boki is on the scooter, the headlight turns on, `OBJECTIVE: GET TO FERI` / `DON'T LOOK BACK`, the camera swings behind him. |

Enter skips. Playing to the end and skipping reach the same end state: rider
visible, headlight on, camera on the `ThirdPersonCamera` rest pose, then
`ScooterController` and `ThirdPersonCamera` are re-enabled. The objective stays
on screen for six seconds after control returns. I replays the intro.

## Ownership and isolation

Everything here is new; no existing file was edited, and no map is involved.

- `Assets/Scenes/Intro_Preview.unity`: the intro's own scene. It is a neutral
  sound stage built from primitives (flat ground, a fogged skyline of dark blocks,
  a few dead trees, dawn fog and light). It uses **no** Pohorje, Maribor or FERI
  asset and none of the level or map-test scenes. It is not in the build list,
  like the other previews.
- `Assets/Cutscenes/Intro/`: `IntroCutscene.prefab` (the whole intro),
  `IntroInput.asset` (`Skip` = Enter, `Replay` = I) and three small materials.
- It does depend on the Boki player (`Boki_Rigged_Test.prefab`, the Boki and
  scooter models and atlas) and on the shared movement input assets, because the
  intro hands control to that player. It does not modify them.
- `Player_Model_Test.unity` is deliberately **not** touched: the intro is kept out
  of shared and map scenes so it cannot collide with other work.

## The prefab

`IntroCutscene.prefab` is self-contained: `Intro Stage` (lying Boki, fallen
scooter, bottle, a cold stage light), `Intro Overlay` (letterbox, 07:58 LCD,
subtitle, objective, skip hint, fade), an `AudioSource` and the `IntroCutscene`
component with all of those already wired. It carries no ground and no map
reference, so it sits on whatever floor the host scene has.

Six scene-side references are left empty on purpose and are assigned per scene:
`shotCamera` (the camera running `ThirdPersonCamera`), `rider` (the player's
`Visuals` child), `riderRoot` (the player root), `followPivot` (the player's
`CameraPivot`), `headlight` (the player's `LightBattery`) and `controls`
(`ScooterController` and `ThirdPersonCamera`, both disabled during the intro).
`playOnStart` is off in the prefab; the preview scene turns it on. Missing
references log an error and disable the component rather than failing silently.

## Setup commands

All are idempotent and refuse Play Mode and dirty scenes.

- **Boki > Intro > Create Intro Preview**: imports `intro_phone_beep.wav`, creates
  the folder assets, the prefab and `Intro_Preview`. An existing scene is kept.
- **Boki > Intro > Open Intro Preview**.
- **Boki > Intro > Add Intro To Open Scene**: the integration step, see below.
- **Boki > Intro > Capture Preview Frames**: renders twelve review frames to
  `output/intro-preview/frames/` without saving the scene.

Regenerate the beep with `python3 music/generate_intro_sfx.py`.

## Manual verification

1. Open `Intro_Preview`, enter Play Mode, click the Game view. The sequence starts
   on its own. Check each beat in the table, the subtitle text (diacritics and
   »« intact), the beep, and the legibility of the 07:58 LCD.
2. Let it finish. Boki rides; WASD, Space and Shift work; the camera follows with
   no jump at the handoff; the headlight is lit.
3. Play again and press Enter mid-way (once in the wake-up, once in the watch
   shot). The end state must be identical to playing through, with the beep
   silenced if it was playing. Pressing Enter after the end does nothing.
4. Press I after the end: it replays and the player controls are locked meanwhile.
5. Exit Play Mode; the cursor state is restored. The Console shows no new errors
   or warnings from the intro. Enter Play Mode twice without a domain reload: no
   duplicated input or objects.

## Adding it to a map later

Agree it with whoever owns the map first. The scene must already have the player
and its follow camera:

1. Open the map scene (it must be saved and clean).
2. **Boki > Intro > Add Intro To Open Scene**. This instantiates the prefab, puts
   its stage at the player's start facing the player's heading, assigns the
   scene-side references above, leaves `playOnStart` off and leaves the scene
   modified but unsaved.
3. Review, then save. If the ground at the player's start is uneven, move
   `Intro Cutscene/Intro Stage` so Boki lies on it (the stage's +Z must face the
   direction of travel; the fallen scooter lies where the player starts).
4. Call `IntroCutscene.Play()` from the New Run flow once `RunSession` accepts the
   run ([Gameplay.md §1](../Gameplay.md#1-main-gameplay-flowchart): *Bellevue intro
   07:58 or Skip*). The intro owns no run state, routes or unlocks and leaves them
   untouched. A scene without a `LightBattery` on the player cannot adopt it.
5. Add the scene to the build list during integration, not here. Re-check the fog
   and lighting in that map: the intro's wake-up shots assume the scene's own.

By hand the same thing is: drag the prefab in, move it, and fill the references
listed under "The prefab".

## Verification record

Unity `6000.6.7f1`, run headless on Linux (AMD Radeon 780M for rendering).

- Compile and EditMode tests: scripts compile with no errors; 125 of 125 tests
  pass, including eight `IntroTimeline` tests (one-shot begin, progress, a delta
  spanning several shots, finishing once, skip finishing once and stopping later
  shots, skip before begin, invalid deltas, constructor limits).
- Setup: the Editor generated every `.meta`. A second **Create Intro Preview**
  left the scene and prefab byte-identical. A dependency scan of the scene and the
  prefab found no map, level or map-test asset.
- Play Mode (headless, temporary driver that has been removed): the intro locked
  the controls at the start; Enter mid wake-up reached the full end state (rider,
  headlight, objective, clear overlay); I relocked and restarted it; played to the
  natural end the camera sat exactly on the `ThirdPersonCamera` rest pose (gap
  0.00 m) and the beep source was playing during the message shot; no console
  errors, exceptions or warnings.
- Adoption path (temporary scratch scene, since deleted): **Add Intro To Open
  Scene** assigned every reference, placed the stage from a player standing at a
  rotated position, left `playOnStart` off, left the scene modified, and a second
  call did not add a duplicate. It was not run on any real scene.
- Frames: the twelve captured frames were reviewed and read correctly (wake-up,
  raised arm, LCD, both subtitles, face shot, mount, follow pose). They are Edit
  Mode renders with the player's lights off, so they do not show the headlight.
- Architecture audit: no failures; the one warning is `Intro_Preview` missing from
  the build list, as for the other previews.
- Git: no tracked file is modified. Hand-written files are free of trailing
  whitespace; Unity-generated YAML keeps the Editor's trailing spaces on empty
  values.

Not verified: interactive mouse and keyboard play, audible playback and level of
the beep, anything on Windows or the 5600G, and the intro inside a real map.
Known rough edges: the imported rig only has the riding pose, so lying Boki keeps
bent knees and raised hands; the watch readout is a UI overlay, not the 3D watch;
the bottle is a cylinder; the sound stage is deliberately plain.

## Deferred

Shot 3 (VHS glitch, cars, black figure), distant techno bass fading into wind, a
real get-up and mount animation (poses are bone rotations, not authored clips),
a Guilt silhouette, the objective as part of the real HUD, wiring into
`Level1_Pohorje`, a Windows build check.
