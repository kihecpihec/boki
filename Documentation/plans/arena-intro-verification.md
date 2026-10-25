# First-arena scare: the enemies' introduction

Implements the first-arena beat of [Story.md §3 LEVEL 1](../Story.md#level-1--pohorje-wrong-way-home-23-minutes) as a
skippable 14.6 s sequence. It plays in `V1_Pohorje` the first time Boki rides into the combat space of whichever route he
chose, then hands over to the normal arena (the opening group floods in as before). It follows the Bellevue intro's and the
ending's design ([intro](intro-preview-verification.md), [ending](ending-preview-verification.md)): a self-contained
prefab, a pure acting function, the same cutscene-only Boki.

| Beat | Shot | Seconds | What plays |
|---|---|---:|---|
| Hush | Hush | 1.6 | Starts on the gameplay camera, then eases back to behind Boki's left shoulder. The world falls silent (the shared pause stops the map ambience). At 0.65 s a twig snaps in the dark and something moves through the undergrowth. |
| Freeze | Startle | 1.5 | Cut to Boki's face from ahead. The eyes pop out on stalks with the eye-pop sound, a `!` and sweat, a jolt and a small flash. |
| Line | Line | 2.3 | He scans left, then right, shaking. The recorded `kaj_je_to_blo.wav` with the subtitle »Kaj je to blo?« (0.3 to 2.05 s). |
| Look | Look | 2.2 | The rider again, facing the dark. The camera pans from his shoulder down the lane. The enemy starts creeping far off, barely lit. |
| Creep | Creep | 6.0 | Tight over his shoulder, his head a dark shape at the edge, the enemy in the middle. It creeps toward him in nine stop-and-go lurches, with a dragging footstep sound per step and, at 3.6 s of creeping, its recorded greeting. The lens slowly tightens. |
| Lunge | Lunge | 1.0 | It stops 8.5 m away, then lunges. The synthesised stinger lands at 0.3 s with a white flash and camera punch; the flash is still there at the cut. |

Enter skips. Playing to the end and skipping call the same finish: the camera is put back exactly where it was, Boki's
heading is restored, the scooter controls come back (from rest: the scooter's momentum resets when its controller is
disabled), the gameplay canvases are restored and the scripted-sequence pause is released. A natural finish carries the lunge's
white frame over the cut and fades it out in the first moments of gameplay, to mask the swap to the real enemies; a skip does not.

## Wiring

`ArenaIntroCutscene` takes the route combat spaces (`BatteryCombatSpace`) and starts the first time one is occupied while
gameplay is active. It runs before `RouteEncounter` (`DefaultExecutionOrder(-50)`) and holds `PauseReason.ScriptedSequence`, so
the opening group's first enemy is not placed until the scene ends. It plays once per scene load; `Retry` reloads
`V1_Pohorje`, so it plays again each run (skippable). The preview keeps an `R` replay; game scenes clear it.

The enemy walks down a **lane** found when the scene starts: the first of 12 bearings (in front of the camera first, then
fanning out) that is clear of environment colliders for 22 m, then 17 m, then 13 m, with ground under the whole approach.
Nothing in the arena has to be authored for it. If no lane exists the scene logs a warning and does not play, so a run never
stalls on it. The shot camera is kept out of walls and trees with a sphere cast, like the follow camera.

The creeper is the forest enemy's model, material and animator with none of its gameplay (`CHR_Melee.fbx`,
`M_Melee_Forest.mat`, `CHR_Melee.controller`). Its pose is a pure function: the `Melee_Pursuit` cycle is walked by distance, one
cycle per lurch, so every rest is the same pose and there is no foot sliding to fix. The close-up Boki is the existing
`Boki_Intro.prefab`, lit only by the rider-layer lights.

`kaj_je_to_blo.wav` is also Boki's recorded Guilt-warning reaction (`PlayerAudio.guiltReaction`, once per scene). Nothing was
changed there; see the observations below. The scene plays the enemies' recorded `greeting.wav`, and tells `RecordedGameAudio`
(`MarkEnemyGreetingPlayed`) so the first real enemy does not repeat it; if the scene is skipped before the greeting, the first
real enemy plays it as before.

## The prefab and tools

`Assets/Cutscenes/ArenaIntro/ArenaIntroCutscene.prefab` holds the stage, the close-up Boki and its light, the creeper with its
positional voice and footstep sources, the overlay (`ArenaIntroOverlay`: letterbox, subtitle, flash, skip hint), the audio
source and the `ArenaIntroCutscene` with everything already wired. Scene-side the host assigns `arenas`, `shotCamera`,
`riderRoot`, `rider`, `controls`, `gameplayCanvases` and optionally `recordedAudio`. Sounds `arena_intro_rustle`,
`arena_intro_step` and `arena_intro_stinger` are synthesised by `python3 Source/music/generate_arena_intro_sfx.py`; the eye pop reuses
`intro_eye_pop.wav`.

All commands refuse Play Mode and dirty scenes.

- **Boki > Arena Intro > Rebuild Arena Intro Preview**: regenerates the prefab, the materials, the clip import settings and
  `ArenaIntro_Preview`, keeping every asset path and GUID. It **refuses once another scene uses the prefab**, because Unity
  gives a rebuilt prefab new internal ids.
