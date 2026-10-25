# FERI ending: victory cutscene

Implements the victory beat of [Story.md §3 FINAL BOSS](../Story.md#final-boss--the-deadline-12-minutes) as a skippable
24.4 s sequence played in `V1_FERI` when the professor falls, then hands over to the existing results panel. It follows the
Bellevue intro's design ([intro verification](intro-preview-verification.md)): a self-contained prefab, a pure acting
function, and a separate cutscene-only model.

| Beat | Shot | Seconds | What plays |
|---|---|---:|---|
| Boss down | Loom | 2.6 | White flash. The professor looms at 1.7× size, arms spread, lit by a flickering red light. The red light goes out, he deflates with a squash and a wheeze to a tired man at his lectern. Boki braces, then sags. |
| Glasses | Glasses | 2.0 | Close on the professor. His right hand rises to the glasses and pushes them up. |
| Saturday | Saturday | 2.8 | Subtitle and the recorded line »Boki ... danes je sobota.« His jaw bobs with the recording's two phrases, with a pause for the ellipsis and a gesture on the last word. |
| Huh | Huh | 2.6 | Boki close-up: a blank stare, then the head tilts and he says »... A?« (one of his two recorded takes, picked at random). |
| Point | Point | 2.0 | He turns and points, wordless, at a wall calendar on the board. |
| Zoom in | ZoomIn | 2.4 | The camera zooms in until **31** of October 2026 fills the screen; a red ring is drawn round it with a low thump. Behind the full-screen card he becomes a cat. |
| Zoom out | ZoomOut | 1.8 | The camera pulls back and swings across the room to a professor with cat ears and whiskers, licking his forearm. |
| Meow | Meow | 2.2 | He stops licking, looks up and meows: a comic `MEOW!` burst, a camera punch, a flash and the meow. |
| Both | Both | 3.6 | Boki and the cat professor side by side; the professor keeps washing, Boki looks at him, then slowly at the camera, eyes out on stalks. Fade out. |
| Closing card | EndCard | 2.4 | Black, `YOU SURVIVED. YOU WERE NEVER LATE.` |

Enter skips. Playing to the end and skipping call the same finish: the scripted-sequence pause is released, the gameplay
canvases are restored and the results panel (`V1GameFlow.Victory`) appears. **31 October 2026 is a Saturday**
(`EndingDate`, covered by a test), which is what makes the professor's line true.

## Wiring

`V1BossArena` has an optional `ending` reference. When the professor dies it calls `EndingCutscene.Play(flow.Victory)`;
with no cutscene assigned it calls `Victory` at once and plays the recorded victory lines over the results panel, as before.
The cutscene plays those same recordings (`professor_saturday.wav`, `boki_reaction_01/02.wav`, the team's
`Audio/Sfx/Recorded/Dialogue` clips) at its own cues, so the arena does not play them a second time. A player who is
already down has the ABSENT panel, which `Victory` leaves alone, so no ending plays over it. `EndingCutscene` takes `PauseReason.ScriptedSequence`
from the shared `RunSession` pause authority, so combat, battery drain and Guilt pressure stay suspended; it plays on
unscaled time, disables the follow camera and scooter controls, hides the player's rider and lights and the gameplay
canvases, and leaves them hidden after the sequence because the run is over. A cutscene that cannot start (a missing
reference) logs an error and finishes immediately, so a run never stalls on it.

`Ending_Preview` is the isolated set (a primitive classroom, no map asset). `V1_FERI` holds an instance of the same prefab
at the professor's spawn, turned to face the classroom entry.

## The cat professor

`Professor_Ending.prefab` is a separate professor for the ending, so it can be bent and customised without touching the
boss. It is the enemy model's rig and mesh, with:

- the rig **snapped to its bind pose** by the builder. `CHR_Professor.fbx` ships in the hunched lectern idle pose, and the
  cutscene's arm and torso angles are offsets from the standing pose;
- separate 3D parts on the head bone: low-poly cat ears (outer and pink lining), six whiskers, a pink nose, an open-mouth
  box and a tongue that reaches whichever point of the forearm the lick is at.

`EndingProfessor` takes a `Pose` and writes transforms; it owns no timing. `EndingActing` is the whole script as a pure
function of (shot, seconds), so playing, skipping and the frame capture show identical frames. To change a gag, edit
`EndingActing` (timings, poses, cameras) with no prefab rebuild. The arm poses (lectern rest, glasses, pointing, licking)
were found by searching joint angles against the rig's real bone positions, not by eye. Boki is the existing
`Boki_Intro.prefab`, standing.

## Staging note: the V1 classroom's lectern

The map's lectern is a desk 2 m wide and 1.4 m high, which hides a standing professor below the shoulders. The professor
therefore looms, adjusts his glasses and speaks behind it, then stands beside it from the Boki reaction on, and the cut
hides the move. The calendar hangs on the `PRISOTNOST: 0/1` board to his right. A different lectern needs only
`ProfessorBeside` and `DefaultCalendar` in `EndingActing`, and the calendar's position in the scene.

## The prefab and tools

`Assets/Cutscenes/Ending/EndingCutscene.prefab` holds the stage, the actors, the calendar (laid out from `CalendarMonth`),
the stage lights, the overlay, the audio source and the `EndingCutscene` with everything already wired. Scene-side, the
host assigns `shotCamera`, `hiddenDuringEnding`, `controls` and `gameplayCanvases`. Sounds (`ending_deflate`, `ending_zoom`,
`ending_thump`, `ending_meow`, `ending_lick`) are synthesised by `python3 music/generate_ending_sfx.py`; the Boki eye pop
reuses `intro_eye_pop.wav`.

All commands refuse Play Mode and dirty scenes.

- **Boki > Ending > Rebuild Ending Preview**: regenerates the professor and cutscene prefabs, the materials, the clip import
  settings and `Ending_Preview`, keeping every asset path and GUID. It **refuses once another scene uses the prefab**,
  because Unity gives a rebuilt prefab new internal ids.
- **Boki > Ending > Open Ending Preview**.
- **Boki > Ending > Integrate FERI Ending**: upgrades `V1_FERI` in place (the cutscene instance and the arena's `ending`
  reference only) and saves it. It does not rebuild the map or change shared prefabs or build settings.
- **Boki > Ending > Capture Preview Frames** and **Capture FERI Frames**: render review frames of every beat to
  `output/ending-preview/<name>/` (or `$BOKI_ENDING_FRAMES`) without saving the scene. Run without `-nographics`.

## Manual verification

1. Open `Ending_Preview` and enter Play Mode. The ending starts on its own and runs about 24 s. Check each beat in the
   table: the red light going out with the deflating sound, the glasses, the subtitle's diacritics and »«, the point, the
   zoom with the thump, the reveal, the meow and the `MEOW!` burst, the two-shot, the closing card.
2. Press Enter mid-way: it jumps to the closing card. Press R: it replays. Enter after the end does nothing.
3. In `V1_FERI`, reach the classroom (any route), defeat the professor (or use any means of dealing 4000 damage) and watch
   the ending play in the real classroom. Check the camera against the walls and the desk, the calendar on the board and that
   the cat is fully visible. Enter must skip to the results panel. Retry must start a fresh run at Pohorje.
4. Escape must not open the pause menu during the ending. The Console shows no new errors or warnings from the ending.

## Verification record

Unity `6000.6.7f1`, run headless on Linux (AMD Radeon 780M for rendering).

- Compile and EditMode tests: 192 of 192 pass on the merged tree (166 before dev's tests were merged), including 14 new: `CalendarMonth` (the ending date is a Saturday, Monday
  weeks, leap February, a six-row month, invalid input) and `EndingMotion` (idle before the licking begins, periodic, bounded,
  the tongue sweeps wrist to elbow, invalid periods).
- Setup: the Editor generated every `.meta`. A dependency scan of `Ending_Preview` and both prefabs found no map, level,
  route or gameplay-scene asset; the only shared assets are the enemy professor model, material and atlas, the intro Boki
  prefab with its model, atlas and face materials, the player's scooter model and atlas, and `intro_eye_pop.wav`.
  `V1_FERI` gained 165 added lines (the cutscene instance, its proxy blocks, one scene-root entry and one reference) and
  nothing was removed: dev's scene is not in Unity's own serialisation, so re-saving it in the Editor rewrote about 9000
  lines; the additions were patched into dev's text instead (`Boki > Ending > Integrate FERI Ending` is still the way to
  wire a fresh scene).
