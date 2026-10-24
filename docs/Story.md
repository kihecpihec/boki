# BOKI: LATE TO FERI — Story

> **Status:** story specification for a 48-hour Unity Halloween Game Jam.  
> **Related:** [Gameplay.md](Gameplay.md) (mechanics, routes, balancing), [Architecture.md](Architecture.md) (Unity implementation).  
> **Genre:** 3D third-person PS1/VHS horror action roguelite.  
> **Successful run duration:** approximately 7–12 minutes (typically 8–12; fast attempts can be shorter).  
> **Premise:** Boki wakes after a rave near Bellevue on Pohorje, sees 07:58 on his watch, and tries to reach a lecture at FERI through an apocalyptic Maribor. A monster called Guilt pursues him: the physical embodiment of his guilty conscience.

## 1. Core Idea and Tone

The game is both funny and unsettling. Boki and his comments are absurd; the world around him is truly horrifying. The player must keep moving: Boki rides an electric scooter, his rave laser fires automatically, and the scooter light holds enemies back. The enormous, unkillable shadow Guilt follows in the background. Each chosen path leads to a combat space: Boki can linger to fight, collect XP, and gain upgrades, but Guilt appears more often, moves faster, and deals more damage the longer he stays. That pressure pushes him to continue toward FERI before the risk outweighs another upgrade. [Gameplay.md §5](Gameplay.md#5-loop-within-each-route) defines this loop and its open tuning decisions.

**Core premise:** choosing a route on Pohorje limits the available routes in Maribor; choosing a route in Maribor limits the entrances to FERI. Hidden paths unlock permanently across attempts, but a previous choice can still block them during a particular run.

**World interpretation:** the player never learns whether this is a real apocalypse, a nightmare, or the result of a sleepless night and university anxiety. Even the narrator leaves it unresolved; the ending overturns the situation.

## 2. Run Structure and Narrative Progression

One run follows **Bellevue → Pohorje → Maribor → FERI → professor**. The route system, starting resources, death/reset behavior, gate states, and unlock timing are defined in [Gameplay.md §1–4](Gameplay.md#1-main-gameplay-flowchart). All FERI routes converge on the same classroom and boss.

### 2.1 Route Connections

Use the single canonical [route matrix in Gameplay.md §3](Gameplay.md#3-allowed-route-matrix), including its first-run bottlenecks and demonstration guidance. Do not maintain a second matrix here. Gates must visibly distinguish a permanent lock from a lock caused by the previous route.

### 2.2 Permanent Unlocks: Narrative Cues

The mechanical milestones and next-run availability are specified in [Gameplay.md §1 and §4](Gameplay.md#4-game-state-and-saving-rules). These are their narrative explanations:

| Key | Narrative cue |
|---|---|
| `UNLOCK_FOREST_C` | Boki finds a wristband marked “AFTERPARTY →” in his pocket and remembers the passage |
| `UNLOCK_CITY_C` | An abandoned city sign reveals a plan of the underground passage |
| `UNLOCK_FERI_C` | Outside the classroom, Boki hears and remembers a service access code |

A short HUD message is enough for each discovery; extra animation is unnecessary. The discovery remains meaningful even if Boki dies later in the run.

### 2.3 Intended Duration

| Section | Time |
|---|---|
| Prologue, if not skipped | 20–30 s |
| Pohorje, chosen route | 2–3 min |
| Maribor, chosen route | 3–4 min |
| FERI approach to classroom | 1–2 min |
| Professor, boss arena | 1–2 min |

**Total:** approximately **7 min 20 s – 11 min 30 s**, summarized as **7–12 minutes** (typically 8–12). The boss is counted once. Skipping the prologue or choosing the fastest routes can shorten a run. This is a playtesting target, not a hard limit.

## 3. Story Beats and Script

### PROLOGUE — “The Morning After” (20–30 Seconds, Skippable)

**Location:** Bellevue in the early morning, overlooking Maribor. The camera is low in the grass. Distant techno bass slowly gives way to wind.

**Shot 1:** Boki lies on the ground beside a plastic bottle and scooter. The camera rises. Gray fog, dark city outlines, and something wrong with the sky's color fill the horizon.

**Shot 2:** Boki raises his left arm. His low-poly watch fills almost the entire screen: **07:58**. His eyes pop out of his head. A short phone beep: *»PREDAVANJE — 08:00 / FERI«*, and they pop out again, bigger.

**Boki:** »Fak ... ne spet.«

**Shot 3:** The view toward the city jumps into a VHS glitch for a second: overturned cars, a reddish sky, and an enormous black figure between the trees. On the next flicker, the figure vanishes.

**Boki:** »... kaj je to blo?«

**Shot 4:** Boki scrambles onto the scooter: he bolts upright, sprints the wrong way, skids, turns back and leaps on. Its large headlight and improvised laser projector from the previous rave turn on.

**HUD:** `OBJECTIVE: GET TO FERI` / `DON'T LOOK BACK`.

**Transition:** the camera moves behind the player. Controls unlock. Three direction signs mark the first fork; C is locked on the first run.

### LEVEL 1 — “POHORJE: WRONG WAY HOME” (2–3 Minutes)

**Shared section:** 10–15 seconds of safe riding to learn movement, with a short jump/boost prompt. The first dark silhouettes appear before the fork. The laser hits them automatically; they leave XP particles when killed; scattered battery charges reward riding through the combat space. Fog makes the trees feel too close. During the first encounter with Guilt, the bass slows into a heartbeat.

**Choice A: Gozdna strmina.** A narrow, winding route between tall spruces and logs. It has the most regular enemies and therefore the most early XP. Special event: at the end of a clearing, the monster appears directly behind the player, who gains a short panic speed advantage. Atmosphere: black trees, thick blue fog, a few orange lights. **Gameplay:** tree slalom and XP collection; simple to build using terrain and cylinders.

**Choice B: Gondolska servisna pot.** A wider path beneath the gondola cable, with overturned maintenance carts and jumps over broken planks. Fewer enemies and less XP make it faster, but movement errors are more punishing. The first Watcher appears as a red light on a gondola. **Gameplay:** movement challenge. No working gondola is required; poles and a hanging cabin silhouette are enough.

**Choice C: Rave tunel (Permanent Unlock).** A hidden concrete tunnel with old rave posters, flashing purple light, and an eerie musical echo. Inside are deeper darkness, two slightly stronger enemies, and **one guaranteed pickup** (battery or XP). The player first sees the monster through a grate; it then strikes a metal gate. **Gameplay:** a shorter route with greater light-related risk. Rework an existing corridor rather than building an entirely new map.

**Level ending:** a short transition to a view over the city. The watch still reads **07:58**, although Boki has been traveling for some time. The screen flashes `YOU REMEMBER A WAY BACK` on the first Pohorje C discovery. A loading transition leads to Maribor.

### LEVEL 2 — “MARIBOR: CITY OF ABSENCES” (3–4 Minutes)

**Shared section:** deserted streets with low-poly overturned cars, overgrown facades, leaves, and old student-event posters. A streetlight occasionally goes out. The player reaches a fork; open gates are filtered by the Pohorje choice and unlock snapshot. Locked routes remain visible to encourage replay.

**Choice A: Tabor – Zapuščene ulice.** The most action-heavy option: a long street, parked and overturned cars, and two larger enemy groups. A jump over a car ramp offers an escape. Guilt appears once on a roof and then behind the player. **Gameplay:** more XP and combat, street slalom. Enemies are presented as »mestni izgubljenci« but use the same melee AI as forest shadows.

**Choice B: Lent – Mrtvo nabrežje.** Two narrow paths beside the water, fog, sounds without visible people, and a long bridge silhouette. Floating Watchers firing simple projectiles dominate. A few lamps offer briefly safe-looking spots. **Gameplay:** more projectile dodging and less direct swarm combat. A flat surface with a water texture and bridge silhouette is sufficient; geographic accuracy is unnecessary.

**Choice C: Podzemni prehod (Permanent Unlock).** An opening under a fence leads into a city beneath the city: a short, narrow, dark service gallery. Doors close behind Boki; Guilt appears at the far end and forces him to sprint back to a side opening. **Gameplay:** fewer regular enemies, a more aggressive chase event, and greater danger from an empty battery. Build with repeated modular concrete segments.

**Level ending:** a banner reads `FERI →`; a distant school bell rings. The watch first changes from **07:58** to **07:59**, briefly showing **88:88**. Completing the city for the first time unlocks Podzemni prehod (Maribor C) for the next run.

### LEVEL 3 — “FERI: THE FINAL LECTURE” (1–2 Minutes to the Classroom, Excluding Boss)

**Shared section:** FERI's exterior is recognizable but stylized and deliberately inaccurate. Inside, space behaves unnaturally: repeating doors, wrong classroom numbers, fluorescent lights going out behind the player. Guilt appears one last time in the corridor and dissolves when Boki opens the classroom door. All variants use the same basic controls before the finale; no checkpoint is needed.

**Choice A: Glavni vhod.** A short corridor with two small groups of »študentskih senc«. A lecture is audible behind a door even though nobody is visible. **Boss modifier:** no special bonus or penalty.

**Choice B: Dvorišče / stranski vhod.** An open passage through an overgrown courtyard, then a short stairwell corridor. A small ambush occurs before the classroom. **Boss modifier:** two regular enemies spawn once when the professor reaches 50% HP.

**Choice C: Servisni vhod (Permanent Unlock).** A narrow technical corridor, electrical sparks, and two simple floor traps. At the end, Boki can activate an electrical panel's `POWER OFF` switch. **Boss modifier:** pressing it makes the professor start with 15% less HP; otherwise the fight starts normally. This rewards discovering the secret route without adding boss mechanics.

### FINAL BOSS — “THE DEADLINE” (1–2 Minutes)

**Shot:** Boki races into the classroom. The professor stands at the back of the classroom, facing the entrance. The board reads `PRISOTNOST: 0/1`.

**Professor (calmly):** »Boki. Koliko je ura?«

Boki checks his watch. The digits change: **07:59 → 08:00 → 88:88 → 08:00**.

**Boki:** »Saj sem skoraj pravočasno ...«

The professor's shadow stretches. An enormous Guilt figure rises behind him from the back wall, then merges with his shadow. Red light fills the classroom. The screen displays `FINAL BOSS: THE DEADLINE`.

**Boss rules:** one arena; the professor patrols inside the classroom perimeter, faces Boki during combat, and stops for pause or the end of combat. The lectern is removed to leave his approach clear. Cycle two attack types through four combos: three aimed papers, a double slam, two radial paper volleys, then a triple slam; repeat from the aimed barrage.

1. **Exam Barrage:** fires 3 slow aimed paper sheets, using the Watcher projectile system with another model. Aim each sheet toward Boki’s movement with 0.35 s of lead, capped at 3 m; projectiles never home. Its radial combo fires two horizontal 360° volleys of 12 papers at the sheet interval, with the second volley rotated 15° to shift the dodge gaps.
2. **Deadline Slam:** a red warning circle appears on the ground for 0.8 s, then the area deals damage. Place it 0.25 s ahead of Boki’s horizontal movement, capped at 2 m, and freeze its position for the entire warning. The player can change direction, jump or boost out. Double and triple slam combos leave 0.35 s recovery after each impact before placing a fresh warning at the player’s new predicted position; every warning lasts the full 0.8 s. Paper casts play a short flutter/whoosh and each slam impact plays a low thump with a sharp crack.

**Harder professor tuning:** retain 4000 HP so damage upgrades shorten exposure rather than inflating the fight. Divide the authored 2.5 s attack gap and 0.6 s sheet interval by a 1.6 combat-tempo multiplier (about 1.56 s and 0.375 s). Keep the 1.5 s opening delay. Apply a 1.5 damage multiplier to authored sheet/slam damage (12 → 18 and 25 → 38); the half-health attack-speed multiplier remains 1.25, and the slam warning always lasts 0.8 s. These are playtest starting values: a zero-upgrade victory should demand consistent dodging, while roughly three selections should noticeably reduce exposure. Difficulty never scales to the number of upgrades or repeat runs.

**Body contact:** during an active fight, touching the professor deals 20 damage immediately, then at most once per active second, and replaces horizontal scooter momentum with a 10 m/s push away from him. The push cancels an active boost, retains its cooldown and vertical motion, and uses normal scooter collision and deceleration; it never teleports Boki. Pause, pre-fight, death and victory cannot deal contact damage. Solid world geometry blocks contact hits.

**Boss HUD:** show a top-centred dark VHS panel with cream lettering, a segmented red health bar, numeric current/maximum HP, and an orange recent-damage trail. Show only the title `THE PROFESSOR` and current/maximum health; omit the clock, phase, subtitle and recording text. Display only during a live fight and hide on player/boss death or boss disable.

At approximately 50% HP, a short audio glitch plays and attacks become slightly faster. FERI B and C apply the modifiers described above. Guilt is only a visual silhouette here, **not** an independent second AI monster.

**Victory:** the red light goes out. The professor shrinks to normal size, adjusts his glasses, and says: »Boki ... danes je sobota.«

**Boki:** »... A?«

Cut to black. `YOU SURVIVED. YOU WERE NEVER LATE.` Then show chosen routes, time, kill count, collected upgrades, and unlocked secret routes. `NEW RUN` lets the player try a different route combination.

**Death anywhere:** the VHS screen cracks and shows `ABSENT` and `TRY AGAIN`. A new game starts at Bellevue with permanent unlocks preserved.

## 4. Enemy Design and Escalation

**Production rule:** only **two basic regular enemy types**, reskinned by level, plus one immortal pursuer and one boss.

| Type and AI | Pohorje | Maribor | FERI | Implementation |
|---|---|---|---|---|
| **Melee chaser** | Forest shadow | Izgubljeni meščan | Student shadow | Same AI script, different material/mesh, slightly more HP |
| **Ranged shooter** | Watcher, late in level | Red Watcher | Flying exam papers | Same projectile AI, different prefab/material |
| **Guilt** | First appearance and pursuit | Longer chase and rooftop appearance | Final corridor appearance | One immortal script with scripted spawn points |
| **Professor** | – | – | Final boss | Two telegraphed attacks in one arena |

**Suggested initial playtest values, subject to balancing:**

- Boki: 100 HP; speed 7 m/s; boost 11 m/s for 0.6 s with a 3 s cooldown; CharacterController jump.
- Auto-laser: nearest target within 12 m; fires every 0.5 s; base damage 10.
- Light battery: 100; drain 1.5/s; regular kills never restore charge; arena battery pickups restore +25, with placement and recharge rules in [Gameplay §5](Gameplay.md#battery-charges-and-action-feedback). At 0 battery, enemies gain +30% speed, with no extra damage boost.
- Melee: approximately 20–30 HP; ranged: 15–25 HP. Increase stats slightly by level, e.g. HP ×1.0 / ×1.25 / ×1.4, without new AI types.
- Guilt: invulnerable; audio warning and light flicker when nearby. Boki gains **+25% speed for 2 s**, with a 5 s cooldown. Guilt periodically disappears and later reappears at a newly selected point. Lingering increases its appearance frequency, chase speed, and damage under [Gameplay.md §5](Gameplay.md#5-loop-within-each-route); approved initial scaling values and caps are specified there. No instant-death mechanic.
- Professor: **4000 HP** in M6, with two attack types. This targets approximately 80 seconds of continuous fire with the approved [M2 combat baseline](Gameplay.md#m2-combat-baseline), before route modifiers. Verify the 60–100 s fight target through interactive playtesting.
- Active regular enemy cap: Pohorje 8; Maribor 12; FERI 8 plus boss. Spawn from a few predefined points only.

### Difficulty Curve

**Pohorje:** tutorial, melee, first Guilt threat.  
**Maribor:** greater overall pressure, ranged projectiles, more demanding battery management.  
**FERI:** fewer regular enemies, different hazards, boss with red warnings.

Within a run, staying longer in a combat space escalates Guilt pressure. Difficulty **does not automatically increase** merely because the player starts a subsequent run. Routes, hidden shortcuts, different encounters, and upgrade choices provide replay value. A separate Hard mode option is optional if time remains; it is outside the MVP.

## 5. Upgrades and Replay Motivation

Regular enemies drop one contact-collected XP pack each until maximum level. Use cumulative thresholds **3 → 7 → 12 → 18 → 25 → 33**: six upgrade selections maximum per run. XP and selections carry across scenes and reset on death/New Run. At six selections stop generating XP drops, disable collection of leftover XP, and show `MAX LEVEL`; kills still count but never recharge battery. Initial pacing target: roughly three selections in normal runs, five or six only through risky extended stays. Tune XP and Guilt pressure together after playtesting.

Crossing a threshold pauses gameplay and offers three distinct random eligible options, or two when only two eligible rewards remain. Select exactly one before resuming; if multiple thresholds are crossed, resolve choices sequentially without unpausing combat. Bonuses add percentages of the original base stat rather than multiplying previous upgrades.

| Upgrade | Exact effect | Selection cap per run |
|---|---|---:|
| **Rave Overdrive** | +30% original base laser damage per selection; three selections give +90%. | 3 |
| **Turbo Skiro** | +20% original base movement speed per selection; does not raise boost speed. | 2 |
| **Bigger Battery** | +40% original base battery capacity per selection (100 → 140 → 180); preserve charge percentage when capacity changes. | 2 |
| **Double Trouble** | Each firing cycle adds a second full-damage instantaneous laser hit. Prefer another valid visible in-range target; otherwise hit the original if still alive and valid. Respect occlusion; never hit a dead target. Uses upgraded laser damage and does not alter fire cadence. | 1 |
| **Panic Mode** | Boost modification described below. | 1 |
| **Emergency Snack** | Restore 30 HP, capped at maximum; eligible only while injured. | Repeatable |

Remove capped or ineligible options from the offer pool. If needed to maintain three choices, add distinct recovery rewards: **Battery Charge** (+25 current battery, clamped to capacity) and **Emergency Snack** (+30 HP). Label recovery cards `RECOVERY`; never show Emergency Snack twice as both an upgrade and a fallback. Battery Charge is eligible only below battery capacity. Each selected recovery consumes one of the six selections. If only two eligible rewards remain after applying caps and recovery eligibility, show those two cards and disable the unused third slot; do not duplicate cards or offer recovery at full health/charge.

Turbo Skiro and the existing Guilt movement advantage add: one Turbo selection plus panic gives +45% original base movement speed; two give +65%. Apply this movement bonus to ordinary riding, not boost speed. Preserve the approved [M1 baseline](Gameplay.md#m1-movement-baseline); this does not approve the separate proposed movement overhaul.

**Panic Mode:** when a boost starts while Guilt is actively chasing within 10 m, give that boost +25% boost speed and +50% duration, with normal cooldown. Snapshot eligibility at activation and retain the values until that boost ends, even if Guilt moves away. Outside that condition use the ordinary boost. This does not grant a permanent movement bonus or stack repeatedly during a boost. Show `PANIC READY` on the boost indicator while eligible. Use the currently approved base boost values, not unapproved Movement.md proposals.

**Delivery order:** Damage, Speed, Battery, and Heal can ship first for the minimum slice; all six remain required for the documented full design. A generic three-card Unity UI is sufficient. Guilt pressure, pause timing, arena battery charges, and replenishment are specified in [Gameplay §5](Gameplay.md#5-loop-within-each-route).

## 6. Visual and Audio Direction

- Low-poly PS1/VHS: low-resolution textures, short view distance, strong fog. Use a consistent limited palette: cold blue/black forest, orange-red city, unsettling green-red FERI.
- V1 lighting: surroundings should be nearly black, with black distance fog and no broad daylight fill. Keep the location palette in surfaces and small landmark accents. The battery-powered scooter lamp uses white light around the rider to reveal riding space and enemies, pulsing red during Guilt warnings. A restrained camera-facing white fill keeps Boki readable as he turns; empty battery switches off the surrounding light while retaining a faint rider fill. Preserve visible recovery-pickup and attack-warning cues in darkness. `Player_Model_Test` demonstrates the same lighting.
- Scenes must stay **readable while moving**: clear contrasting enemy silhouettes, red warnings, yellow/green pickups. Keep the VHS shader subtle and switchable.
- Show Guilt mostly as a distant silhouette in fog or on roofs. Constantly rendering a large mesh is unnecessary and less frightening.
- Audio: distant techno bass in the prologue, heartbeat near Guilt, digital transition glitches, laser sounds, scooter-wheel impacts, school bell, and an unusually quiet space before the boss.
- Keep text and humor short: 1–2 sentences per story beat, not long conversations.

## 7. Location Scope and Production Shortcuts

Do not build a real city or kilometers of travel. **Pohorje, Tabor, Lent, and FERI are recognizable stylized backdrops.** Build 3 scenes with shared sections and 3 short branches each. Alternatives reunite at one ending per scene. Nine complete maps are unnecessary.

- **Pohorje:** trees, logs, rocks, fence, gondola poles, 1 tunnel module.
- **Maribor:** 3–4 reusable building models, cars, bins, fences, road, a suggestion of river/bridge, 1 underpass.
- **FERI:** approximate building exterior, corridor, courtyard, technical corridor, **one** classroom.

Differentiate branches through **obstacle layout, encounter types, and lighting/audio**, rather than recoloring the same straight path.

## 8. Technical Target

Hardware and Editor setup live in [README.md](../README.md#development-setup); performance settings and measurement requirements live in [Gameplay.md §8](Gameplay.md#8-performance-target-and-unity-settings) and [Architecture.md §7](Architecture.md#7-project-settings-and-performance).

Keep the visual direction within that budget: simple lighting, small textures, short ParticleSystem effects, simple colliders, three gameplay scenes plus a menu, and no realistic scooter simulation or hundreds of active enemies. Add object pooling only if spawning causes measurable stutter. A Very Low preset can disable AA, reduce additional lights and view distance, and turn off grain. Test an actual Windows build on the integrated Radeon early; FPS in a stronger machine's Editor is not a valid target-hardware test.

## 9. Minimum Scope for 48 Hours

### Must Work (MVP)

1. Boki on a scooter, third-person camera, boost, jump.
2. Auto-laser, two AI types (melee and ranged), HP, death.
3. Light battery and Guilt triggering fear and pursuit.
4. Three scenes (Pohorje, Maribor, FERI), each with at least **two visually distinct routes**.
5. Route choice correctly filters the next level; closed gates give a clear reason.
6. At least one secret C passage with a permanent unlock; implement the other two by reusing short modular segments.
7. One boss with two attacks, win/lose screen, new run.
8. Working Windows build with at least 30 FPS on target hardware.

### If Time Remains

- Three fully distinct branches in all three scenes.
- All six upgrades, expanded audio, intro cutscene, more animations, route statistics.

**Priority:** if time runs short, shorten routes and reduce effects; **keep route decisions and permanent unlocks**. They define this version of the game.

## 10. Short Story and System Test

Use the [three canonical example runs in Gameplay.md §11](Gameplay.md#11-three-example-runs) to validate the narrative:

- **Run 1:** Boki discovers ways back as he reaches the professor. Even dying there must preserve those discoveries for a later attempt.
- **Run 2:** secret paths change his journey and let him weaken the boss using the panel.
- **Run 3:** his chosen sequence still seals alternatives despite remembered discoveries.

---

**Intended feeling:** Boki is in a ridiculous situation, but the world takes it deadly seriously. Each run reveals a new path, hidden city detail, or sequence of threats. When he finally reaches the professor, the most frightening line is »Danes je sobota.«
