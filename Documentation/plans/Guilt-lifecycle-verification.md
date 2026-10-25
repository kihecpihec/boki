# Guilt Lifecycle and Spawn Handoff

Scope: ambient Guilt lifecycle and safe placement, using [Gameplay §5](../Gameplay.md#5-loop-within-each-route). No production scenes or prefabs are edited. Movement bonuses, battery integration, replenishment, and boss route modifiers are separate work.

## Implementation

`RunState.Guilt` owns per-level pressure and phase progress in `Boki.Enemies.Rules`. Accepted level advances and New Run reset it. `RunSession` sets loading suspension and ends ambient pursuit on accepted boss entry. Standalone scenes can use the same rules without creating a session.

Combat-space occupancy is keyed by volume ID, so overlapping volumes and repeated notifications cannot multiply pressure or stop it prematurely. Gameplay, loading, and scripted suspension are independent. Hidden Guilt has no active CharacterController. Disabled/re-enabled adapters retain rule progress and contact cooldown.

Placement accepts only a sufficiently distant marker behind Boki, with valid ground and capsule clearance, outside the main camera frustum. Missing markers, ground, camera, or clearance never trigger a fallback teleport. Retries while awaiting placement use a 0.25-second gameplay timer; lingering pressure continues, and successful placement starts a full warning with a fresh tier snapshot. Stuck/distant repositioning retains chase progress and waits hidden for a valid marker.

## Inspector and Scene Setup

Scene ownership authorization is required before applying these steps to production assets.

1. On `GuiltChaser`, assign `Target`, `Visual Root` (a child), and `Spawn Points` (2–3 authored markers). Tag the gameplay camera `MainCamera`. Select terrain in `Ground Mask`, and solid obstacles in `Obstruction Mask`. Ensure markers allow the entire controller capsule to fit. Keep the Guilt root upright, with a centered controller and unit scale.
2. Enable `Use Run Session` in a routed level. Leave it off in a standalone test scene. Use one active ambient chaser per level; it alone ticks the shared pressure state.
3. Add `GuiltCombatSpace` to each owned combat-space BoxCollider. Enable `Is Trigger`, assign `Guilt` and `Player`, and activate only the selected route's spaces. Unity must deliver player trigger callbacks; verify the CharacterController/collision layers interact with the volume. Trigger disable/exit releases its occupancy.
4. The shared pause/upgrade authority sets `RunSession.Instance.State.Guilt.GameplaySuspended`. Scripted sequence ownership sets `ScriptedSequenceActive` before starting and clears it after finishing. These switches suspend ambient Guilt only; they do not implement global pause or scripted AI. In standalone checks, use `guilt.Pressure` for the same switches.
5. Routed boss entry uses `RunSession.EnterBossArena()`. A standalone arena owner calls `guilt.Pressure.EndForBoss()`. Decorative boss shadow presentation remains separate.
6. `SetPressureActive(bool)` is a standalone test-space convenience. Production volumes use their own IDs; do not leave this extra occupancy enabled alongside production triggers.

## Verification

Validated with Unity `6000.6.7f1` on Linux:

- EditMode: **110/110 passed**, no failures or skipped tests. Results: `/tmp/boki-guilt-editmode.xml`; log: `/tmp/boki-guilt-editmode-final.log`.
- Play Mode smoke: **passed** (`ENEMY_SMOKE_PASSED`). Placement/exit/boss checks passed; the normal Guilt cycle reached Warning, Chase and Reposition, with four strikes. Existing melee, Watcher and professor attack checks also passed. Log: `/tmp/boki-guilt-smoke.log`.
- `audit.sh`: no failures; existing milestone-helper duplication warnings remain. `git diff --check`: passed.
- Unity imported `GuiltCombatSpace.cs` and generated its `.meta`; no import is pending.
- Production trigger wiring, interactive visual review, shared pause/scripted integration, and Windows/5600G performance were not verified.

The existing `Boki → Enemy Models → Run Smoke Checks` command uses in-memory fixtures and restores the prior scene setup. Its Guilt checks cover rejected positions, occupied capsule space, successful placement, exit suspension, boss termination, and the normal attack cycle. It does not establish production scene wiring, visual quality, or Windows performance.

Manual checks in an authorized scene:

- Enter a combat space, leave during Warning and Chase, then reenter: pressure and phase progress resume without restarting grace. Overlap two spaces and leave only one: pressure continues once per frame.
- Pause or select an upgrade during a warning/chase, then resume. Combine pause and scripted suspension; clearing one must not clear the other.
- Make every marker visible, too close, blocked, or unsupported: Guilt stays hidden without an invisible collider or damage. Restore a valid marker: the warning lasts its full duration before chasing.
- Force a stuck/distant chase: relocation remains offscreen and cannot bypass solid geometry when striking. Disable/re-enable the chaser and verify phase retention.
- Enter the boss twice: pursuit ends once and cannot resume from a stray space-enter event. Advance a level or start a new run: pressure, occupancy, pending placement, and phase start fresh.
- Check Console for new warnings/errors. Windows player performance on the 5600G remains unverified.