- Frames: about 40 frames per scene were rendered and reviewed in the preview set and in the real classroom. They found the
  hunched rest pose, the mirrored calendar and the lectern that hid the professor; all three are fixed.
- Play Mode (headless, temporary driver that has been removed; keyboard input was injected with background and focus
  filtering turned off): the ending starts on its own held by `PauseReason.ScriptedSequence` at time scale 0, finishes by
  itself after the expected 24 s with the pause released, R replays, Enter skips to the end state and a second Enter does
  nothing; no console errors.
- Version 1 integration checks: on the branch as first written (before dev's later XP and enemy rebalancing was merged),
  `V1SceneChecks` played through all three levels, the boss ending (`Boss defeat plays the ending before the results panel`,
  then `Skipping the ending shows terminal victory screen`), Retry and the second run: `V1_INTEGRATION_PASSED`. The harness
  did not pass before this change: `V1_Pohorje` opens with the Bellevue intro, which holds gameplay, and the checks never
  skipped it. They now press Enter while Pohorje's intro is pending. On the merged tree dev's professor-only mode
  (`Boki > Version 1 > Run Professor Checks`) passes with the ending and covers both a living and a fallen player
  (`PROFESSOR_ARENA_CHECKS_PASSED`). The whole-run mode now stops earlier in Pohorje on expectations that predate dev's
  rebalancing (`active=15` where the check allows 4, then `Upgrade pauses gameplay`); that is not caused by the ending and
  was left for whoever owns those checks.
- Architecture audit: no failures; the one new warning is `Ending_Preview` missing from the build list, as for the other previews.

Not verified: how the sounds actually sound or how loud they are (the clips were checked numerically and by spectrogram),
interactive mouse and keyboard play, anything on Windows or the 5600G, and the ending's frame cost in a player build. The
frames are Edit Mode renders, so they do not show the player's lights or the VHS shader.

## Observations outside this change

- In `V1_FERI` the combat professor is placed with an identity rotation at the spawn, so it faces away from the classroom
  entry, and its own prefab lectern sits behind it, while the map's lectern collider is in front at z = 93. The ending's stage
  faces the entry and ignores this. Whoever owns the map or the boss prefab may want to rotate the boss.
- `V1_Pohorje` and `Intro_Preview` were not touched.

## Deferred

Authored animation clips in place of pose code, Guilt's silhouette rising behind the professor before he is beaten
(Story §3 boss intro), a meow pitch that follows the camera's punch, and a Windows player check.
