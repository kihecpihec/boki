# M6 Handoff and Verification

> **Historical scope:** this records the original fixed-encounter M6 implementation and its checks. The approved revised loop in [Gameplay §5](../Gameplay.md#5-loop-within-each-route) and upgrades in [Story §5](../Story.md#5-upgrades-and-replay-motivation) supersede its kill-based battery rewards, fixed ambient chase settings, and XP deferral. Those changes are specified, not implemented or verified by this handoff. Windows verification remains open.


Implemented on `feat/m6-full-run` following the [M6 plan](M6.md). The developer explicitly authorized edits to all three production scenes for M6. No commit, push, or merge is authorized or performed.

## Delivered Behavior

The existing run now includes light-battery pressure, ranged enemies, Guilt pursuit, FERI C hazards and `POWER OFF`, and one professor fight in the shared classroom. All three production scenes retain their existing routes, seals, prefab references, and GUIDs. Earlier test scenes and existing shared prefabs are unchanged.

`RunState.Combat` holds deterministic exploration/boss/won/lost transitions, panel eligibility, the half-health phase, and panic timing. Battery is fractional run state and carries with HP across both scene transitions. Death or victory accepts one terminal outcome. Threats and rewards are also suppressed while a scene transition is loading. New Run/Retry resets temporary state, panel use, panic, health, and battery, while M5 still reads permanent flags only into the new run's snapshot.

Battery starts at 100, drains at 1.5/s, receives +10 per regular kill and +25 per battery pickup, and clamps at 100. A regular death increments kills once. At zero, melee/Guilt movement gains 30%; damage and the laser remain unchanged. Watchers stand and shoot. Recharge restores ordinary movement and the unshadowed scooter light. Pickups, enemies, traps, and Guilt belong to selected-only encounter roots. Forest C has two stronger Watchers and a guaranteed pickup; Lent has extra ranged pressure and battery recovery.

The shared paper projectile travels at 5 m/s, deals 10 damage, and expires after 5 seconds. Watchers fire within 18 m every 2.5 seconds after an initial 1-second delay, with environment line-of-sight checks. Swept collision and initial overlap checks consume a projectile on its first wall/player hit. The existing player and enemies share the Ignore Raycast layer; filtering explicitly includes the player and excludes other enemy bodies.

Guilt uses three lane anchors and `Distant → Warning → Chase → Reposition`. It chooses a valid offscreen anchor behind the player within 5–25 m, warns for 1.5 seconds, and moves at 4.5 m/s. Ordinary chase duration is 5 seconds in Forest, 8 in City, and 4 in FERI. City C instead uses a one-shot appearance ahead, a 12-second chase at 5.4 m/s, and a side-door retreat. Contact deals 10 damage at most once per second, with wall occlusion. Nearby Guilt grants +25% player speed for 2 seconds with a 5-second cooldown, multiplying the approved existing movement/boost. Warning text and light flicker provide the minimal cue; no new audio assets are included. Guilt cannot be targeted or killed. Its controller collides only during active chase, so an invisible distant/warning Guilt cannot block a lane. Movement accepts small frame steps and stuck detection measures progress over a time interval. Its AI stops during the professor fight; a separate decorative silhouette remains in the classroom.

FERI C has two red floor warnings. Each has a 3-second cycle, warns for 0.8 seconds, and applies one 10-damage hit during the following 0.4 seconds if the player remains in its area. The panel accepts E within 3 m with clear access, only on selected FERI C before arena entry. Successful use is idempotent and shows confirmation.

Classroom entry retains M5's immediate FERI unlock save, starts the boss once, clears approach projectiles/enemies, ends panic, and closes the arena behind the player. Professor tuning is 4000 maximum HP; used FERI C panel starts it at 85% of maximum, otherwise full HP. FERI B activates exactly two melee reinforcements at the first crossing of half maximum HP. The active regular population remains below the eight-enemy FERI cap.

The professor alternates three-paper Exam Barrage with Deadline Slam. Slam locks a red circle to one ground position for 0.8 seconds, then applies one 20-damage hit within 2.5 m; moving away avoids it. Attack recovery is 2 seconds, reduced to 1.5 after half HP. Death/victory cancels remaining damage. Victory releases the cursor and shows the preserved ending lines, routes, elapsed time, kills, saved C discoveries, and NEW RUN. Existing ABSENT/Retry handles loss.

## Files and Editor Setup

- `Game/Assets/Scripts/Routes/Rules/M6State.cs`: deterministic battery, panic, panel, arena, phase, and outcome rules.
- `RunState.cs` / `Core/RunSession.cs`: fractional carried battery, combat reset, death binding, validated arena startup.
- `Core/M6Gameplay.cs`: scene battery/light HUD, regular kill rewards, terminal shutdown, victory view/New Run.
- `Enemies/RangedEnemy.cs`, `EnemyProjectile.cs`, `GuiltChaser.cs`: shared ranged combat and immortal pursuit.
- `Core/PowerPanel.cs`, `BatteryPickup.cs`, `FloorHazard.cs`, `ProfessorBoss.cs`: interactions, hazards, and fight.
- Existing health, melee, laser, scooter, and classroom adapters: small integration changes; standalone defaults remain supported.
- `Assets/Scenes/Level1_Pohorje.unity`, `Level2_Maribor.unity`, `Level3_FERI.unity`: authorized M6 wiring.
- `Assets/Art/M6`: simple shared URP materials, generated and imported in the pinned Editor.
- `Assets/Editor/M6SceneBuilder.cs`, `M6SmokeChecks.cs`, `M6PlayerBuild.cs`: safe scene upgrade, isolated integration checks, explicit three-scene build.
- `Assets/Tests/EditMode/Routes/M6StateTests.cs`: deterministic rule coverage. M5 smoke checks now count both regular enemy types and suppress new autonomous threats during route probes.

All Inspector references are saved. No manual assignments, input-asset edits, or shared build-setting changes are needed. Each scene's M6 Gameplay adapter references its player, headlight, battery text, victory panel/result, and New Run button. FERI additionally binds the professor, HP text, slam marker, entrance seal, reinforcement root, panel prompt, and hazards. Each ranged enemy references the player; each Guilt instance references its player, mesh, and lane anchors.

After updating an open Editor, exit Play Mode and reopen `Level1_Pohorje` before testing so cached scene instances receive the new wiring. Use **6000.6.7f1**. The `Boki/Apply M6 Combat and Boss` helper refuses Play Mode/dirty scenes, skips scenes already containing M6, and restores the previous scene setup. Do not regenerate production content with the old scene builders.

Build with `Boki/Build M6 Linux Test Player` or `Boki/Build M6 Windows Test Player`. Batch output uses `BOKI_M6_BUILD_DIRECTORY`; builds include all three production scenes explicitly.

## Validation

Validation runs in `/tmp/boki-m4-project` with product name `Boki-M5-Validation`, isolating PlayerPrefs from normal play. M6 smoke checks seed only that validation profile's three unlock flags. The separate M5 persistence write/read checks exercise a fresh profile across Editor processes. Never rename or clear the normal project's preferences to run these checks.

- All **47 EditMode tests passed**, including the existing health/routes/persistence cases and fifteen M6 cases covering fractional drain/recovery/invalid inputs, panel variants and guards, arena/terminal transitions, skipped half-HP crossings, panic duration/cooldown, reset, stale health isolation, and safe panel reduction across different boss maximum HP values, plus City C eligibility, idempotence, death rejection, and reset.
- M6 saved-scene smoke checks passed four histories: `A → A → A`, `A → B → B`, `C → C → C` with panel use, and `A → B → C` without it. Checks exercised real projectile/player and wall collisions, one regular reward per death, HP/battery carry, the guaranteed Forest C pickup, offscreen Guilt chase/panic and targeting exclusion, timed floor-trap damage, panel range/idempotence, real arena trigger contact, correct starting HP and B-only reinforcements, live barrage damage, fixed slam telegraph/avoidance, victory/New Run, terminal damage rejection, and ABSENT/Retry.
- M1 movement/collision/camera, M2 targeting/combat/three retries, and M3 physical gates/UI mouse handling/HP carry/three City death-and-Retry regressions passed.
- Updated M5 scene regression passed all five fresh A/B histories and three unlocked C histories, including physical CharacterController traversal, real gates/exits/arena triggers, saved flags, next-run snapshot timing, route-lock priority, rear seals, chosen-only enemies, singleton lifetime, and HP carry. Its disabled-before-activation Guilt setup also verifies that invisible controllers do not block the route.

- The real PlayerPrefs adapter passed separate-process write/read checks, preserving unrelated preferences and treating invalid values as locked.
- A second M6 scene-helper application passed without duplicating content or changing the delivered scene assets. Scene-local references, original scene GUIDs, unique asset GUIDs, complete metadata, documentation targets, and whitespace checks passed. Existing prefabs and earlier scenes remain unchanged.
- A Linux x86_64 development player built successfully at `/tmp/boki-m6-player/Boki-M6.x86_64`. Its eight-second headless startup logged no errors/exceptions and ended at the expected timeout (exit 124). This validates startup only, not rendered visuals, player input, a complete player run, or FPS. Build/startup logs: `/tmp/boki-m6-gaps-build-final.log` and `/tmp/boki-m6-gaps-player-startup-final.log`.

Storage/helper logs: `/tmp/boki-m6-persistence-write.log`, `/tmp/boki-m6-persistence-read.log`, and `/tmp/boki-m6-repeat.log`.

Logs: `/tmp/boki-m6-gaps-editmode.log` and `.xml`, `/tmp/boki-m6-gaps-smoke.log` (reviewed-code pass), and `/tmp/boki-m6-regression-M1.log` through `M3.log` plus `M5.log`. Automated probes disable autonomous behavior where controlled damage/traversal is needed; these are not interactive full-run acceptance.

## Logic Review

The follow-up logic review reproduced and corrected four defects:

- Destroying 30 projectiles retained 30 materials because cleanup read `Renderer.material`, cloning the shared material while leaving the original allocated. Each projectile now owns and destroys its exact material reference; the same probe retains zero materials.
- Guilt checked one projected point before repositioning. A camera angle could exclude that point while part of the mesh remained visible. Anchor validation now tests the complete candidate renderer bounds against the camera frustum, using a reused plane array.
- A projectile initially overlapping both a thin wall and the player could apply damage before checking the wall. Initial overlaps and equal-distance swept contacts now prioritize environment obstruction.
- With a valid 1-HP Inspector setting, the panel's integer reduction produced zero starting HP. Positive maximum HP now always yields at least one starting HP, and boss death subscription precedes initial modifier damage. Current scene tuning is 4000/3400 HP.

Focused PlayMode regression checks reproduce material ownership, partial silhouette visibility, and thin-wall/player overlap. They pass alongside the expanded EditMode suite and the full M6 scene smoke suite. Logs: `/tmp/boki-m6-logic-before.log`, `/tmp/boki-m6-logic-partial.log`, `/tmp/boki-m6-logic-after.log`, `/tmp/boki-m6-review-editmode.xml`, and `/tmp/boki-m6-gaps-smoke.log`. Run the focused probe through `Boki.Editor.M6SmokeChecks.RunLogicReview` in the isolated validation project.

## Branch Visibility, City C Escape, and Boss Duration

`RouteVisibility` explicitly maps each production branch's renderers. Before a choice, static interiors render normally. Selection sets `Renderer.forceRenderingOff` on unchosen interiors; existing route gates still control their opaque barriers, rear seals, and encounter activation. Inactive encounter renderers are assigned by branch ownership, so they cannot inherit another branch's visibility. The original shared floor collision remains intact; separate render-only floor sections allow each lane to disappear independently.

At validated arena entry, the existing entrance seal closes, all approach renderers are suppressed, approach encounters/lights are deactivated, and route/arrival labels are hidden. The arena floor, perimeter visuals, professor, silhouette, and B reinforcements remain available. Original colliders continue to enforce the arena perimeter and entrance seal. Retry/New Run recreates initial visibility.

City C now has a partitioned service gallery with a dead end. A real trigger starts Guilt's visible appearance ahead, closes the rear door, and opens a side door behind Boki. Retreat through that opening stops the chase and closes the side door behind the player. The eastern service lane reaches the existing shared exit; the production City C transition rejects attempts before the escape completes. Start/complete guards reject duplicates, wrong routes/levels, and dead players; new runs reset the event. The trigger accepts capsule contact at the edge of its volume. Escape detection covers the full side lane and playable tunnel height, so moving or boosting to the far wall still completes the event without closing the door through the player. Two Watchers remain, with a battery pickup in the service lane.

Boss HP is now 4000, or 3400 after POWER OFF, while laser tuning remains 20 damage every 0.4 seconds. The measured continuous-fire benchmark is 79.66 game seconds. Probes with real boss attacks and CharacterController movement won through all three entrances: A in 79.69 game seconds with 40 HP, B in 82.10 seconds with 30 HP, and C with POWER OFF in 67.67 seconds with 50 HP. These probes accelerate game time for validation and establish gameplay timing, not target FPS or final subjective balance.

New files: `Scripts/Routes/RouteVisibility.cs`, `CityCTurnback.cs`, `Editor/M6GapSceneBuilder.cs`, and `M6GapChecks.cs`. All assignments are saved in the three scenes. `Boki/Apply M6 Visibility, Turnback, and Boss Tuning` upgrades existing M6 scenes safely; the original M6 helper also invokes this upgrade. Scene GUIDs and existing shared prefabs are retained. The helper skips already-upgraded scenes and refuses Play Mode or dirty scenes.

The latest validation includes all 47 EditMode tests, real City C warning/chase/escape/door/exit/reset checks, branch visibility checks for all selected histories, approach culling and physical arena-retreat rejection, the full M5 route traversal regression including the new City C path, and the prior material/occlusion/offscreen regression. Logs: `/tmp/boki-m6-gaps-editmode.xml`, `/tmp/boki-m6-gaps-city-final.log`, `/tmp/boki-m6-gaps-smoke.log`, `/tmp/boki-m6-gaps-routes-final.log`, `/tmp/boki-m6-gaps-duration.log`, `/tmp/boki-m6-gaps-combat.log`, `/tmp/boki-m6-gaps-combat-b.log`, `/tmp/boki-m6-gaps-combat-c.log`, and `/tmp/boki-m6-gaps-review.log`. The dedicated and original M6 helpers also passed repeat application without scene changes (`/tmp/boki-m6-gaps-repeat.log` and `/tmp/boki-m6-gaps-base-repeat-final.log`).

## Manual Acceptance Remaining

Check normal input and camera traversal, E interaction/focus recovery, projectile/warning/HUD readability at 1280×720, battery depletion and recovery under real combat, trap avoidance, Guilt warning/reposition visibility, and gate/arena jump or boost bypass attempts. Automated movement and controlled damage do not replace interactive acceptance.

Boss timing now falls within the 60–100-second target in the measured probes. Check subjective difficulty and all modifiers interactively. The 7–12-minute full-run target remains unestablished by the short graybox levels. XP/upgrades, menu, cutscenes, extended audio, and PS1/VHS polish remain outside this delivery.

The installed Editor has Linux and WebGL support, but no Windows build module. Windows player build, Windows behavior, and FPS measurement on the Ryzen 5600G at 1280×720 Low remain pending. Linux headless results establish neither Windows compatibility nor target performance.
