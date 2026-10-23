# Verification and handoff

State exactly which checks passed, which could not run, and which Editor steps remain (AGENTS.md). Never imply a check ran.

## Checks by change type

| Change | Run |
|---|---|
| Any `.cs`, asmdef, or asset | `scripts/audit.sh` (no Unity needed) |
| Rules or deterministic logic | EditMode tests (below) |
| Scene wiring, triggers, movement, AI, UI | Manual Play Mode check with the steps you list; PlayMode tests only when a runtime asmdef exists |
| Documentation only | Check references and consistency. No tests |
| Performance-sensitive change | Windows x86_64 player build on the 5600G at 1280×720 Low. Linux/macOS Editor FPS does not substitute |

## Running Unity from the command line

Use the pinned Editor from `Game/ProjectSettings/ProjectVersion.txt` (`6000.6.7f1`). Do not run a different version against the project (it would rewrite settings and `.meta` files).

If the `unity` CLI is installed (`unity --version`):

```bash
unity test Game --mode EditMode --output /tmp/boki-editmode.xml
unity recompile            # compile errors from a running Editor
```

Otherwise call the Editor directly (path varies per machine; Hub default shown):

```bash
# macOS
/Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath Game -runTests -testPlatform EditMode \
  -testResults /tmp/boki-editmode.xml -logFile -
# Windows: C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe, same arguments
```

Rules:
- Close any Editor instance already holding the project; a locked project fails.
- Run tests from a clean checkout state when possible; a first run also imports new scripts and generates their `.meta` files. Review those new `.meta` files and include them with the change (without committing unless asked).
- Read the results XML. A test run that exits 0 but skipped or never discovered a test assembly is not a pass.
- Do not edit `Library/`, `Temp/`, or the results of a previous run.

## Editor-side smoke checks

`Boki/Run Mx Smoke Checks` menu items exist for older milestones. They demonstrate wiring; they do not establish that a milestone is playable. Never add pass/fail wording like "milestone complete" based on them.

## Manual verification list (write for the user to follow)

Always provide:
1. Scene and object to open, and who owns it.
2. Inspector assignments with exact field names.
3. Steps to reproduce the behavior, expected result, and the edge you tried (double trigger, death mid-transition, New Run after unlock, pause during the effect).
4. Console must be free of new errors/warnings from your code.

For anything touching run state also run through the Architecture §10 list: New Run resets to 100 HP/100 battery/0 XP with no choices while permanent unlocks stay; gates and exits cannot fire twice; new unlocks leave the current snapshot unchanged; scene changes do not duplicate `RunSession`, input listeners, or HUD.

## Handoff template

```markdown
## Change
<one paragraph, what and why; link the canonical doc sections, don't copy them>

## Files
<list; mark new files that still need a Unity import / .meta>

## Inspector / scene setup
<assignments, or "none">

## Checks
- audit.sh: <result>
- EditMode tests: <passed N / not run because ...>
- Manual: <done / steps for the reviewer>
- Windows player build + FPS: <done / not done (still open)>

## Not done / follow-ups
<deferred items, ownership questions, doc conflicts needing a decision>
```

Put long-lived facts (rules, values, route connections) in the canonical doc in the same change; put only what-was-done in the handoff.
