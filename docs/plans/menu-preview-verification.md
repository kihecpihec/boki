# Isolated menu preview

The menu work owns `Game/Assets/Scenes/Menu_Preview.unity`, its new
`Assets/UI/Menus/` presentation assets, and the `MenuScreen`/`MenuPreview`
scripts. Existing map scenes, prefabs, input assets, gameplay scripts and build
settings are outside this change.

The visual references are [main menu](../../output/start-screen-mock/start-screen-mock.png)
and [pause menu](../../output/pause-screen-mock/pause-screen-mock.png).
The first pass uses the existing background image, a built-in font, restrained
red edges and a selected-row highlight. Exact typography, scanlines and
animated backgrounds remain presentation work.

`Menu Music` has a 2D looping Audio Source, `MenuMusic`, and an enabled Audio
Listener so this camera-free UI scene can play its main menu track. The music
setup command also repairs a missing listener when music is already wired.

`Menu Sfx` has a 2D Audio Source and `MenuSfx`, which plays the clips in
`Assets/Audio/Sfx/` from `MenuScreen` events: `menu_select` when W/S or the
arrows move the selection, `menu_click` for any activated option (Enter or
mouse), `menu_back` for Escape out of Settings/Unlocks or resuming, and
`menu_pause` when the pause menu opens. Regenerate the clips with
`python3 music/generate_menu_sfx.py`; wire them with **Boki > Menus > Add Menu
Sound Effects To Preview** (idempotent). Mouse hover does not play a sound.

## Open and use

Open `Assets/Scenes/Menu_Preview.unity` in Unity `6000.6.7f1` and enter Play Mode.
The scene is exclusively a UI sandbox. No map needs to be open.
If the scene has not been generated yet, save open scenes and choose
**Boki > Menus > Create Isolated Menu Preview**. This command preserves an
existing preview scene and restores the previous scene setup.

All Inspector references are assigned by the setup command. `Menu Canvas` has
`MenuScreen` with `Panel`, `Effects`, `Location`, `Subtitle`, `Details`, `Hint`,
four `Buttons`, and dedicated `Navigate`, `Submit`, `Back` action references.
Its `MenuPreview` component references `Screen` and `Notice`.
The `Menu Event System` uses the Input System UI module for mouse clicks;
keyboard navigation belongs to `MenuScreen` to avoid duplicate submits.

Manual verification:

1. At 1600×900 and 1280×720, verify the background, title, options and hints are
   legible. The old HUD baked into the reference image should be covered.
2. Use W/S and up/down arrows, then Enter. Also click each option with the mouse.
   Selection wraps; one Enter triggers one action.
3. Open Settings, toggle Screen Effects, and return using Back or Escape.
   Only the menu edge effect toggles. The choice survives screen navigation
   during this Play Mode session; it is not saved to disk.
4. Open Unlocks and return. It explicitly requests integration with saved data;
   it does not invent earned unlocks or modify persistence.
5. Choose New Run. A notice explains that gameplay has not loaded. Escape opens
   the pause screen. Resume hides it; Escape opens it again. Verify Settings,
   Restart Run, and Quit to Menu. Repeated transitions must not duplicate input.
6. The pause summary displays `--` until supplied with real data. Quit displays
   a preview notice. Exit Play Mode normally; it must restore the previous
   cursor state. Console must show no new errors.
7. With Game view audio unmuted, verify the main menu music fades in. New Run
   fades it out, the pause menu stays silent, and Quit to Menu fades it back in.
   Confirm there is exactly one enabled Audio Listener and no missing-listener
   warning. No additional Inspector assignments are required.
8. Verify one soft tick per W/S move, a two-note click per activated option
   (keyboard and mouse), a falling two-note sound for Escape out of a subpage,
   and a low power-down sound when Escape opens the pause menu. Effects must
   stay audible over the menu music and are silent when the menu is hidden.

## Connect later

Remove the `MenuPreview` component and its notice when integrating the view.
Bind `MenuScreen` events in the Inspector or from an integration adapter:
`NewRunRequested`, `ResumeRequested`, `RestartRequested`, `MainMenuRequested`,
`QuitRequested`, and `PauseRequested`. Runtime listeners subscribe in
`OnEnable` and unsubscribe in `OnDisable`.

The integration adapter calls `ShowMainMenu`, `ShowPauseMenu`, or `Hide` only
after the corresponding gameplay transition is accepted, and supplies a
formatted summary via `SetRunSummary`. `MenuScreen` owns its dedicated input
actions while enabled. Do not share those actions with another input owner.
The integration adapter must arbitrate cursor capture and disable conflicting
gameplay input while a menu is open.

Scene loading belongs to `RunSession` per [Architecture §4–5](../Architecture.md#4-runtime-state).
Actual pause belongs to the single shared gameplay pause authority described
in [Architecture §6](../Architecture.md#6-combat-and-enemy-flow). This UI never
writes `Time.timeScale` and does not freeze a running game. Connecting the
pause view must freeze every gameplay subsystem through that authority;
showing a panel alone is insufficient. Coordinate map ownership before wiring
the view into another developer's scenes. Add entry scenes to shared build
settings during integration, not during preview work.

## Verification record

- Audio Listener fix: Unity `6000.6.7f1` compiled the updated music setup and
  repaired the existing scene through `MenuMusicSetup.Apply`. The saved scene
  has one enabled listener on `Menu Music`; audible playback and fade checks
  remain manual Play Mode checks.
- Unity `6000.6.7f1`: scripts compiled, preview scene and presentation assets
  generated with Editor-created `.meta` files. A subsequent batch invocation
  exited successfully and preserved the existing preview scene.
- Architecture audit: no failures. Its build-list warning for `Menu_Preview`
  is expected for this sandbox; existing map-preview and milestone tooling
  warnings remain unchanged.
- No deterministic gameplay rules changed; no EditMode tests added or run.
- Interactive mouse/keyboard review and Windows player performance remain
  manual checks; a headless import does not establish either.
