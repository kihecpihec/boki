# Intro Boki: atlas

The intro uses its own Boki (`Source/Game/Assets/Art/Intro/Boki_Intro.prefab`) so the face can act without
touching the player model. See [the intro plan](../../../Documentation/plans/intro-preview-verification.md#the-intro-boki).

- `build_intro_atlas.py` copies [the player atlas](../player-model-v1/Textures/T_Boki_BaseColor.png) to
  `Source/Game/Assets/Art/Intro/T_Boki_Intro_BaseColor.png`. It blanks the painted eyes (to the eye-bag colour, the
  socket the 3D eyes sit in) and the painted eyebrows, mouth and chin line (to skin). Bags, nose shading,
  stubble, ears, jacket, trousers and the watch are untouched.
- The rig is [`CHR_Boki_BindPose.fbx`](../player-model-v1/Export/CHR_Boki_BindPose.fbx), copied unchanged into
  `Source/Game/Assets/Art/Intro/` (neutral A pose, same 22 bones, no animation).

```sh
python3 Source/output/intro-boki-v1/build_intro_atlas.py   # needs Pillow
```

After regenerating the atlas, run **Boki > Intro > Rebuild Intro Preview** in the Editor.
