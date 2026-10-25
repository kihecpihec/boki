# Gameplay mock update / local design handoff

Open [the gallery](index.html), [UI states](ui-states.html), or the [PNG renders](renders/). This is an output-only design update. It adds no Unity scripts, scenes, prefabs, or imported assets and makes no Figma changes.

## Authority and scope

The [Gameplay rules](../../../Documentation/Gameplay.md#5-loop-within-each-route), [branch isolation](../../../Documentation/Gameplay.md#4-game-state-and-saving-rules), and [upgrade rules](../../../Documentation/Story.md#5-upgrades-and-replay-motivation) remain canonical. The JSON records source-document hashes to identify the reviewed snapshot; it does not replace those documents. Recheck those sources before implementation if the hashes change.

The original PNG art remains a silhouette, material, and atmosphere reference. The new sheets supplement the earlier topology plans with combat-space boundaries and semantic markers. Every pocket extent, spawn/charge coordinate, and Guilt anchor is a **construction proposal**, not an approved balance change. The room insets use meters; the journey diagrams are topology only. Corridor widths in the original kit still describe the approaches, not the new widened combat pockets.

## Route spaces and assembly

Use the three scene sheets to place a traversal approach, a widened combat pocket, and an exit on every selected route. Preserve the canonical route matrix, run-start unlock snapshot, immediate fork selection, and next-run discoveries. No extra confirmation UI or new route connections are introduced.

- Pohorje A: clearing after spruce slalom; B: service clearing after jumps; C: widened rave room built from existing concrete modules. Retain the two stronger regular enemies and grate set piece. B1 is the guaranteed battery pickup; it is part of the three-charge allocation.
- Maribor A: street court after car slalom; B: riverside yard connected by the narrow bank lanes; C: service room inside Podzemni prehod. No water traversal. C's far-end reveal still forces a retreat to the side door before the shared exit; the combat-space exit is not permission to skip this authored event.
- FERI A: seminar room off the repeated corridor; B: open courtyard pocket with continuous ramp onward; C: widened service room with the two floor traps on the approach and optional POWER OFF afterward. Every branch continues to one shared landing and classroom.

All regular-enemy spawn markers are candidates. At runtime each must be offscreen, at least 8 m from Boki, and on collision-free valid ground. Wait if no point qualifies; never accumulate missed spawns or replace an enemy immediately on death. Desired counts/cadences are 4/4 s, 6/3 s, and 4/4 s; total caps remain 8/12/8 including authored groups and boss reinforcements. Replenish only while the chosen combat space is occupied during active gameplay. Pause and upgrade selection freeze timers. Stop on leaving, transition, arena entry, or terminal outcome.

Exits have no kill quota. Do not physically lock the player inside a pocket to force a wave clear. The dashed inset boundary denotes pressure/spawn occupancy, not a wall. Scene owners must test that the proposed spaces support scooter turns, boost, camera clearance, usable spawns, and the 7–12 minute run target.

## Battery props and placement

Each B marker is a stationary, contact-collected +25 charge with available, depleted, and return-cue states shown in the UI sheet. Initially charged; recharge after 20 active occupied-space seconds. Clamp to upgraded capacity and leave a charge available if the battery is full. Leaving/reentry retains remaining recharge time. Pause, upgrades, and inactive spaces do not advance recharge. Battery drain is 1.5/s during active gameplay, including the boss; charge and capacity carry between scenes.

Three locations per regular combat space are the canonical initial tuning. **Lent B4 is an explicitly recorded authoring proposal** for its greater recovery opportunities, pending playtesting; it is not an extra universal charge or a canonical rule change. All placements must stay reachable at zero battery, clear of traps and spawn hazards. Forest C's B1 guarantees recovery on that route.

The classroom has two equivalent charges at proposed X/Z (4,8) and (20,8), inside the arena seal and outside the central dodge floor. Keep furniture and collision out of their riding lanes. Both use the same collection/recharge rules. Regular kills grant XP and kill count, never battery or charge drops. Upgrade recovery remains the explicit exception.

## Guilt and authored events

G markers are three candidate anchors per selected pocket, not simultaneous monsters. Use only an offscreen point behind Boki with valid movement clearance. The same immortal Guilt family supplies ambient and authored appearances. It gains no new attack or death animation.

Use [Gameplay §5](../../../Documentation/Gameplay.md#initial-playtest-rules-pressure-and-timing) for grace, warning, chase, rest tiers, snapshots, caps, and contact cooldown. The gallery mirrors the approved initial tier table for review. Accumulate active combat-space time across spaces in the same level. Outside, suspend pursuit and retain clock/phase; next level/New Run resets both. Pause, upgrades, loading, and scripted events suspend ambient timing. Authored events retain their own timing and cannot overlap ambient pursuit. Boss entry ends pursuit entirely.

The ordinary panic movement advantage remains separate from the Panic Mode upgrade. PANIC READY requires owning Panic Mode and an active chase within 10 m; snapshot boost eligibility at activation. Do not show the ready label during a distant silhouette or warning alone. Scale the in-place chase animation with actual movement speed; do not use animation root motion to override escalation.

## Branch and arena seals

Each alternative needs an entrance blocker **and an opaque rear seal** at the merge. Only the selected branch's rear seal opens. Hide unchosen interiors while retaining blockers and seals. Unchosen enemies cannot move, attack, or become laser targets; their charges/spaces stay inactive. Opening a gate never removes unrelated collision.

Arena entry closes a solid entrance seal, suppresses all approach rendering, disables approach encounters/replenishment, and ends Guilt pursuit. The boss shadow is decorative. Keep both arena charges inside this commitment boundary. New Run restores original visibility through scene reload. Retain idempotent boss entry and next-run FERI C persistence.

## UI mock contract

[UI states](ui-states.html) are static visual examples. They show XP/selection progress, MAX LEVEL, PANIC READY, distinct RECOVERY cards, fixed battery states, Guilt warning/chase presentation, and the open-exit choice. They do not implement these systems.

Six cumulative XP thresholds and the complete six-upgrade pool are listed in the expandable reference. Every offer contains three distinct eligible options; resolve exactly one before gameplay resumes. Cap/eligibility filtering, base-percentage stacking, charge-percentage preservation, and sequential paused choices remain in Story §5. The displayed offer is one example, not a restriction to three upgrade types.

Keep effects short and bounded. Guilt warnings, attack telegraphs, and low-battery cues take priority over ordinary impacts. Make VHS effects switchable and retain state readability on Low settings; color alone must not distinguish charge states.

## Integration checklist / pending Editor work

- [ ] Agree scene ownership before assembly. Import new assets through Unity 6000.6.7f1; let Unity generate their metadata.
- [ ] Assign combat-space occupancy volumes, selected-route ownership, replenishment candidates, charge objects, and Guilt anchors.
- [ ] Assign front/rear blockers, visibility roots, arena entrance seal, and encounter deactivation bindings.
- [ ] Verify all routes, City C's mandatory side escape, Forest C's guaranteed pickup, and Lent's proposed extra recovery.
- [ ] Validate spawn distance/visibility/caps, pressure retention/reset, and authored/ambient exclusion.
- [ ] Validate charged/depleted/return cues, full-battery behavior, pause/reentry timers, and both boss charges at zero battery.
- [ ] Validate six selections, three eligible offers, MAX LEVEL, RECOVERY, and Panic Mode snapshots.
- [ ] Preserve the two boss attacks, 4000 base HP, B's two melee once at half HP, and C's 3400 starting HP only after POWER OFF.
- [ ] Playtest pacing and measure a Windows 1280 × 720 Low player build on the Ryzen 5600G. Static diagrams establish neither collision correctness nor performance.

## Rebuild

Run `python3 Source/output/gameplay-mocks-v2/build.py` for HTML/SVG/JSON. To also rebuild PNGs, run `uv run --with cairosvg python Source/output/gameplay-mocks-v2/build.py --render` (the checked-in PNGs are rasterizations of these editable vectors). Use the full sibling packs together so their cross-links resolve.
