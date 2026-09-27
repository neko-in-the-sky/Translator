# Implementation Plan: Windows installer alongside the zip

Spec: [SPEC.md](../SPEC.md). Tasks: [todo.md](todo.md).

## Overview

Add an Inno Setup installer, built from the same `publish/` folder as the zip, by one script that
both CI workflows call. The app's code doesn't change. The work goes in thin slices:

1. A minimal installer that installs and uninstalls.
2. An automated smoke test in CI.
3. The optional tasks and the Russian UI.
4. The WebView2 check.
5. The release workflow and the docs.

## Architecture Decisions

- **One build script.** `build/Build-Release.ps1` publishes, verifies, zips and builds the
  installer. `release.yml` and the new PR job both call it, so the PR job tests exactly what a
  release ships.
- **Per-user install into `{autopf}`** (`%LOCALAPPDATA%\Programs`). This is what lets the app
  stay unchanged (SPEC → Why per-user).
- **CI comes early.** The smoke test and the `build.yml` job come right after the minimal
  installer, so problems with the runner, such as where ISCC is or how the uninstaller behaves,
  show up before the script grows.
- **The scripts run on Windows PowerShell 5.1 and PowerShell 7.** This machine has only 5.1, and
  CI uses `pwsh`.

## Dependency Graph

```
Build-Release.ps1 + minimal Translator.iss (T1)
    │
    ├── Test-Installer.ps1 + build.yml installer job (T2)
    │       │
    │       ├── tasks, Run entry, launch, Russian (T3) ── extends the smoke test
    │       │       │
    │       │       └── WebView2 check (T4) ── needs [CustomMessages] from T3
    │       │
    │       └── release.yml uses Build-Release.ps1 (T5)
    │
    └── README (T6) ── after T3/T4, so it describes the final options
            │
            └── ADR 0003 replaces SPEC.md and tasks/ (T7)
```

## Task List

### Phase 1: Minimal installer, tested in CI
- [x] T1: Build the zip and a minimal per-user installer from one script
- [ ] T2: Smoke-test the installer on every PR

### Checkpoint A
- [ ] The installer job is green on a PR, and a local install and uninstall work without UAC.

### Phase 2: Installer features
- [x] T3: Add the shortcut, startup and launch options, and Russian
- [ ] T4: Warn when the WebView2 Runtime is missing

### Checkpoint B
- [ ] The installer job is green, and SPEC manual checks 1–6 pass on this machine.

### Phase 3: Release and docs
- [ ] T5: Release both assets from the build script
- [ ] T6: Document both install options in the README
- [ ] T7: Replace SPEC.md and tasks/ with ADR 0003

### Checkpoint C
- [ ] Every SPEC success criterion is met, apart from the first real release, which you run. Ready for PR.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Inno's uninstaller copies itself to `%TEMP%` and exits at once, so a smoke test that waits for `unins000.exe` checks too early | High | `Start-Process -Wait` waits for the process *and its descendants*. Check that it does in T2. If it doesn't, poll until `{app}` is gone, with a 60 s timeout |
| `windows-latest` moves to an image without Inno Setup, or ISCC isn't where the script looks | Med | ISCC lookup: PATH, then the known folders. If it's missing, fail with a message that names `choco install innosetup`. The PR job catches this long before a release |
| Restart Manager can't close the tray app during an upgrade, because it has no visible window | Med | Manual check 2 in Checkpoint B. If it fails, ask before adding an `AppMutex` to the app, since that changes the app's code |
| A `Run` value from an earlier install stays after the user unticks the task on upgrade | Med | A `[Registry]` entry that deletes the value, with `Tasks: not startup`. T3's smoke test runs a second install with no tasks and checks the value is gone |
| The zip changes by accident when publishing moves into the script (file set, name, `.pdb`s) | Med | T1 compares the new zip's file list with the last release's zip. They must be the same, except for the version |
| `release.yml` can't be tested without creating a real GitHub release | Med | Everything before the upload is the same script the PR job runs. Only the `gh release create` arguments are untested. The first release is yours to run (Checkpoint C) |
| Unsigned setup exe triggers SmartScreen | Low | Accepted in the spec. The README says how to get past it (*More info → Run anyway*) |
| A `Translator.exe.WebView2` folder left behind after uninstall | Low | `[UninstallDelete]`, and the smoke test creates a stand-in folder and checks that it is removed |

## Open Questions

None. Inno Setup 6.7.3 is installed locally, and the publisher is "Neko in the Sky" (SPEC → Decisions).
