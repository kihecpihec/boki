# Player enemy-testing arena

Open `Assets/Scenes/Player_Model_Test.unity` alone in Unity **6000.6.7f1** and press Play. The existing rigged Boki, scooter, movement tuning, laser, headlight, damage/boost feedback and HUD are retained. The player starts in the new safe hub. The original movement arena remains intact and is reachable through **Original movement area**.

## Controls and stations

Use WASD, mouse look, Space to jump and Left Shift to boost. Escape releases the cursor for the right-hand test panel; click the world to capture it again. The panel selects a station, returns to the safe hub, resets the current enemy, toggles automatic laser fire or restarts the entire sandbox. Death stops riding and releases the cursor; **Restart sandbox** restores 100 HP, initial battery, zero XP/kills and the safe hub.

Each walled station activates only while Boki is inside it. Selection buttons place Boki inside; riding through the entrance also works. Leaving or switching removes the previous enemy and its live projectiles, so attacks cannot follow into another test. Only one enemy is active at a time. Resetting the enemy does not heal Boki, erase XP or refill depleted charges; restart the sandbox for a fresh state.

1. **Forest shadow:** existing forest melee model and pursuit/contact AI.
2. **Izgubljeni meščan:** existing city melee variant.
3. **Student shadow:** existing FERI melee variant.
4. **Forest Watcher:** existing forest ranged model and red orb projectile.
5. **City Watcher:** existing city ranged variant.
6. **Floating exam paper:** FERI's documented ranged reskin, using the existing paper model/projectile and shared ranged AI/tuning. No new enemy type is introduced.
7. **Guilt:** existing immortal pursuer bound to shared run pressure. Wait through the canonical 20-active-second grace period and turn the camera to allow a safe offscreen spawn. Its full warning precedes pursuit. Leaving resets pressure for independent sandbox trials; this is a test control, not production reentry behavior.
8. **Professor:** existing boss model, Exam Barrage, Deadline Slam boundary/countdown/impact and paper trails. Guilt is inactive here. Route-specific boss modifiers are not simulated.

Switch **Laser: OFF** to observe enemy attacks without killing the low-HP Watchers immediately. Switch it on to test targeting, hits, death effects and drops. Ranged stations have primitive cover. Enemy damage, speeds and attack timing retain existing tuning.

## Pickups

