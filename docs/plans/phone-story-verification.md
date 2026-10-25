# Phone story notifications

The authored script and presentation requirements live in [Story §3](../Story.md#phone-messages--last-night-catching-up).

## Integration

`RunState.TryConsumePhoneStory` owns once-per-level eligibility and New Run reset. `PhoneStoryTrigger` identifies the assigned player in a primitive trigger. `PhoneStoryView` presents one card and one bounded audio source, without input or camera control. It uses the shared gameplay/pause state, skips occupied combat spaces and scripted chases, and dismisses for damage, route selection, loading, death and boss entry. Optional `reaction` clips are unassigned; no new dialogue recordings were created.

`Boki/Story/Apply Phone Notifications` is an idempotent, targeted setup command for the three V1 scenes and the required `Player_Model_Test` demonstration. The level-scene edits were explicitly authorized in this task. It refuses Play Mode and dirty scenes, restores the previous scene setup, and adds a separate `Phone Story` root without rebuilding maps or changing shared prefabs/build settings. Existing notification roots retain their Inspector assignments; rerunning only corrects trigger position/size to the authored setup.

| Scene | Trigger placement |
|---|---|
| `V1_Pohorje` | First safe-lamp marker, offset (-7.6, -2, 0), 28 × 10 × 8 volume across the riding lane |
| `V1_Maribor` | City exit marker, 30 metres before the portal, 160 × 8 × 4 volume covering A/B/C rear exits |
| `V1_FERI` | Shared player arrival, offset (0, 1, 0), 10 × 5 × 2 volume |
| `Player_Model_Test` | 12 metres forward from the player, offset (0, 1, 12), 20 × 5 × 4 volume; demonstrates the old conversation |

Inspector bindings under each `Phone Story` root:

- `PhoneStoryView`: `card` = Notification Card CanvasGroup; `message` = Message Text; `source` = root AudioSource; `notification` = existing `intro_phone_beep.wav`; optional `reaction`; `displaySeconds` = 6.
- `PhoneStoryTrigger`: `player` = scene PlayerHealth; `view` = root PhoneStoryView; `level` = Forest/City/Feri; BoxCollider has Is Trigger enabled.
- Canvas scales from 1280 × 720; card sits below the top-left location label. No raycasts or interactive controls.

The FERI beep uses falling playback pitch for an unsettling tail. After the reminder has appeared, the first FERI Guilt warning recalls the same beep once at quarter-speed alongside the established warning audio. It never triggers a chase or changes pressure timing. FERI's existing fork is only a few metres past arrival: route selection dismisses the reminder, so full six-second reading time is not guaranteed. Test this before changing level geometry.

## Verification

- Pinned Unity `6000.6.7f1`: script compilation and asset import passed; Unity generated all new `.meta` files.
- EditMode suite: **230 passed, 0 failed, 0 skipped**, including the two new phone-story eligibility/reset tests (`/tmp/boki-phone-tests.xml`).
- Architecture audit: no FAILs; existing warnings remain unrelated to this change.
- `git diff --check`: passed.
- Scene preservation: original serialized objects retained verbatim; additions are confined to a new notification root and its root-list entry. A final Unity setup rerun loaded all four scenes successfully and left their file hashes unchanged.

Manual checks still required:

1. Open `Player_Model_Test`, enter Play Mode and ride forward through the demo trigger. Read the complete old conversation at 1280 × 720; controls and camera must remain responsive. Reenter: no repeat.
2. Start a normal V1 run from MainMenu. Ride each shared section and check one message per level, including Maribor C's side escape and rear exit. Verify the first beep does not overlap the existing departure line.
3. Pause and open an upgrade selection while a card is visible: text duration and audio freeze and resume. Take damage, enter combat, select a route, die or load the next scene: card and sound dismiss without a queued replay.
4. Retry/New Run: messages become eligible again. Use the boss speed shortcut: no skipped-level messages appear in the classroom.
5. Check FERI reminder reading time before the fork, and listen to its lowered tone alongside the existing later Guilt warning. No new Console errors should appear.
6. Optional recordings can be assigned to each scene's `reaction` field after checking overlap with player voices. The text-and-tone slice works without them.

Windows player validation on the Ryzen 5600G has not been run. Headless Linux import and tests do not establish visual readability, audio quality or Windows performance.
