# Upgrade screen import

The source is the [UI mockup](../../Source/output/upgrade-screen-mock/level-up-ui.html).
`Assets/UI/Upgrades/UpgradeScreen.prefab` reproduces its right-hand offer,
numbered cards, red selection, dimming, scanlines, recovery tint, and input hints.
Liberation Serif and Mono approximate the mockup's Georgia/Courier typography;
the fonts and their SIL Open Font License are included under `Fonts/`.
The sample numbers are presentation fixtures from the mockup, not run state.

## Preview and ownership

Open `Assets/Scenes/Upgrade_Preview.unity` in Unity `6000.6.7f1` and enter Play
Mode. This new scene is an isolated UI sandbox owned by this change. The shared
menu preview, model test, map scenes, prefabs and build settings are untouched.
The backdrop is the existing menu background and is excluded from the reusable
upgrade prefab. No Boki model behavior or effects are added.

**Boki > Menus > Create Isolated Upgrade Preview** creates missing preview
assets. It refuses Play Mode and dirty scenes, preserves an existing preview
scene/prefab/input asset, and restores the prior scene setup.

The setup assigns every Inspector reference. The prefab's `UpgradeScreen`
contains `Panel`, `Effects`, three each of `Buttons`, `Titles`, `Descriptions`,
`Tags`, `Selection Arrow`, and dedicated `Navigate`, `Submit`, `Quick Select`
references in `UpgradeInput.asset`. Each button calls `RequestChoice` with a
zero-based index. The scene's `UpgradePreview` references `Screen`, `Notice`,
and `Reopen`. Its Event System uses the Input System UI module for pointer
input; navigation events are disabled to avoid duplicate keyboard submits.

## Manual verification

1. Enter Play Mode at 1600×900 and 1280×720. Check all card text, recovery color,
   selection border/arrow, dimming and hints. The first card starts selected.
2. Use A/D, left/right arrows, W/S or up/down arrows. Selection wraps among
   three cards. Enter chooses once; keys 1/2/3 directly choose their card.
3. Click each card, reopening with **Show Sample Offer** between choices.
   The notice reports the chosen card; reopening resets to the first card.
4. Escape does not skip selection. Input after accepting an offer cannot
   produce a second choice while hidden. Disabling/re-enabling the screen
   must not duplicate callbacks. Exit Play Mode and verify cursor restoration.
5. Toggle `ScreenEffectsEnabled` from an integration adapter to remove the
   red edges and scanlines while retaining card readability and dimming.
   The Console must show no new errors.

## Gameplay integration

Instantiate the prefab in an authorized gameplay scene with an existing Input
System Event System. Do not copy the preview adapter or backdrop into gameplay.
Before `ShowOffer()`, call `SetCard(index, title, description, tag, recovery)` for
all three slots, using eligible options supplied by the progression rules.
Subscribe to `ChoiceRequested` in `OnEnable` and unsubscribe in `OnDisable`.
The integration validates/applies one choice and calls `Hide()` only when that
choice is accepted. A rejected choice leaves the screen open. Queued offers can
be populated and shown immediately while gameplay remains paused.

The screen never awards XP, changes stats, chooses random offers, writes
`Time.timeScale`, or manages cursor capture. The integration must use the
single shared pause authority and suppress conflicting gameplay/menu input,
release the cursor while choosing, and restore it when all queued offers are
resolved. Follow [Story §5](../Story.md#5-upgrades-and-replay-motivation) and
[Architecture §6](../Architecture.md#6-combat-and-enemy-flow). Progression,
shared gameplay pause and production scene wiring remain separate work.

## Verification record

- Unity `6000.6.7f1` batch import and compilation: passed; all new assets have
  Editor-generated metadata. Logs: `/tmp/boki-upgrade-import.log` and
  `/tmp/boki-upgrade-repeat.log`.
- Asset/GUID audit: no failures; all prefab/scene GUID references resolve.
  The new isolated preview is deliberately absent from shared build settings
  and produces the same audit warning as the other preview scenes.
- Setup idempotency: passed; a second run preserved every saved scene and
  presentation asset byte-for-byte. Source/document whitespace checks passed;
  Unity-generated YAML retains the Editor's trailing spaces on empty values.
- Interactive visual/input checks above: pending.
- Windows player/5600G performance: not run; Linux import does not verify it.
- No deterministic gameplay rules changed, so no new EditMode rule tests.
