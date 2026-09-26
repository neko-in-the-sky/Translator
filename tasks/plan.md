# Implementation Plan: User settings that survive updates

Spec: [SPEC.md](../SPEC.md). Task details: [todo.md](todo.md).

## Overview

Move the four user-changeable settings into `ApplicationSettings.UserSettings`. Then overlay
`%APPDATA%\Translator\usersettings.json` onto that object, and only that object, at startup. The
work is split into seven tasks in three phases:

1. Restructure the config without changing behaviour.
2. Build the overlay one rule at a time: values, then lists, then unknown keys.
3. Add the starter file, the tray item and the docs.

The app builds, passes tests and runs after every task.

## Architecture Decisions

- **The user file is a separate `IConfiguration`, never added to `builder.Configuration`.** This
  keeps the rule "only `UserSettings` is overridable" true by construction. There is no key
  filtering that could be wrong, and Serilog can't be affected.
- **Overlay runs in `PostConfigure<ApplicationSettings>`.** Shipped values bind first through the
  unchanged `Configure<ApplicationSettings>`. The user overlay then mutates `s.UserSettings`.
  Loggers come from DI through `AddOptions<ApplicationSettings>().PostConfigure<ILoggerFactory>(…)`.
- **The user configuration is built eagerly in the `App` constructor.** A malformed file then
  throws inside the existing `try`, so the existing error dialog shows it, and .NET's message
  includes the path. No new error UI is needed.
- **Lists are replaced by resetting before binding.** `ConfigurationBinder.Bind` appends to
  existing arrays. `Apply` sets every array property the user file mentions to empty, then binds.
  Reflection over `UserSettings`' array properties makes this apply to any future list
  automatically.
- **Unknown keys give a warning, not an error.** `BinderOptions.ErrorOnUnknownConfiguration` is
  deliberately not used. A key removed in a later release must not stop the app from starting.
- **All `UserSettingsFile` members take the path, configuration and logger as parameters.** Tests
  use temp directories, as `BlocklistManagerTests` does, and never touch the real `%APPDATA%`.

## Dependency Graph

```
T1  UserSettings type + restructured appsettings.json + consumers
 │
 ├── T2  Scalar/object overlay, wired into App        ← first user-visible slice
 │    │
 │    ├── T3  List replace semantics
 │    │    │
 │    │    └── T4  Unknown-key warning             (reads which keys Apply consumed)
 │    │
 │    ├── T5  Starter file on first run            (needs DefaultPath + wiring from T2)
 │    │
 │    └── T6  "Open settings folder" tray item     (needs DefaultPath from T2)
 │
 └── T7  README                                    (needs final behaviour: after T3–T6)
```

T5 and T6 are independent of each other and of T3–T4, so they can be done in either order or in
parallel.

## Task List

### Phase 1: Restructure
- [x] T1: Move the four settings into `ApplicationSettings.UserSettings` (no behaviour change)

### Checkpoint A
- [x] `dotnet test` green in Debug and Release. The app starts and behaves exactly as before.

### Phase 2: Overlay
- [x] T2: User file overrides values and nested objects, end-to-end
- [x] T3: Lists in the user file replace the shipped list
- [ ] T4: Keys outside `UserSettings` are ignored with a warning

### Checkpoint B
- [ ] Spec tests 1–8, 12, 13 and 14 pass. A hand-made user file changes culture, pop-up size and
      the full-screen list, and `SearchEngines` in it is ignored with a warning in `log.txt`.
- [ ] Review with the user before Phase 3.

### Phase 3: Discoverability
- [ ] T5: Create the starter file on first run
- [ ] T6: "Open settings folder" tray item
- [ ] T7: README section and migration note

### Checkpoint C (done)
- [ ] All 14 spec tests pass in Debug and Release.
- [ ] Spec manual check passes. The last step (replace the install folder, setting persists) is
      done with a real `dotnet publish` output.
- [ ] Every Success Criteria box in SPEC.md is ticked.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| .NET 8 stores `"X": []` as key `X` with a `null` value, so `GetSection("X").Exists()` is false and an empty user list would be ignored | High: silently breaks spec story 4 | T3 test for `[]` is written first. Presence is detected via `GetChildren()` on the root and the section key, not via `Exists()` |
| `Bind` behaviour on arrays differs from the assumption (append vs replace) | Med | T3 tests cover a user list shorter than, longer than and absent from the shipped list |
| The test project may not get the shipped `appsettings.json` in its output folder (needed by spec test 14) | Low | Checked in T1. If it's missing, link the file in `Translator.Tests.csproj` with `CopyToOutputDirectory` |
| `Resources.Designer.cs` is generated only by Visual Studio's `PublicResXFileCodeGenerator`, not by `dotnet build` | Med: a new string compiles in VS but not in CI, or the other way round | T6 edits `Resources.Designer.cs` by hand to match the generator's pattern, and CI (`dotnet build`) verifies it |
| An invalid `Culture` in the user file throws `CultureNotFoundException` without naming the user file | Low | Same dialog-and-exit as today with a bad shipped culture. Accepted, not in spec scope. Noted in the README |
| Existing users lose install-folder edits one last time | Low, one-off | README migration note (T7). The maintainer adds a line to the GitHub release notes when tagging, because notes are auto-generated |
| `TreatWarningsAsErrors` in Debug | Low | Build Debug in every task's verification, not just Release |

## Out of scope (from spec)

`Blocklist/my.txt`, `log.txt` location, settings UI, live reload, automatic migration.
