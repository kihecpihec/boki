# Bellevue prologue: minimum slice

Implements [Story.md §3 PROLOGUE](../Story.md#prologue--the-morning-after-2030-seconds-skippable)
shots 1, 2 and 4 as a skippable ~20.5 s sequence, played for laughs: Boki's eyes pop out
twice and he scrambles onto the scooter. Shot 3 (the VHS glitch with the overturned
cars and the black figure) is **not built**; neither is the bass-to-wind audio. The
full prologue stays M10 in [Architecture.md](../Architecture.md#completion-of-the-documented-game).

| Beat | Shot | Seconds | What plays |
|---|---|---:|---|
| Wake | 1 | 4.5 | Fade in. The camera starts low at Boki's side in the grass, then rises and pulls back to the sunrise skyline. Boki lies sprawled, breathing with his mouth slack, beside a bottle, glow sticks, a party cup and the fallen scooter. |
| Stir | 2 | 1.8 | His left arm rises toward his face; the eyes crack open into tired slits. |
| Watch | 2 | 1.6 | The camera closes on the watch; the LCD fills the screen with **07:58**. |
| Double take | 2 | 1.7 | Cut to his face. A blank stare, then the eyes pop out (first pop) with a `!`, sweat drops, raised brows, a gaping mouth, a camera punch and a flash. The arm drops. |
| Message | 2 | 2.4 | Phone beep, subtitle »PREDAVANJE — 08:00 / FERI«. The bigger second pop: eyes on stalks, mouth wide, shaking, harder punch. |
| Line | 2 | 3.1 | Voice `fak_nespet.mp3` starts with subtitle »Fak ... ne spet.« The eyes sag back, the brows turn worried, the head droops; then fade to black. |
| Scramble | 4 | 2.4 | From black: Boki sits bolt upright and knocks the bottle away, springs up, sprints the wrong way, skids, turns back (the scooter flips upright) and leaps onto it. |
| Mount | 4 | 3.0 | Rider on the scooter, headlight on, `OBJECTIVE: GET TO FERI` / `DON'T LOOK BACK`, the camera swings behind him. |

Enter skips. Playing to the end and skipping reach the same end state: rider
visible, headlight on, camera on the `ThirdPersonCamera` rest pose, letterbox and skip
hint gone, then `ScooterController` and `ThirdPersonCamera` are re-enabled. The
objective stays on screen for six seconds after control returns. I replays the intro only in `Player_Model_Test`.

## Pohorje integration

`V1_Pohorje` now plays the intro at its authored player start, in the actual map,
then hands control to the existing rider without loading another scene.
`Intro_Preview` keeps its established path and GUID and uses a saved copy of that
integrated scene, including its map, lighting, player and gameplay wiring. The
original primitive meadow/skyline set is no longer the preview's environment.

`Boki/Intro/Integrate Pohorje Intro` upgrades `V1_Pohorje` in place, refreshes
`Intro_Preview`, and wires an opt-in intro into `Player_Model_Test` (press I).
It refuses Play Mode and dirty scenes and restores the previous scene setup.
It does not rebuild maps or change shared prefabs or build settings.
Production and preview disable replay so I cannot trigger a cutscene after riding
away from the start. Retry reloads V1_Pohorje and starts the intro again.

The existing `RunSession` owns `PauseReason.ScriptedSequence` while the intro runs:
physics, battery drain and gameplay pressure stay suspended. Presentation uses
unscaled time, and completing or skipping releases only the intro's pause reason.
V1's pause menu cannot take over the camera during the sequence. Movement,
follow camera and scooter presentation are disabled during the shots and restored
at the handoff. Gameplay canvases are hidden during the sequence and restored to their previous enabled state afterwards; the model test starts with the intro overlay hidden. The follow distance matches the scene's authored camera.

The reusable `Assets/Cutscenes/Intro/IntroCutscene.prefab`, intro Boki, audio,
acting, Slovenian lines and skip input are retained. No shared player or map prefab
is modified.

## The intro Boki

`Boki_Intro.prefab` is a separate Boki built only for the intro, so it can be bent and
customised without touching the player. It is the player model's neutral **bind-pose**
rig (`CHR_Boki_BindPose.fbx`, same 22 bones) with:

- its own atlas, `T_Boki_Intro_BaseColor.png`: the player atlas with the painted eyes,
  eyebrows, mouth and chin line blanked (`Source/output/intro-boki-v1/build_intro_atlas.py`);
  bags, nose shading, stubble, jacket and watch are unchanged;
- separate 3D face parts under a `Face` pivot on the head bone: flat eyes that swell into
  balls on skin-coloured stalks, pupils, brows, a mouth, closed-eye lash lines;
- cues above the head that face the camera: a `!` mark and sweat drops.

`IntroBoki` takes a `Pose` (rig angles about character-frame axes, plus lids, pop,
gaze, brows, mouth, shake, alert, sweat) and writes transforms. It owns no timing.
`IntroActing` is the whole script of the intro as a pure function of (shot, seconds), so
playing, skipping and the frame capture show identical poses. To change a gag, edit
`IntroActing` (timings, pose presets) or the face layout constants in `IntroBoki`; neither
needs the prefab rebuilt. `IntroMotion` (engine-free, tested) holds the cartoon curves:
`Pop` (rush, overshoot, settle), `Impact` and `Tremor`.

## The prefab

`IntroCutscene.prefab` is self-contained: `Intro Stage` (the `Boki Intro` instance,
fallen scooter, bottle, leftovers, a cold stage light), `Intro Overlay` (letterbox,
07:58 LCD, subtitle with a backing band, objective, skip hint, flash, fade), an
`AudioSource` and the `IntroCutscene` component with all of those already wired. It
carries no ground and no map reference, so it sits on whatever floor the host scene has.

Six scene-side references are left empty on purpose and are assigned per scene:
`shotCamera` (the camera running `ThirdPersonCamera`), `rider` (the player's
`Visuals` child), `riderRoot` (the player root), `followPivot` (the player's
`CameraPivot`), `headlight` (the player's `LightBattery`) and `controls`
(`ScooterController`, `ThirdPersonCamera`, and optional `ScooterPresentation`, disabled during the intro).
`playOnStart` is off in the prefab; the preview scene turns it on. Missing
references log an error and disable the component rather than failing silently.

Sounds: `intro_phone_beep.wav`, `intro_eye_pop.wav` (played at both pops) and
`intro_dash.wav` (the sprint). All are synthesised by `python3 Source/music/generate_intro_sfx.py`.

## Setup commands

All refuse Play Mode and dirty scenes.

- **Boki > Intro > Rebuild Intro Preview**: regenerates `Boki_Intro.prefab`,
  `IntroCutscene.prefab`, the materials, the clip import settings and `Intro_Preview`,
  keeping every asset path and GUID. Unity gives a rebuilt prefab new internal ids, so
  the command **refuses once any other scene uses `IntroCutscene.prefab`**; after
  adoption, edit the prefab directly. It discards hand edits to the preview scene.
- **Boki > Intro > Open Intro Preview**.
- **Boki > Intro > Add Intro To Open Scene**: the integration step, see below.
- **Boki > Intro > Capture Preview Frames**: renders 28 review frames covering every
  beat to `Source/output/intro-preview/frames/` (or `$BOKI_INTRO_FRAMES`) without saving the
  scene. Run it without `-nographics`: frames need the GPU.

## Manual verification

1. Open `Intro_Preview`, enter Play Mode, click the Game view. The sequence starts
   on its own. Check each beat in the table: the eyes pop at 07:58 and again on the
   beep, the subtitle text (diacritics and »« intact), the pop and sprint sounds, and
   the legibility of the 07:58 LCD.
2. Let it finish. Boki rides; WASD, Space and Shift work; the camera follows with
   no jump at the handoff; the headlight is lit; no letterbox or skip hint remains.
3. Play again and press Enter mid-way (once in the wake-up, once during the message).
   The end state must be identical to playing through, with the beep silenced if it
   was playing. Pressing Enter after the end does nothing.
4. In `Player_Model_Test`, press I at the authored start: it replays with controls locked. V1 and Intro_Preview intentionally ignore replay.
5. Exit Play Mode; the cursor state is restored. The Console shows no new errors
   or warnings from the intro. Enter Play Mode twice without a domain reload: no
   duplicated input or objects.

## Generic adoption into another map

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
   and lighting in that map: the wake-up and face shots assume light from above and a
   few metres of clear ground; the face shots use the stage light inside the prefab.

By hand the same thing is: drag the prefab in, move it, and fill the references
listed under "The prefab". Environment geometry and lighting belong to the host scene, not the prefab.

## Original isolated-preview verification record

Unity `6000.6.7f1`, run headless on Linux (AMD Radeon 780M for rendering).

- Compile and EditMode tests: scripts compile with no errors; 148 of 148 tests pass,
  including 17 intro tests: the timeline (now with `ShotStart`, `Elapsed` and
  `TotalSeconds`) and `IntroMotion` (`Pop` is 0 before and exactly 1 after its duration
  and overshoots in between; `Impact` only falls; `Tremor` stays inside its envelope;
  invalid parameters rejected).
- Setup: the Editor generated every `.meta`. Existing materials and prefabs were
  rewritten in place and kept their GUIDs. A dependency scan of the scene and both
  prefabs found no map, level, route or enemy asset; the only shared assets are the
  player test prefab and its models, atlas and input.
- Play Mode (headless, temporary driver that has been removed; keyboard input was
  injected with background and focus filtering turned off): controls locked at the
  start with the eyes flat; the first pop bulged the eyes and played its sound; the
  beep played with the subtitle shown; the second pop pushed the eyes further than the
  first; the line subtitle and the sprint sound fired; played to the natural end
  the rider, headlight, controls and objective were on, letterbox gone, and the
  camera sat exactly on the follow rest distance (gap 0.00 m); Enter after the end
  changed nothing; I replayed and relocked the controls; Enter during the wake and
  during the message reached the same end state, and the message skip silenced the
  sound; a second replay and skip also worked. No console errors from the intro; one exception
from Unity's own search indexer at Editor start-up appeared in the log and was ignored.
- That run found and fixed a bug in the earlier intro: after a natural finish the
  frame update ran once more and brought the letterbox and skip hint back, so playing
  through and skipping did not end identically.
- Frames: the 28 captured frames were reviewed (wake, stir, double take, both pops, the
  deflate, sit-up, sprint, skid, leap, ride-off, follow pose). They are Edit Mode
  renders with the player's lights off, so they do not show the headlight.
- Sound: the new clips were checked numerically (48 kHz mono, 0.5 peak, no DC offset,
  silent at both ends); the phone beep is byte-identical after regeneration.
- Architecture audit: no failures; the one new-scene warning is `Intro_Preview`
  missing from the build list, as for the other previews.

Not verified: how the new sounds actually sound or how loud they are, interactive
mouse and keyboard play, anything on Windows or the 5600G, and the intro inside a real
map. The total length is 19.4 s, a little under Story §2's 20–30 s budget; lengthen
`IntroActing.ShotSeconds` if that matters. Known rough edges: the 07:58 readout is a UI
overlay, not the 3D watch (going by the model's build script, the watch face points back
along the forearm, so it cannot face the camera while the arm is raised); the mouth is a flat box, not a jaw;
the set uses a procedural sky and primitive meshes. The preview sky is a clean sunrise,
calmer than Story §3's "something wrong with the sky's color"; tint `DawnSky.mat`
and the fog in the scene if the map should look sicker.

## Original deferred work

Shot 3 (VHS glitch, cars, black figure), distant techno bass fading into wind, a
Guilt silhouette, the objective as part of the real HUD, wiring into `Level1_Pohorje`,
a Windows build check, authored animation clips in place of pose code if the gags grow.

## Pohorje change verification

Open `V1_Pohorje` and enter Play Mode: the wake-up, watch, reaction, scramble
and mount must play against the real Pohorje scenery. Check each camera shot for
terrain/prop clipping. Enter must skip to the same rider/camera pose; riding,
boost, laser, HUD and route choices must work afterwards. Battery must stay full
through the intro. Escape must not open the V1 pause menu during it; pause/resume
must work after the handoff. Retry must replay the intro with fresh run state.
Open `Intro_Preview` and repeat. In `Player_Model_Test`, I starts the same acting
at the authored test start. Inspector references are assigned by the integration
helper; no manual assignments are required.

Manual visual/interaction verification and Windows player performance remain
required; batch import and tests alone do not establish camera framing quality.

Checks for this integration: Unity 6000.6.7f1 imported and saved all three scenes;
152/152 EditMode tests passed. All 28 review frames rendered successfully, with
wake, reaction, scramble and mount frames inspected against the Pohorje setting.
The architecture audit and `git diff --check` passed. Full interactive Play Mode
and Windows player checks have not been performed.

## Spoken line cue

`IntroCutscene.bokiLine` references `Assets/Audio/Sfx/fak_nespet.mp3` on the shared intro prefab. The voice starts when the Line shot begins (12 seconds into the sequence). That shot now lasts 3.1 seconds to fit the approximately 3.03-second recording before Scramble; total runtime is 20.5 seconds. Existing finish/skip handling stops the audio. The preview builder preserves this assignment.

Manual check: play `V1_Pohorje`, listen for the line with its subtitle, and confirm it finishes before the scramble. Retry should play it once again. Skip during the line should silence it immediately. In `Player_Model_Test`, press I to replay and repeat the skip check; check the Console for new errors.
