# Kill streak feedback

The rule is defined in [Gameplay §5](../Gameplay.md#5-loop-within-each-route). `AutoLaser` reports lethal hits, `ComboTracker` passes those kills to the plain-C# `ComboCounter`, and `ComboView` presents the counter and milestones. Double Trouble reports each lethal target separately. Nonlethal hits do not add to the streak.

## Wiring

No scene or prefab edits, new assets, or Inspector assignments are required. The existing `AutoLaser` in `Assets/Scenes/Player_Model_Test.unity` adds the tracker and view at runtime, as do the lasers in the V1 scenes. Shared scene ownership remains unchanged.

`ComboView` exposes `Animated Effects`, `Cue Volume`, and an optional `Player Hud`. It locates the HUD once on creation if unassigned and honors `PlayerHud.ScreenEffectsEnabled`. Disabling animated effects retains readable text and audio but removes pop, rotation, random positioning, and cracking motion. Four short synthesized audio clips are created once per view, use one quiet, low-priority 2D audio source, and are released when the view is destroyed. No audio assets need importing. Gameplay pause freezes presentation and pauses its audio source.

## Manual Play Mode checks

Open the existing shared `Player_Model_Test` demonstration scene. Do not save scene changes while performing these checks. Test the same combat behavior in `V1_Pohorje` afterward.

1. Hurt an enemy without killing it: the streak stays unchanged. Kill two enemies without taking damage: the right-edge counter shows `2 KILL STREAK` and pops per kill.
2. Reach 5, 10, and 20 kills: observe `LOCKED IN`, `RAVE MODE`, and `UNSTOPPABLE`, with increasingly energetic short tones. Check that text stays clear of Boki, central attack warnings, and the top-centred boss bar at 1280×720 and the intended display aspect ratios.
3. Take damage: a visible streak flashes red, displays `STREAK BROKEN`, briefly cracks/slides, fades out, and plays a descending tone. The next kill starts from 1. Healing alone must not break a streak.
4. With Double Trouble, verify two lethal targets add two, two surviving hits add zero, and a surviving hit followed by a lethal hit on the same target adds one.
5. Pause during a milestone: text animation and its tone freeze and resume together. Test upgrade selection as well. Death, disabling the laser, and changing scenes reset the streak; no HUD or audio source duplicates survive scene transitions.
6. Turn off `PlayerHud.ScreenEffectsEnabled` before building a streak: the text remains, but milestone positions are fixed and pop/rotation/cracking motion are absent.
7. Confirm no new Console errors or warnings. Listen with normal combat audio to check that milestones do not mask Guilt, incoming attacks, or battery cues.

Visual/audio feel and Windows performance on the Ryzen 5600G require a manual player check; headless EditMode tests do not establish them. Arena spawn timing, Guilt tuning, and pickup placement are unchanged by this feedback feature.

## Automated validation

- Unity 6000.6.7f1 headless import, compile, and EditMode run: 217 passed, 0 failed, 0 skipped, including all 8 `ComboCounterTests` cases.
- Architecture audit: no failures; existing warnings remain, plus the once-at-creation optional HUD lookup in `ComboView`.
- `git diff --check`: passed.
- Manual Play Mode checks and Windows player/5600G performance checks: not performed in this task.
