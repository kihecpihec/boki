# Blender → Unity character handoff

**Target project:** Unity `6000.6.7f1`, URP, Windows x86_64. All new hierarchy, rig, socket, parameter and clip names below are proposals to implement after modeling.

## 1. What exists now

Read-only inspection found:

| Existing item | Verified state |
| --- | --- |
| `Game/Assets/Prefabs/Player/Boki_Player.prefab` | CharacterController and ScooterController on the root |
| CharacterController | Height 1.8 m; radius 0.35 m; center (0, 0.9, 0) |
| CameraPivot | Local position (0, 1.4, 0) |
| Visual placeholders | Boki placeholder, Scooter deck, Forward marker |
| `ScooterController.cs` | Moves and rotates its root; it does not drive an Animator |
| `ThirdPersonCamera.cs` | Requires `followPivot` and `lookAction`; obstruction mask should exclude the player |

Enemy, boss, weapon, damage and animation adapters described by the architecture are planned systems. Their model-wiring fields were not found in the inspected Scripts directory. This pack does not claim they already work.

## 2. Scale and axes contract

Use meters. The **Unity result** must face +Z, with +Y up and +X to the character's right. Give the gameplay root unit scale and a ground-centered origin. Do not compensate for a sideways import by rotating the movement root.

Proposed character sizes: Boki 1.72 m in standing bind pose; melee base 1.85 m; professor 1.78 m; Guilt 4.20 m. Confirm all of them in a graybox before approval. Guilt's silhouette height does not prescribe collider height.

For Boki, deck height + straight standing height exceeds the current capsule, so the **riding pose must bend the knees/hips**. Aim for a crown below 1.8 m from ground in the ride pose. Keep the 1.8 m capsule during the art test; change it only after movement/clearance review.

Shared scooter contact coordinates, measured in Unity-local meters from floor center between axles:

| Marker | X | Y | Z |
| --- | ---: | ---: | ---: |
| Wheel_F center | 0 | 0.10 | +0.52 |
| Wheel_R center | 0 | 0.10 | -0.52 |
| Grip_L | -0.24 | 1.07 | +0.40 |
| Grip_R | +0.24 | 1.07 | +0.40 |
| Foot_L | -0.06 | 0.16 | -0.28 |
| Foot_R | +0.06 | 0.16 | +0.08 |
| Headlight marker | 0 | 1.01 | +0.49 |
| Laser muzzle marker | 0 | 0.90 | +0.49 |

These are starting coordinates. Reconcile them with the shoulder/arm reach in Blender; move the model and contact guides together if necessary. They are **not** existing Inspector values.

## 3. Mesh and UV delivery

Suggested files per family:

```text
Source/CHR_Boki.blend
Export/CHR_Boki.fbx
Export/CHR_Boki_Animations.fbx          # optional separate clips, identical rig
Textures/T_Boki_BaseColor.png
Textures/T_Boki_Emission.png            # only if needed
Export/PROP_Scooter.fbx

Export/CHR_Melee_Base.fbx
Textures/T_Melee_Forest_BaseColor.png
Textures/T_Melee_City_BaseColor.png
Textures/T_Melee_Student_BaseColor.png
Export/CHR_Watcher.fbx
Export/PROP_ExamPaper.fbx
Export/CHR_Guilt.fbx
Export/CHR_Professor.fbx
Export/PROP_Lectern.fbx
Export/FX_DeadlineRing.fbx
```

Prepare clean mesh names from `model-specs.json`. Remove fully occluded body faces under closed clothing. Use deformation loops at bends; do not triangulate randomly through elbows. Keep quads in the source where useful and inspect a triangulated export copy so shading remains stable. Preview bend tests with the final normals.

Pack UV islands with padding appropriate to the chosen texture size. A 512² Boki/professor atlas and 256² melee atlas are proposed starting targets, not strict game constraints. Keep checked fabric and facial detail painted. Do not promise an atlas was authored: the delivered artwork is reference art.

Apply object rotation/scale **before rigging** when setting up the blockout. Do not blindly apply transforms to an already animated armature. Remove negative-scale mirror leftovers before skinning.

## 4. Skeleton proposal

