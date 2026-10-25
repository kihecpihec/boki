# Boss shortcut arrival regression

## Cause and fix

The old `V1BossArena.ArriveFromPortal` ray started four metres above the
entry trigger. In the authored `V1_FERI` scene it hit
`COL_Roof_Arena_Classroom_001`, placing Boki at `(0, 4.70, 79)` above the map.
The classroom floor is at Y=0. This was reproduced in the macOS Editor, so
it is not exclusive to the Windows build.

The ray now starts at the entry trigger's interior height. The regression
check requires the professor's actual movement-floor collider and a landing
within 0.2 metres of its top; a roof also counts as solid ground, so the old
generic ground check could not detect this bug.

## Reproduce and verify

No Inspector assignments, scene, prefab, or model changes are required.
Use the existing `V1_Pohorje`, `V1_Maribor`, and `V1_FERI` scenes without saving
changes to them. From a clean Editor run **Boki > Version 1 > Run Boss Shortcut Checks**,
or run from the repository root:

```sh
/Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath "$PWD/Source/Game" \
  -executeMethod Boki.Editor.V1SceneChecks.RunBossShortcut \
  -logFile /tmp/boki-boss-shortcut.log
```

This runs the physical Forest and City fast-portal fixtures, checks the
classroom floor, single boss entry, sealed skipped routes, carried resources,
next-run discoveries, victory/death and retry cleanup. It also checks that a
17.999 m/s portal entry still travels normally to City.

For manual player verification, choose a route and enter its shared portal
at 18 m/s or faster. Boki must arrive inside the professor's classroom on its
floor, behind the sealed entrance, with the professor fight running. Repeat
from City and after retry. Check camera, controls, and the portal reveal;
there must be no new gameplay errors. Windows VM launch and visual validation
must be performed on Windows; Editor checks do not establish VM compatibility
or the Ryzen 5600G performance target.

## Recorded checks

- Unity `6000.6.7f1` on macOS: strengthened check failed against the original
  runtime at stage 24, reporting the roof collider and player Y=4.70.
- With the fix: `V1_FAST_FOREST_PORTAL_PASSED`, `V1_FAST_CITY_PORTAL_PASSED`,
  and `BOSS_SHORTCUT_CHECKS_PASSED`; process exited 0.
- Architecture audit: no FAILs; existing warnings remain.
- Windows x64 Mono rebuild: `Build Finished, Result: Success.`
- No new gameplay errors were reported. The Editor emitted its existing
  `UnityEditor.Search.SearchDatabase` indexing exception in both runs; the
  integration harness already excludes this Editor-only exception.
- Windows VM launch, visual presentation, and target-hardware FPS remain untested.
