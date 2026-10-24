# Recorded SFX integration

## Scope

Prepared against `dev` at `64aa3c1`, including the current enemy replenishment
cadence, professor patrol, wooden FERI arena, current boss health HUD/contact
challenge and the latest intro dialogue. Updates the existing rigged
player variant, `Player_Model_Test`, `V1_Pohorje`, `V1_Maribor` and `V1_FERI`.
The setup adds audio bindings and objects in place; it does not rebuild maps.
Existing scene object IDs, geometry, light/camera settings and non-audio prefab
overrides are preserved. Other scenes and enemy prefabs are unchanged.

## Clip mapping

- Scooter motor: the recorded scooter voice, trimmed with short edge fades.
- Player damage: three variations; laser: nine variations.
- Guilt: two growls alongside the existing warning heartbeat. The first visible
  warning presentation per player activation plays `kaj_je_to_blo` before growls.
- Health recovery: the recorded snack voice on an accepted health increase.
- Regular enemies: ten hit and four death variations, with a single greeting
  on the first eligible enemy activation in each scene. Numbering/category
  suggestions follow the source manifest and the approved short/long grouping.
- Maribor/FERI arrivals: two teleport variations.
- Intro: `fak_ne_spet` plays during the existing spoken-line shot.
- Actual FERI boss entry: `professor_question`, then `skoraj_pravocasno`.
  Shared `ScriptedSequence` pause holds combat during these two lines and releases
  only the pause acquired by this presentation.
- Victory: `professor_saturday`, then one of the two Boki reaction takes. The
  existing victory panel and terminal pause remain in effect.

Generated jump, boost, landing, player death, battery and XP cues, heartbeat,
menu sounds and music remain in use. No new boss attack recordings are invented.

## Import and Inspector wiring

All 40 runtime assets in `Assets/Audio/Sfx/Recorded/` are mono 24 kHz, 16-bit WAVs
imported as PCM with decompression on load. Their decoded sample data totals
2,585,280 bytes. Source MP3/QT recordings in the root `sfx/` folder are unchanged.
The imported Guilt group receives +11 dB for audibility; other imported groups
have conservative attenuation to leave headroom. No new audio packages are used.

Run **Boki → Audio → Apply Recorded Sounds** in Unity **6000.6.7f1**, outside
Play Mode with clean scenes. It restores the previously open scenes afterward.
Running it again reuses the existing components and seven output sources.

`PlayerAudio.damageVariations`, `shotVariations`, `guiltVariations`,
`guiltReaction`, `snack` and `motor` are assigned on the player variant and the
four scenes. Each scene's `Recorded Game Audio` object has `RecordedGameAudio`
with the player, optional V1 flow/route/status references, six `enemySources`,
one `dialogueSource` and the recorded clip groups. Enemy voices are 3D with no
Doppler; player and dialogue cues are 2D. Excess hit voices are skipped, while
deaths may replace an existing voice, keeping the six-source enemy limit.

Every scene-local regular enemy, professor and inactive spawn/reinforcement
template has `EnemyAudio.output` assigned to its scene output. Professor bindings
suppress the regular death groan. `V1BossArena.recordedAudio` points at that
output; existing `IntroCutscene.bokiLine` references the intro recording.
No manual Inspector assignments remain after applying setup.

## Checks performed

- Unity 6000.6.7f1 on macOS imported/compiled the clips and scripts and generated
  all new `.meta` files. Existing GUIDs are preserved.
- Setup ran twice and retained one output, six enemy sources, one dialogue
  source, one player audio component and one binding per enemy.
- **Run Recorded Audio Checks** passed static bindings in all four scenes and
  live V1_FERI checks: instantiated template damage/death, death tail after
  enemy destruction, shared menu pause/resume, fixed voice-source count under
  repeated requests, actual arena entry, question/reply sequencing, paused
  speech, combat resumption, actual victory/reaction, terminal suppression and
  disable cleanup without releasing the terminal pause.
- **Run Player Audio Checks** passed actual damage/laser playback, XP pickup,
  shared pause/resume, death and disable cleanup in `Player_Model_Test`.
- All **171 EditMode tests passed**, with zero skipped or failed, including the
  latest encounter-cadence, boss-contact and knockback tests. No deterministic gameplay rules were changed.
- Media checks passed for all 40 imported WAVs: expected format, nonempty data,
  peaks below full scale and paired Unity metadata. Original source hashes match.
- The architecture audit has no failures. Unity logged its previously recorded
  internal `UnityEditor.Search.SearchDatabase` startup indexing exception; the
  runtime checks reported no new gameplay/audio exceptions.

These automated checks inspect live playback state and references. They do not
establish audible mix quality or Windows performance.

## Manual listening and platform checks

1. Open `Player_Model_Test`, capture Game view, ride/stop, jump, land and boost.
   Use H for damage, T for a laser target, B for battery warnings and G for the
   first-warning reaction/growls. Collect XP and batteries; select health recovery
   while injured. Check that the motor and rapid laser shots leave warnings clear.
2. In V1_Pohorje, listen to the intro naturally and skip/retry it. Confirm skipped
   dialogue stops and no voices or callbacks duplicate on replay.
3. Fight repeated enemies on each chosen branch. Hit/death cues should vary;
   deaths should finish after enemies disappear. Pause and change application
   focus during a voice, then resume. Inactive branches must stay silent.
4. Travel into Maribor and FERI. Listen for arrival cues. Enter the current FERI
   arena: both opening lines finish before the professor resumes moving/firing.
   Defeat him and hear the Saturday line followed by Boki's reaction. Retry and
   confirm the next run has fresh audio state.
5. Listen and measure a Windows x86_64 player at 1280×720 Low on the Ryzen 5600G.
   Subjective balance, motor loop quality and target-platform performance remain
   pending until checked there.