- **Boki > Arena Intro > Open Arena Intro Preview**.
- **Boki > Arena Intro > Add Arena Intro To Open Scene**: the generic adoption step for another map (the scene needs the
  player, its `ThirdPersonCamera` and at least one `BatteryCombatSpace`). Leaves the scene modified but unsaved.
- **Boki > Arena Intro > Integrate Pohorje Arena Intro**: adds the prefab instance and its references to `V1_Pohorje`. See
  "The scene diff" below before using it on a changed scene.
- **Boki > Arena Intro > Run Preview Checks** and **Run Pohorje Checks**: headless Play Mode checks (below) that also render
  review frames to `Source/output/arena-intro-preview/frames` and `Source/output/arena-intro-pohorje/frames` (or `$BOKI_ARENA_INTRO_FRAMES`).
  Run without `-nographics`: frames need the GPU. The Pohorje run never saves the scene.

### The scene diff

A script-driven save of `V1_Pohorje` makes Unity re-serialise the whole file (about 17 000 changed lines: reordered
modification lists, serialized defaults, a stale `m_MaxSize`), which would be miserable to merge. The integration was
therefore applied as a text patch: **138 added lines and none removed**, one `PrefabInstance` document plus its entry in
`SceneRoots`. If you integrate a changed scene, run the command, keep Unity's output aside, `git checkout` the scene and
patch in only the new `PrefabInstance` and the `SceneRoots` line. `git diff --numstat` should show additions only.

## Manual verification

1. Open `ArenaIntro_Preview` and enter Play Mode. Hold W and ride forward into the clearing. The scene should start at
   the clearing's edge and run about 14.6 s. Check each beat in the table: the quiet and the snap, the eye pop and `!`, the
   subtitle's diacritics and »«, the creeper emerging from the dark and its steps and greeting, the lunge and flash. Enter
   skips; R replays.
2. In `V1_Pohorje`, choose route A (or B) and ride into the arena. Check the camera against the trees and walls on the way
   in from different directions, that no enemy appears until the scene ends, that Escape does not open the pause menu during
   it, and that the HUD is hidden and returns. After it, Boki is at rest, the camera is where it was and the arena floods.
3. Die and Retry: the scene plays again on the next arena entry. Enter skips it every time.
4. The Console shows no new errors or warnings from the scene.

## Verification record

Unity `6000.6.7f1`, run headless on Linux (AMD GPU for rendering).

- Compile and EditMode tests: 226 of 226 pass, 8 new for `CreepMotion` (rest at both ends, never backwards or outside the
  approach, still between lurches, a lurch ends on a whole share, footstep count and timing, invalid shapes).
- Setup: the Editor generated every `.meta`. A dependency scan found no map asset in the prefab or the preview scene; the shared
  assets are the enemy model, material and animator, the intro Boki prefab with its model, atlas and face materials and
  `intro_eye_pop.wav` (the preview scene also uses the intro mesh `LowPolyCone` and the player test prefab).
- Preview checks (`ARENA_INTRO_PREVIEW_CHECKS_PASSED`) and Pohorje checks (`ARENA_INTRO_POHORJE_CHECKS_PASSED`, run on the
  integrated scene on disk): nothing plays before the arena and waiting at its edge does not start it; riding in starts it; it
  holds the pause at time scale 0 with the controls off; no enemy is placed meanwhile; the faces and the creeper are in frame
  in every captured shot; the creeper's feet move (0.61 m of travel); it finishes by itself; afterwards gameplay, controls,
  rider, Boki's heading and the camera (0.00 m off) are back; the arena floods on the first poll after it (0.1 s); 49 of 49 positions
  across route A's arena find a lane; replay and skipping through the API and with Enter reach the same restored state; Enter
  after the end does nothing; no console errors.
- The existing whole-run harness (**Boki > Version 1 > Run Integration Checks**) passes before and after the integration
  (`V1_INTEGRATION_PASSED`). It now skips this scene like it skips the Bellevue intro.
- Frames: 24 per set, Play Mode renders with the real scene lights, reviewed in the preview and in the real arena. They found
  the empty face shots (the close-up Boki was never placed), a camera that sat too close behind Boki, a flash that hid the
  lunge and a `!` that slid under the letterbox; all fixed.
- Architecture audit: no failures; the one new warning is `ArenaIntro_Preview` missing from the build list, as for the other
  previews.

Not verified: how the sounds actually sound or how loud they are (the clips were checked numerically), interactive mouse and
keyboard play and a ride-in at full speed on the real camera, route B and C arenas (only route A's was probed for lanes),
anything on Windows or the 5600G, and the scene's frame cost in a player build. The frames show the scene with the gameplay
canvases hidden, as it plays, so they do not show the darkness overlay or the VHS shader.

## Observations outside this change

- `kaj_je_to_blo` is also the Guilt-warning reaction, so on Pohorje the player can hear it again at the first Guilt warning
  (after the 20 s ambient grace). If that repeats too soon, one option is to spend `PlayerAudio`'s once-per-scene reaction when
  this scene plays; not done, since it is another feature's behaviour.

## Deferred

Playing the scare only once per install, authored animation clips in place of pose code, a creeper approach that crosses the
frame at an angle so its stride reads side-on (from the front the swing of its feet and hands is hard to see), and a Windows
player check.
