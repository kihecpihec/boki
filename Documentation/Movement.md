# BOKI: LATE TO FERI — Movement

> **Status:** the first momentum iteration is implemented; remaining proposals are identified below.
> **Related:** [Gameplay.md §0](Gameplay.md#0-gameplay-pillars-and-controls) (controls), [Story.md §4](Story.md#4-enemy-design-and-escalation) (speed, boost, and Guilt values), [Architecture.md §3](Architecture.md#3-proposed-modules-and-ownership) and [§7](Architecture.md#7-project-settings-and-performance) (`ScooterController`, arcade `CharacterController`).

This document explains **why** Boki's scooter movement behaves the way it does and **how** `ScooterController` should produce that behavior. Base values (speed, boost, Guilt bonus, upgrades) stay canonical in Story.md; this document only references them. Everything marked **proposal** or **initial tuning** is new and can change after playtesting. Section 11 lists open decisions.

**Implemented scope:** the approved first iteration adds camera-relative acceleration, ground braking, gradual air steering, horizontal momentum across jumps, soft overspeed decay, 0.1 s jump buffering/coyote time, and 0.1 s landing friction grace for timed hops. `ScooterMotion` owns the engine-free state; `ScooterController` supplies input, ground probing and collision response. Existing serialized prototype values remain authoritative for this playtest: see [Gameplay §M1](Gameplay.md#m1-movement-baseline). The second iteration adds approved downhill acceleration and capped air-strafe speed gain. The owned `Player_Model_Test` scene also has wheel spin, turn lean, slope tilt, visual takeoff lean and landing squash, speed-based FOV and a boost cooldown HUD. See [PlayerEffects.md](PlayerEffects.md) for the integrated action feedback and optional Guilt camera pulse. Air boost, variable jump height, stronger falling gravity, extra camera follow lag and speed-line effects remain outside the implemented scope. Player movement audio is implemented on the shared rigged player variant; see [PlayerEffects.md](PlayerEffects.md#player-gameplay-audio). The shared controller uses this behavior in existing scenes without rewriting their assets; `Player_Model_Test` is the owned visual playtest arena.

## 1. Design Goals

Movement is the player's main skill expression: the laser aims itself, so steering is how the player avoids damage, escapes Guilt, and picks up drops. The goals are:

1. **Feel fast but stay controllable.** Boki should reach full speed almost immediately and turn sharply. A slow ramp-up feels sluggish in a short jam game.
2. **Keep momentum.** Speed gained from a boost, a downhill section, or a jump should fade gradually rather than vanish. Smooth speed changes read as "riding a scooter"; instant changes read as "a character walking."
3. **Be readable.** The player must always predict where Boki will be in half a second, because enemies and the `Deadline Slam` circle punish mistakes.
4. **Stay arcade.** Movement uses a kinematic `CharacterController`, not wheel physics ([Architecture.md §7](Architecture.md#7-project-settings-and-performance)). The scooter model can lean and tilt visually without affecting the simulation.
5. **Be cheap.** One `CharacterController.Move` and one ground probe per frame, with no per-frame allocations. Movement must not cost measurable time on the Ryzen 5600G.

## 2. Core Model: Velocity Is State

The controller keeps its own `Vector3 velocity` between frames and never overwrites it from input. Input only **changes** velocity through acceleration. Everything else in this document follows from that rule:

- Speed above the normal maximum (from a boost or a slope) survives until something actively reduces it.
- Jumping changes only the vertical part, so horizontal speed carries through the air.
- Collisions remove only the part of velocity that pushes into a surface.

Velocity is split into a **horizontal** part (on the ground plane) and a **vertical** part (gravity and jumping), and each is handled separately.

### 2.1 Per-Frame Order

Run in `Update` with `Time.deltaTime`. The `CharacterController` is not a Rigidbody, so `FixedUpdate` is unnecessary.

1. Read input from the Input System actions `Move`, `Jump`, and `Boost`. The template action has already been renamed to `Boost`.
2. Probe the ground (§6.1): grounded flag, ground normal, distance.
3. Update timers: coyote time, jump buffer, boost duration, boost cooldown.
4. Compute the current speed cap (§3.2 and §8).
5. Update horizontal velocity: acceleration, braking, or overspeed decay (§3).
6. Apply slope influence when grounded (§6.3).
7. Update vertical velocity: jump, gravity, or ground snap (§5 and §6.2).
8. `controller.Move(velocity * dt)`.
9. Remove velocity into any surface hit during the move (§7).
10. Update visuals (model yaw and lean, camera field of view) from the final velocity (§9).

Keeping the gameplay math in steps 3–7 as plain functions (inputs in, new velocity out) lets EditMode tests check them without a scene (§12).

## 3. Horizontal Movement

### 3.1 Wish Velocity

Each frame, input becomes a **wish velocity**: the input direction relative to the camera, flattened onto the ground plane and multiplied by the current speed cap. **Proposal:** camera-relative steering, so `W` always means "away from the camera." The scooter model rotates toward the direction of travel; it does not drive the direction itself. See §11 for the tank-steering alternative.

### 3.2 Acceleration Toward the Wish

When the current horizontal speed is **at or below the cap**, move the horizontal velocity toward the wish velocity by at most `acceleration × dt` per frame (`Vector3.MoveTowards`).

- **Why a high value:** at 60 m/s² Boki reaches 7 m/s in about 0.12 s. That feels responsive while still smoothing the first frames, so the camera and model don't snap.
- **Why `MoveTowards` and not lerp:** the change per second is constant and independent of frame rate, so 30 FPS and 144 FPS feel the same.
- **Turning** uses the same rule. A sharp reversal briefly passes through a lower speed, which reads as a quick scooter turn rather than an instant flip.

With no input on the ground, brake toward zero at `brakeDeceleration`. The value is lower than acceleration so Boki coasts briefly instead of stopping dead.

### 3.3 Soft Speed Cap (Overspeed)

This is the central rule for momentum. When horizontal speed is **above the cap**, input must not be allowed to reduce it instantly:

- **Magnitude:** the speed moves toward the cap by `overspeedDecay × dt` per frame. Speed above the cap fades smoothly instead of being clipped.
- **Direction:** the player can still steer. Rotate the velocity direction toward the wish direction at `overspeedTurnRate` (degrees per second) without changing its magnitude.
- **Releasing input or pressing backward** brakes with `brakeDeceleration`, so the player can always shed speed on purpose.

**Why:** a hard clamp (`velocity = Vector3.ClampMagnitude(velocity, cap)`) makes every boost end with a jolt and makes downhill sections feel flat. A soft cap gives each speed source a visible tail, which is what makes movement feel connected.

## 4. Boost

Design target: **11 m/s for 0.6 s, 3 s cooldown** ([Story.md §4](Story.md#4-enemy-design-and-escalation)). Control: `Left Shift` ([Gameplay.md §0](Gameplay.md#0-gameplay-pillars-and-controls)).

The current playtest retains the approved [M1 boost values and ground-only activation](Gameplay.md#m1-movement-baseline); its cooldown starts after the active boost ends. The longer-term design below remains a proposal.

How the design target works in the velocity model:

1. On press, if the cooldown is ready: set the horizontal speed to `max(current speed, boostSpeed)` in the wish direction (or the current travel direction if there is no input). Start the 0.6 s duration and the 3 s cooldown.
2. While the boost is active, the speed cap is `boostSpeed`. Boki holds 11 m/s and steers normally.
3. When the boost ends, the cap drops back to the base speed. The soft cap (§3.3) decays the extra speed over a fraction of a second instead of cutting it.

**Why:** step 1 makes the boost an immediate, readable burst, which matters for escaping a `Deadline Slam` circle within 0.8 s ([Story.md §3](Story.md#3-story-beats-and-script)). Step 3 removes the "hit a wall" feeling at the end. The boost works in the air too, which allows recovering from a bad jump; see §11 if that turns out to be too strong.

The boost uses the term **boost** everywhere (code, UI, and docs), as Gameplay.md requires.

## 5. Jump

### 5.1 Launch

On jump, set vertical velocity to `jumpSpeed = sqrt(2 × gravity × jumpHeight)`. Designing with **height** (in meters) instead of raw speed makes level geometry easy to check: a 1.2 m jump clears 1.0 m obstacles with room to spare. Horizontal velocity is untouched, so a jump at boost speed travels much farther than a jump from rest.

### 5.2 Forgiveness Timers

- **Coyote time (0.1 s, initial tuning):** a jump still works shortly after riding off an edge. Without it, the player loses jumps that felt correct, especially at high speed where 0.1 s covers about 1 m.
- **Jump buffer (0.1 s, initial tuning):** a jump pressed shortly before landing is stored and fires on the landing frame.

Both timers are standard because players judge timing by what they see, and the ground contact they see is a few frames off from the physics.

### 5.3 Gravity Shape

Use stronger gravity while falling than while rising (`fallGravityMultiplier ≈ 1.6`, initial tuning). Rising feels floaty enough to aim; falling is quick, so airtime stays short and landings are predictable. Releasing `Space` early can optionally cut the rise for a short hop; see §11.

### 5.4 Air Control

In the air, use the same rules as the ground but with `airAcceleration` (about one third of ground acceleration) and no braking. The player can adjust a jump but cannot instantly reverse direction in midair. That keeps jumps over gaps a commitment, which the Gondolska servisna pot route relies on ([Story.md §3](Story.md#3-story-beats-and-script)).

### 5.4.1 Air-Strafe Speed Gain

Approved for the second iteration. Camera-relative input steers the trajectory while retaining existing speed when steering forward/across it. An additional projected acceleration rewards a wish direction whose velocity projection is between zero and the ordinary riding speed. Turning across travel can therefore add strafe speed; holding straight forward at riding speed or taking the first jump grants no strafe bonus. Timed landing-hop bonuses are a separate rule in §5.5. Reverse input does not receive this gain.

Current tuning is 18 m/s² projected acceleration (up from 6), with new gain limited to 3× base riding speed plus 0.1 m/s headroom (18.1 m/s with the retained prototype). Air steering uses 35 m/s² acceleration and overspeed turning up to 360°/s; air overspeed decay is reduced to 0.75 m/s². Normal air overspeed decay still applies. This is a cap on speed generated by air strafing, not a hard cap on faster downhill momentum. Active boosts do not also receive air-strafe acceleration. No new action/binding is required. This is an original tunable approximation of the requested movement feel, not a reproduction of another game's internal formula.

### 5.5 Landing

Landing keeps horizontal velocity. The implemented first iteration gives a 0.1 s friction grace period before ground braking/overspeed decay starts; steering or deliberately pressing backward can still shed speed. A buffered or timed jump uses airborne decay without a ground friction penalty. Any remaining overspeed then decays normally through §3.3. Landing should never snap speed down, because that punishes jumping, which the levels encourage (car ramp, broken planks).

**Stronger bunny-hop iteration:** after an actual airborne interval, a fresh jump or buffered jump within 0.1 s of landing adds 1.5 m/s along existing travel. The rider must already be at least 80% of base speed and hold an input direction with a forward component along that travel. The bonus shares the air-strafe generation limit (3× base speed plus 0.1 m/s headroom, currently 18.1 m/s); it never clamps faster downhill momentum. The first jump from standing/ordinary riding, a late jump, reverse input, stationary hopping or merely holding jump does not grant the bonus. The bonus is consumed once per landing, is not awarded during an active boost, and is cleared on reset/input suspension. Releasing and pressing Space again is required for every jump; press just before touchdown to use the existing buffer.

## 6. Ground and Slopes

### 6.1 Ground Probe

Do not rely on `CharacterController.isGrounded` alone. It only updates during `Move` and flickers on slopes and small steps. Instead, each frame:

- `SphereCast` downward from the controller center, with a radius slightly smaller than the controller radius, using a ground layer mask.
- Treat Boki as grounded if the hit is within the skin width plus a small tolerance **and** the surface angle is at or below `slopeLimit` (45°, the `CharacterController` default).
- Store the hit normal for the slope rules below.

Use the non-allocating physics query overloads to avoid garbage.

### 6.2 Ground Snap

When Boki was grounded last frame, is not jumping, and ground is within `snapDistance` (0.3 m, initial tuning), set the vertical velocity to a small downward value so the move keeps him on the surface. **Why:** without snapping, fast movement over a crest or down a slope launches Boki into a series of tiny hops. That breaks the grounded state, disables jumping, and looks broken.

### 6.3 Movement on Slopes

When grounded, project the horizontal velocity onto the ground plane before moving, so Boki follows the slope instead of pushing into it or flying off it.

**Implemented — slope momentum:** add the horizontal part of `ProjectOnPlane(gravity, groundNormal) × slopeGain` while grounded. The initial gain is 0.5. Riding downhill suspends ground overspeed decay while slope acceleration adds speed; on flat ground the normal soft decay resumes. Uphill acceleration opposes travel. Releasing input still applies ground braking, which can hold this scooter on a walkable slope. Jumping immediately stops slope acceleration and carries the acquired speed into the air. `slopeGain = 0` disables the gain.

Surfaces steeper than `slopeLimit` are walls: Boki slides down them and cannot jump from them.

## 7. Collisions

After `Move`, remove only the part of velocity that points into a hit surface (from `OnControllerColliderHit` or the move's collision flags): `velocity -= min(0, dot(velocity, normal)) × normal`.

- **Glancing hits** keep most of the speed, so brushing a car or tree while riding the street slalom doesn't stop Boki.
- **Head-on hits** stop him, which is fair and readable.
- **Ceiling hits** zero any upward velocity so Boki doesn't stick to the ceiling.

Level colliders should be primitives (boxes and capsules), per the project rules, which also keeps these normals clean.

## 8. Speed Modifiers

Several systems change speed: **Turbo Skiro** (+20% movement speed), Guilt's **+25% for 2 s** bonus, and **Panic Mode** (stronger boost while Guilt is nearby) ([Story.md §4–5](Story.md#5-upgrades-and-replay-motivation)).

**Approved upgrade integration:** [Story §5](Story.md#5-upgrades-and-replay-motivation) owns exact effects and selection caps. Add Turbo Skiro and Guilt's temporary riding-speed advantage as percentages of original base movement speed (+45% with one Turbo and panic). These do not increase boost speed. Panic Mode alone modifies an eligible newly activated boost: +25% speed and +50% duration while Guilt is chasing within 10 m, eligibility snapshotted at activation, normal cooldown. Use the approved M1 base values until a separate movement overhaul is authorized. Scaling acceleration with riding speed remains a movement proposal, not an approved upgrade requirement.

Modifiers live in run state (upgrades) or come from other systems (Guilt). Keep those gameplay rules separate from controller and UI wiring.

## 9. Feedback

Speed is felt mostly through presentation, not numbers:

- **Camera field of view** widens with horizontal speed, e.g. 70° at rest to about 80° at boost speed, smoothed over about 0.2 s. This is the strongest "fast" cue and costs nothing.
- **Camera follow lag:** the third-person camera trails slightly behind at high speed and catches up when Boki slows.
- **Model lean:** the scooter model rolls into turns based on sideways acceleration and pitches with the slope. Visual only (Architecture.md §7, item 3).
- **Boost cue:** a short audio cue, a brief FOV kick, and an optional speed-line overlay. Speed lines count as a screen effect and must be disableable together with the VHS/glitch effects ([Architecture.md §7](Architecture.md#7-project-settings-and-performance)).
- **HUD:** show boost cooldown so the player knows when it's ready.

## 10. Tuning Values

All values are **initial tuning** except those marked canonical. Expose them in the Inspector, or in a ScriptableObject if several scenes need the same settings.

| Parameter | Value | Source |
|---|---|---|
| Base speed | 6 m/s playtest; 7 m/s design target | Gameplay.md M1 baseline / Story.md §4 |
| Boost speed / duration / cooldown | 10.5 m/s / 0.35 s / 1 s playtest; 11 m/s / 0.6 s / 3 s design target | Gameplay.md M1 baseline / Story.md §4 |
| Guilt speed bonus | +25% for 2 s, 5 s cooldown | Canonical, Story.md §4 |
| Ground acceleration | 60 m/s² | Initial tuning |
| Air acceleration | 35 m/s² | Initial tuning |
| Brake deceleration | 25 m/s² | Initial tuning |
| Overspeed decay (ground / air) | 6 / 0.75 m/s² | Initial tuning |
| Overspeed turn rate | 360 °/s | Initial tuning |
| Jump height | 1.2 m | Initial tuning |
| Gravity / fall multiplier | 25 m/s² / 1 currently; 20 / 1.6 proposal | Retained prototype gravity; stronger falling gravity deferred |
| Timed bunny-hop speed gain | 1.5 m/s per eligible landing jump | Current playtest tuning |
| Coyote time / jump buffer / landing grace | 0.1 s / 0.1 s / 0.1 s | Implemented initial tuning |
| Snap distance | 0.3 m | Initial tuning |
| Slope limit | 45° | `CharacterController` default |
| Slope gain | 0.5 (0 = off) | Implemented initial tuning |
| Air-strafe acceleration / gain speed limit | 18 m/s² / 3× base riding speed + 0.1 m/s (18.1 m/s currently) | Implemented initial tuning |
| Visual lean / slope tilt limit | 12° / 25° | Implemented initial tuning; visual only |
| Wheel radius | 0.1 m | Imported scooter geometry |
| Field of view at rest / at boost speed | 70° / 80° | Initial tuning |

## 11. Open Decisions

These are not settled by the other design documents and need a team decision before they are treated as rules:

1. **Steering model:** camera-relative steering is approved and implemented for this iteration. Tank steering is outside the current scope.
2. **Slope momentum (§6.3):** approved and implemented; downhill riding can exceed base speed.
3. **Overspeed from repeated jumps:** approved for this iteration. Timed hops retain speed with softer airborne decay and now add the bounded landing-hop bonus in §5.5. Air-strafe speed gain is approved and implemented with the tunable limit in §5.4.1.
4. **Boost in the air:** deferred; activation remains ground only, and an active boost carries through a jump.
5. **Variable jump height:** whether releasing `Space` early cuts the jump short.
6. **Acceleration scaling:** whether the proposed momentum controller scales acceleration with riding-speed modifiers. Additive upgrade stacking, Turbo exclusion from boost speed, and Panic Mode are settled in Story §5.
7. **Slide or crouch action:** not proposed. There is no control for it in Gameplay.md §0, and it would need a new binding.

## 12. Validation

- **EditMode tests** for the movement math as pure functions: acceleration reaches the cap without exceeding it; overspeed decays to the cap and never below it; boost respects duration and cooldown; coyote time and jump buffer windows; modifier stacking; frame-rate independence (the same input over 1 s at 30 and 144 steps gives nearly the same result).
- **Manual checks** in the M1 arena ([Architecture.md §9](Architecture.md#9-development-milestones)): ride over a crest and down a slope without hopping; boost out of a 0.8 s warning circle; jump a 1 m obstacle; brush along a wall without stopping; no Console errors.
- **Performance:** confirm with the Profiler that the controller stays well below 0.1 ms per frame, and include movement in the Windows player-build test.


### First iteration verification

- Unity 6000.6.7f1 imported the new scripts, assemblies and their generated `.meta` files.
- All 50 EditMode cases passed (18 movement cases plus 32 existing combat/route cases); results are written to `Source/Game/Logs/movement-editmode.xml`. Re-run using **Boki → Player Model → Run EditMode Tests** outside Play Mode.
- Existing M1 smoke checks passed: references, grounding, wall collision, camera obstruction and 120 Play Mode frames. The rigged player test scene opens and renders in Play Mode.
- No new Inspector assignments are required. Existing input, camera, collider, speed and boost settings are retained. New movement tuning fields use the initial values above; runtime tuning changes require re-enabling the component to rebuild its rules state.
- Manual feel validation remains: in `Player_Model_Test`, boost then jump and release WASD to check airborne carry; steer during flight; time another jump on landing; coast to a stop; ride the ramp and approach walls/overhangs. Check cursor release/recapture and focus changes for stuck input. Automated UI input could not reliably focus the Game view on this machine; the Play Mode smoke check does not establish these keyboard-driven scenarios.
- Existing Editor deprecation warnings and the macOS FMOD audio-device warning remain. No Windows player or Ryzen 5600G performance validation was run.

### Second iteration verification and remaining work

Verification in Unity 6000.6.7f1: all **57 EditMode tests passed** (25 movement cases plus 32 existing cases); M1 integration smoke checks passed again. The presentation upgrade was run twice and leaves one HUD and one presentation component. The rigged scene renders in Play Mode with its boost status/bar and widened resting FOV, with no new code errors. All new assets have Editor-generated `.meta` files and the asset audit passes. Dynamic wheel/lean/FOV feel and the real keyboard-driven slope/strafe routes still require hands-on review; the existing macOS FMOD warning remains.

The second pass implements both requested speed-gain mechanics and the basic movement presentation in the owned model test scene. `ScooterPresentation` reads the controller's state, rotates axle pivots from actual grounded travel, tilts only `Visuals`, and smooths camera FOV from 70° to 80°. `BoostView` reads existing boost timers; it owns no cooldown rules. **Boki → Player Model → Upgrade Test Presentation** wires or refreshes these references without rebuilding the arena, duplicating its HUD, or changing the shared player prefab. Play Mode and dirty scenes are refused. Re-run the menu after creating an older setup.

Remaining work for this movement feature:

1. Hands-on tuning: air-strafe angle/acceleration and limit, downhill gain, landing timing, ramps, repeated hops, wheel direction, lean/FOV comfort and focus recovery. Rule tests are not evidence of the final feel.
2. Production integration: presentation is wired only in `Player_Model_Test`. The shared controller already supplies mechanics to its existing consumers; visual/HUD integration into developer-owned production scenes/prefabs still needs their ownership authorization. Guilt/Turbo/Panic state integration is separate game-system work, not implemented by this movement pass.
3. Target validation: Windows x86_64 player and Ryzen 5600G profiling, including garbage allocation and controller CPU cost.

Optional follow-ups are camera follow lag, boost audio/speed-line effects and additional riding/jump animation. Air boost, variable jump height, stronger falling gravity, and a separate slide/crouch action are optional design changes rather than requirements of the approved movement pass.

### Faster strafe and bunny-hop playtest

The current test scene uses air acceleration 35, projected strafe acceleration 18, overspeed turning 360, air decay 0.75, generated speed multiplier 3, and bunny-hop gain 1.5. To apply these values to an older owned scene, exit Play Mode, open `Player_Model_Test`, and run **Boki → Player Model → Apply Faster Strafe and Hop Tuning**. The helper refuses other scenes and dirty scenes; the shared player prefab and other developers' scenes are not edited. Ordinary riding speed, jump height and boost values are retained.

Playtest: gain riding speed, jump, then tap Space again within the landing buffer/grace window while holding forward movement. Chain correctly timed landings to build speed. Use camera-relative diagonal inputs and turn the mouse through the trajectory for faster air strafing. Missing the hop window allows ground decay/braking to resume.

Verification for the faster iteration: Unity 6000.6.7f1 compiled/imported the changes and generated the new test metadata; all **66 EditMode tests passed** (9 new faster-strafe/bunny-hop cases), M1 integration smoke checks passed again, and the asset audit passed. The helper applied the current tuning in the owned scene. Hands-on tuning and Windows/5600G validation remain pending.
