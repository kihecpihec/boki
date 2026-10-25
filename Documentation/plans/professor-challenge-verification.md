# Professor challenge and health bar

The professor now automatically attaches a themed `ProfessorHealthBar` when enabled. It presents actual `EnemyHealth` state, a recent-damage trail and the title `THE PROFESSOR`. `V1BossArena` clears its former numeric-only label so the displays do not duplicate each other. The new contact and attack tuning is canonical in [Story §3](../Story.md#3-story-beats-and-script).

## Integration

No Inspector assignments or saved scene/prefab changes are required. Existing `ProfessorBoss` instances use the new serialized field defaults, including V1 FERI and the professor template in the owned `Player_Model_Test` scene. The bar is created once per boss instance and reused on component re-enable. It sits below the V1 route/objective text and does not intercept UI input.

Tuning is on `ProfessorBoss`: `Combat Tempo`, `Attack Damage Multiplier`, `Aim Lead Seconds`, `Contact Damage`, `Contact Interval`, and `Knockback Speed`. Existing attack-gap, sheet-interval, enrage, opening-delay, HP and route-modifier assignments are preserved. `ScooterMotion.ApplyKnockback` supplies horizontal momentum through the existing controller's collision and deceleration path.

## Automated verification

- Unity 6000.6.7f1 imported the new scripts and generated their metadata.
- All 171 EditMode tests passed, including contact cooldown/separation, pause, long-frame behavior, knockback, boost cancellation, wall response and reset. Existing boss scheduler tests cover three-sheet alternation, enrage and the unchanged 0.8-second slam warning. Results: `/tmp/boki-boss-editmode.xml`.
- **Boki → Enemy Models → Run Professor Challenge Checks** activates the actual professor station in `Player_Model_Test` and checks the runtime HUD, contact damage, knockback, immediate repeat-hit rejection, world-wall blocking, shared pause, half-health fill and boss/player death visibility. The check passed. It refuses Play Mode and dirty scenes, changes only the in-memory fixture, restores the previous scene setup and never saves scenes or prefabs. Batch entry point: `Boki.Editor.ProfessorChallengeChecks.Run`, without `-quit`.
- A camera-rendered 1280×720 HUD image was inspected at `/tmp/boki-professor-healthbar.png`; graphics-enabled checks regenerate it. This image verifies the boss panel presentation, not the complete overlay arrangement in V1 FERI.
- Asset audit: no failures; existing resource-loading, build-list and legacy tooling warnings remain. `git diff --check` passed.
- Unity's Editor search index emitted a `UnityEditor.Search.SearchDatabase.EnumerateAll` startup exception during the runtime checks. The professor checks passed; this exception originates in Editor search indexing, outside the changed code.

## Manual verification still required

1. Open the owned `Player_Model_Test` scene, enter Play Mode and visit station **8 Professor**. Enable the laser to fight. Confirm the bar appears only in the active professor encounter; returning to the hub clears it.
2. Ride into the professor. Expect a health loss and a short push away; hold toward him to check that contact repeats at the configured cooldown. Approach beside a wall to confirm the push collides normally. Pause while touching him and during a slam warning; health and attack timers must stop.
3. Dodge papers by changing direction. The slam locks its predicted position when the warning starts; its boundary must remain still and allow the full 0.8 seconds to react. At half HP, verify the deadline label and faster attacks. Check player death, boss death and restarting the sandbox for stale/duplicate bars or Console errors from the changed code.
4. In `V1_FERI`, verify the full HUD arrangement, entrance seal, B-route reinforcements, C-route `POWER OFF` reduction and victory flow. This scene belongs to integration/world work and was not edited.
5. Compare complete runs with zero upgrades, about three selections, and five/six selections. The intended result is a demanding rush fight, a fair prepared fight and a shorter but still dangerous heavily upgraded fight. Automated checks do not establish this balance or a win rate.
6. Windows x86_64 player testing and Ryzen 5600G performance validation remain pending; Linux Editor checks do not establish either.


## Paper and slam combos

The scheduler now cycles the combos documented in [Story §3](../Story.md#3-story-beats-and-script). `ProfessorAttackAudio` is automatically attached by the boss and generates two short clips once per instance; no Inspector assignments or scene/prefab edits are needed. The existing Professor station in `Player_Model_Test` receives the behavior automatically. Clips are released when the instance is destroyed, and playback pauses with gameplay.

Manual verification: open `Player_Model_Test`, visit **8 Professor**, leave the laser off, and observe a full cycle. Expect three aimed casts, two separately warned slams, two offset radial volleys, and three separately warned slams. Listen for a paper flutter per cast/volley and an impact per slam. Pause during a warning and during audio playback, resume, then return to the hub or reset the encounter. Check the HUD shows only `THE PROFESSOR` and health, with no duplicate audio or HUD after reset. Interactive sound/visual review and Windows 5600G performance checks remain pending.

Validation for this update: Unity 6000.6.7f1 generated the new audio script metadata and passed all **173 EditMode tests** (`/tmp/boki-professor-combos.xml`), including combo ordering, repeated warnings, pause and enrage coverage. The updated runtime check passed (`PROFESSOR_CHALLENGE_PASSED`, `/tmp/boki-professor-runtime.log`), verifying the simplified HUD, twelve-projectile radial volley and audio component wiring in the actual model-test encounter, alongside existing contact and visibility checks. The existing Unity Search startup exception appeared again. Repository audit and whitespace checks passed. These batch checks do not establish audible sound quality, interactive attack readability or Windows performance.
