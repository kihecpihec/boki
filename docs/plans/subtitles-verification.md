# Subtitle presentation verification

Intro, ending and recorded boss dialogue share `SubtitlePresentation`: bold 32-point text, a subtle outline, a dark panel sized to its contents, consistent padding, wrapping and screen-safe positioning. Cutscene captions sit above the letterbox; gameplay captions sit lower. New lines fade in over 0.1 unscaled seconds and clear immediately when dismissed. Existing quotations, colours and dialogue timing are preserved.

Recorded dialogue creates its own scene-owned canvas instead of borrowing `V1GameFlow.objective`. Opening dialogue stays below menus; fallback victory dialogue appears above the results panel. Disabling its output clears the caption. No scenes or prefabs were edited.

## Wiring

No new Inspector assignments. `IntroOverlay` and `EndingOverlay` retain their existing `subtitle` and `subtitleBacking` references. `RecordedGameAudio` creates its subtitle output automatically, including in the shared `Player_Model_Test` demonstration scene. The audio setup helper no longer assigns the objective as a subtitle target.

## Checks performed

- Unity 6000.6.7f1 on macOS imported the new script, generated its `.meta` and compiled successfully.
- A temporary Editor probe checked both authored preview overlays at canvas widths of 1600 and 800: short lines, a long wrapping line, Slovenian diacritics, horizontal padding, vertical fit, screen margins, immediate clearing and unchanged prefab hierarchy all passed. The probe was removed after verification.
- Existing intro and ending frame capture tools completed. The reminder, Boki's opening line and the professor's Saturday line were visually inspected at 1280×720.
- The existing recorded-audio check initially failed at its fallback victory expectation because the FERI scene now has an integrated ending. With only that ending reference cleared in memory by the temporary probe, the check passed: four scene bindings, actual boss entry, question/reply, menu pause/resume, victory/reaction and disable cleanup. Additional runtime observations confirmed opening captions were visible and did not overwrite the objective. No scene changes were saved.
- Unity's existing SearchDatabase indexing exception appeared during Play Mode; the recorded-audio fixture explicitly excludes that Editor stack. No subtitle exceptions were reported.

No deterministic gameplay rules changed, so EditMode rule tests were not needed. Windows player behaviour and target-hardware performance were not tested.

## Remaining manual checks

Use the existing scene owner's checkout; this change requires no scene edits. Open `Intro_Preview` and `Ending_Preview`, then play through `V1_FERI` and use `Player_Model_Test` for the shared demonstration. Check bright shots, resize the Game view, pause and resume during the boss opening, skip each cutscene, and disable/re-enable the recorded audio output. Expect readable captions, stable objectives, no lingering panels and no new Console errors from subtitle code. Confirm the brief fade-in and final placement in a Windows player build.
