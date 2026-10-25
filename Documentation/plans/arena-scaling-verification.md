# Arena scaling verification

Tuning is canonical in [Gameplay §5](../Gameplay.md#enemy-replenishment-and-exits) and [Story §5](../Story.md#5-upgrades-and-replay-motivation). Scene edits were explicitly authorized for this task; no shared prefabs changed.

## Wiring

V1_Pohorje, V1_Maribor and V1_FERI retain their existing RouteEncounter references (Space, Player, View Camera, Spawn Points and Templates). Initial Count is 15; Cadence remains 4/3/4 seconds. Desired Count and Maximum Count are removed. AutoLaser Damage is 10 in those scenes and Player_Model_Test. No Inspector assignments are pending.

Player_Model_Test retains its isolated enemy stations; it demonstrates the reduced laser damage and shared XP thresholds. Arena waves use the existing combat spaces in the V1 scenes.

## Manual checks still required

1. Open V1_Pohorje, choose a route and enter its combat space. Expect 15 opening enemies, spread across safe positions. If positions are blocked, the remaining opening enemies wait for valid ground. Watch for overlap, inaccessible spawns and Console errors.
2. Avoid killing enemies for two minutes. Population must exceed 15 and keep increasing; cadence falls from 4 to 2 seconds (Maribor: 3 to 1.5). New enemies have 1.5 times base health and 1.3 times base damage at two combat minutes; existing enemies retain their original stats. Verify both melee hits and ranged shots.
3. Leave and reenter: no second opening group and no reset of scaling. Pause or open an upgrade offer: timers and spawning freeze. Check scripted City C sequence, next-level clock reset, boss entry stopping replenishment, and death/New Run clearing progression and restoring the opening group.
4. Collect death XP: one XP per pack, first offer at 8, then cumulative thresholds 18/30/45/65/90. Six selections still end progression. In Player_Model_Test, verify the laser deals 10 before upgrades and that upgrade visuals still follow selections.
5. Run a Windows x86_64 player on the Ryzen 5600G at 1280×720 Low, including an extended stay with many surviving enemies. Uncapped populations have not been performance validated.

No manual Play Mode or Windows performance verification was performed by the coding agent.

## Automated checks

Unity 6000.6.7f1 compiled and ran all 178 EditMode tests: 178 passed, none failed or skipped. Coverage includes cumulative XP boundaries, queued upgrade selections, run-state carry/reset, cadence and regular-enemy strength growth beyond two minutes. Repository audit passed with existing warnings; `git diff --check` passed.