Use a deliberately small Generic rig for the first bespoke jam animations. If the team later wants Humanoid retargeting, configure and test the Avatar mapping first; switching rig type alone does not prove that clips retarget correctly. Unity distinguishes these import modes in its [Rig tab documentation](https://docs.unity3d.com/6000.6/Documentation/Manual/FBXImporter-Rig.html).

```text
root                                # stays fixed for in-place clips
└─ hips
   ├─ spine
   │  └─ chest
   │     ├─ neck → head
   │     ├─ clavicle.L → upper_arm.L → lower_arm.L → hand.L
   │     └─ clavicle.R → upper_arm.R → lower_arm.R → hand.R
   ├─ thigh.L → shin.L → foot.L → toe.L
   └─ thigh.R → shin.R → foot.R → toe.R
```

Optional thumb/index bones are justified only by the professor's gesture test. Reuse an identical hierarchy, bind pose and skinning basis for all melee variants. Boki/professor/Guilt may use the same naming convention with their own proportions and animation assets.

Proposal: at most four skin influences per vertex; rigid pieces use one bone. Check weight normalization and elbow/knee collapse at extreme ride/throw/chase poses. Keep authoring control bones out of the exported deformation rig.

Watcher, scooter and prop animation can use a small transform hierarchy rather than a full humanoid armature. Preserve wheel axle pivots and the steering pivot. Wheel roll is visual; no suspension or realistic scooter physics is introduced.

Keep sockets visible during first integration. If Optimize Game Objects is enabled later, expose the transforms required by scripts and props instead of assuming their hierarchy remains accessible. See [Unity's rig optimization options](https://docs.unity3d.com/6000.6/Documentation/Manual/FBXImporter-Rig.html).

## 5. Animation contract

Author clips in place. Proposed authoring sampling is 30 fps for convenience; this is **not** the game FPS target. The gameplay root must not drift or change scale over a clip.

| Family | Minimum visual clips | Contact / ownership rule |
| --- | --- | --- |
| Boki | RideIdle, Jump; optional Boost, Steer_L/R, Hit, Death | Hands stay on grips, feet stay on deck in ride; controller owns movement |
| Melee | Idle, Pursuit, Contact, Hit, Death | Contact damage is code-owned; one family across reskins |
| Watcher | Hover, Fire (or transform-only cues) | Code spawns and moves projectiles |
| Guilt | Distant, Warning, Chase; held BossShadow pose | Invulnerable; disable chase AI in boss fight |
| Professor | Idle, ExamBarrage, DeadlineSlam, Defeat, AdjustGlasses | Exactly two attacks; gameplay controls warning and hit timing |

Barrage releases **three slow paper projectiles**. Deadline Slam uses the documented **0.8 s warning** before damage. Do not bake the warning's gameplay duration into a clip event as the sole authority. Put visual cue markers in clips if useful, but let the combat system determine spawns/hits and make animation follow it.

The current ScooterController has no Animator bridge. A later adapter will need movement/grounded/boost state access or suitable presentation signals. Candidate parameter names such as `Speed01`, `Grounded`, `BoostActive`, `Hit` and `Dead` are proposals, not implemented fields.

## 6. FBX round-trip

Make a small **scale + facing test** first. Use a one-meter cube, the character root and a clear forward marker.

Suggested export starting profile: selected mesh/armature objects only; export the deformation rig and intended actions; omit cameras/lights; disable extra leaf bones when the exporter offers that option. Forward -Z / Up Y is a common starting preset, but the result depends on authored axes and Blender/exporter version. Validate the **Unity result**, not the checkbox label. Do not apply experimental axis corrections to the finished rig.

In Unity:

1. Import the test FBX beside its textures and check cube size, upright orientation, +Z facing and unit root scale.
2. In Model settings, verify imported units/scale and the desired normal handling. Preserve the faceted shading; inspect any hard-edge seams.
3. In Rig settings, select Generic and the intended root for the proposed bespoke rig; apply and preview all clips.
4. Set Animator Apply Root Motion off for controller-driven characters. Validate that clip transforms also remain in place. Root motion changes a GameObject using animation data; it must not compete with this controller. See [Unity root motion](https://docs.unity3d.com/6000.0/Documentation/Manual/RootMotion.html).
5. Assign project URP materials explicitly. A successful FBX import does not guarantee material/shader conversion from Blender.
6. Tune texture filtering for the desired pixel look and inspect mip behavior at gameplay distance. Point filtering is a proposal; test shimmering before adopting it.
7. Test all sockets, rigid props, clip bounds and skinning, then export the rest of the cast using the approved settings.

Model import settings are documented in [Unity's Model tab reference](https://docs.unity3d.com/6000.0/Documentation/Manual/FBXImporter-Model.html). The exact project Editor is pinned to 6000.6.7f1; confirm its import UI and results there. The Blender manual could not be retrieved during preparation, so the Blender settings above are a proposed round-trip procedure, not a version-certified preset.

## 7. Player assembly proposal

Perform this later in Unity with the player prefab owner's authorization. Keep existing GUIDs and let the Editor generate metadata. Do not replace another developer's prefab through YAML edits.

```text
Boki_Player                         # existing movement root, scale 1
├─ CharacterController              # component, existing
├─ ScooterController                # component, existing
├─ CameraPivot                      # existing, stays unanimated
└─ VisualRoot                       # proposed presentation-only transform
   ├─ BokiModel
   │  ├─ Animator                   # Apply Root Motion off
   │  └─ DeformRig + SkinnedMeshRenderer
   └─ ScooterModel
      ├─ M_Deck
      ├─ Wheel_R
      └─ SteeringPivot
         ├─ Wheel_F
         ├─ M_Steering
         ├─ Socket_Grip_L / Socket_Grip_R
         ├─ Socket_Headlight
         └─ Socket_LaserMuzzle
```

Attach the visual model at a stable local origin. Pose the model to the shared ground-local contact positions. Animate/lean VisualRoot or bones, not the movement root or CameraPivot. Replace visual placeholders only after the ride test passes.

Required existing Inspector assignments: ScooterController movementCamera plus Move/Jump/Boost action references; ThirdPersonCamera followPivot = CameraPivot plus Look action reference; obstructionMask excludes the player layer. Current prefab root uses layer 2; verify the final camera/player layer policy instead of copying an unreviewed mask. Do not add a Rigidbody competing with the CharacterController.

Proposed later wiring: AutoLaser muzzle points to Socket_LaserMuzzle; light presentation points to Socket_Headlight; animation adapter points to Animator. Those systems/fields are not delivered by this pack.

## 8. Enemy and boss assembly proposals

- **Melee:** gameplay root + simple capsule + future melee/health components; child model and Animator. One shared animation family, three material/add-on appearances.
- **Watcher:** gameplay root + sphere/primitive collider + future ranged logic; child shell/eye and front projectile socket. Eye emission does not require a point light.
- **Paper:** projectile root + simple hit collider + separate paper mesh. Keep geometry visible from both sides. Projectile travel remains gameplay-owned.
- **Guilt:** pursuit root + tested simple collider + child elongated model. Do not treat the whole tall silhouette as the collision specification. Boss shadow is a separate decorative instance without chase logic.
- **Professor:** gameplay root + boss/health logic to implement; child model/Animator and separate lectern. Keep central arena floor open. A uses standard settings; B spawns 2 melee enemies once at 50% HP; C activates a start-at-85%-HP modifier only if POWER OFF was used.
- **Warning ring:** separate floor effect. Its radius and hit test must agree with the combat system; the artist does not choose a hidden different gameplay radius.

## 9. Manual acceptance checks for the later integration

- [ ] Import into pinned Editor with no missing textures, rig errors or missing metadata.
- [ ] One-meter cube is one meter; +Z facing and unit root scale are correct.
- [ ] Boki's ride pose fits the capsule envelope; hands and feet stay attached during steer/jump/boost transitions.
- [ ] No elbow/knee collapse, backpack drift, glasses detachment or wrong paper back-face culling.
- [ ] In-place clips never move the CharacterController root or CameraPivot.
- [ ] Camera follows correctly, excludes player collision, and still resolves environment obstruction.
- [ ] All melee reskins share the intended rig/clip family; no duplicate AI is added by the art.
- [ ] Watcher projectile socket faces forward; paper projectiles and warning rings match gameplay hit areas.
- [ ] Guilt remains invulnerable and does not run independent pursuit AI during the boss fight.
- [ ] Professor has only the two documented attacks; entrance modifiers work as specified.
- [ ] Primitive collision permits the same routes before and after replacing placeholders.
- [ ] Windows 1280 × 720 Low player build is measured on Ryzen 5600G during the city horde and boss. Enemy caps remain 8 / 12 / 8; these art budgets do not establish frame rate.

No items in this manual integration list are claimed complete by the reference-pack task.