Each station contains three contact-collected battery charges, using the existing +25 recovery, full-battery rejection, depleted visuals and occupied-space recharge timers from [Gameplay §5](../Gameplay.md#battery-charges-and-action-feedback). **B** drains 25 battery for testing. Battery pickups use the existing green glow. The original arena's three charges remain, for 27 total.

Each station starts with two gold XP sample packs. Killing a regular enemy increments the run kill counter and leaves one more pack; the professor and invulnerable Guilt leave none. Contact calls `RunSession.State.Progression.AddXp()`, updates the existing HUD and produces its gold glow. Paused/dead players cannot collect. The six-selection maximum disables XP collection and further drop generation. Kills do not recharge battery.

`XpPickup` is the shared collector for both preplaced gold samples and regular-enemy death rewards. `EnemyHealth` is the sole kill/drop owner; stations only manage enemy lifecycle. Crossing a threshold pauses the arena and opens the real upgrade mockup. Offers, caps, effects and queued choices follow [Story §5](../Story.md#5-upgrades-and-replay-motivation), including the approved two-card exception. The obsolete `ArenaXpPickup` component has been removed.

## Wiring and regeneration

**Boki → Player Model → Build Enemy Testing Arena** refreshes the generated `Enemy Testing Arena` hierarchy in the existing `Player_Model_Test` scene. It refuses Play Mode/dirty scenes, restores the previous scene setup and never creates a replacement for a missing player scene. Repeat runs replace only the generated hierarchy and refresh player charge subscriptions, spawn and panel references. Custom edits inside that hierarchy are replaced on regeneration. The original model/arena, `Enemy_Test`, shared prefabs, production scenes, build settings and packages are not rewritten.

The builder reads `Enemy_Test` additively to copy its configured enemy models, animations, attack views and projectile templates. Scene-local target references are rebound to the existing player, and Guilt gets three station-local spawn markers. Templates stay beneath an inactive root and do not register targets. `PlayerTestArena` owns selection and test reset commands; `EnemyTestStation` only spawns/removes the selected copy. `ScooterController.BindGuilt` and `PlayerHud.BindGuilt` follow that same active clone, and both clear on return to the hub. No separate upgrade-test Guilt advances the shared clock. The builder also reapplies upgrade wiring: one EventSystem, one screen/manager, and the upgrade canvas above the arena controls. Station switches, resets, travel and laser toggles reject requests while upgrade selection suspends gameplay. Restart sandbox remains the explicit full-reset action.

## Verification

Run **Boki → Player Model → Run Arena Checks** outside Play Mode with clean scenes. Batch entry point: `Boki.Editor.PlayerTestArenaChecks.Run` without `-quit`. Require `PLAYER_TEST_ARENA_PASSED`. It exercises all eight enemy spawns, references and target isolation, panel callbacks, regular-kill drops, contact XP, pause rejection, battery contact, enemy reset without charge refill, Watcher shots, projectile cleanup, professor barrage/slam events, Guilt shared state, player death and full scene restart.

Manual review: ride through a station entrance and back out; verify isolation, readable labels and pickup placement. Test each melee contact and jump evasion; Watcher cover/projectile collisions; professor slam radius, warning and boost escape; Guilt offscreen placement, full warning, chase, damage and escape-speed response. Verify repeated pickups, depletion/recharge, laser toggling, panel clicks after Escape, camera comfort, original movement area and no new Console errors. Confirm Low-settings readability. Linux headless checks do not establish rendered appearance or Windows x86_64/Ryzen 5600G performance.

## Recorded checks (2026-10-24)

- Unity 6000.6.7f1 imported/compiled the scripts and generated all new metadata. Repeated arena builds retained eight stations, 27 charges, one player and one test panel. No additional Inspector setup is pending.
- `PLAYER_TEST_ARENA_PASSED` in `/tmp/boki-test-arena-verified.log`: all eight spawns and reference checks, target isolation, 13 live panel callbacks, regular-kill pack generation, paused/accepted contact XP, +25 battery contact, enemy reset preserving depleted charges, actual melee contact damage, Watcher firing/projectile cleanup, professor barrage and slam, Guilt's full grace/placement/warning/chase and player warning bridge, death and restart.
- Rendered Linux captures at 1280×720 were reviewed for the safe hub, Boki, panel, station entrance, gold/green pickups and professor's warning boundary. The review prompted brighter sandbox ambient lighting, a lighter darkness overlay, an entrance-facing hub spawn and removal of XP labels that rendered through walls. Keyboard/mouse feel and physical UI clicks remain hands-on checks.
- Architecture audit: no failures; existing warnings remain. `git diff --check` passes. Among scenes/prefabs/settings/packages, only `Player_Model_Test.unity` changed. Deterministic rules were unchanged, so the EditMode suite was not rerun.
- Unity again logged its previously recorded internal `UnityEditor.Search.SearchDatabase` startup indexing exception. No arena runtime errors were logged. Windows x86_64/Ryzen 5600G performance and compatibility remain unverified.


## Combined upgrade validation

The arena now uses real progression and shares its active Guilt with movement and HUD. The earlier recorded sandbox-only behavior above is historical; current eligibility, effects and selection rules are in Story §5. See [functional upgrade reconciliation](functional-upgrades-verification.md#arena-reconciliation).

`PLAYER_TEST_ARENA_PASSED` in `/tmp/boki-combined-arena-gameplay.log` revalidated all eight stations, 27 battery charges, one kill/drop per regular death, no boss XP drop, shared pickup collection, sole live Guilt driver, movement binding and cleanup, death/restart. `UPGRADE_GAMEPLAY_PASSED` in `/tmp/boki-combined-upgrades-gameplay.log` covers the combined screen and pause flow. The scene has one EventSystem, one HUD and one upgrade manager. Interactive and Windows checks remain open.
