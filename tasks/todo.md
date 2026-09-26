# Tasks: User settings that survive updates

Plan: [plan.md](plan.md). Spec: [SPEC.md](../SPEC.md). Spec test numbers (#1–#14) refer to the
table in SPEC.md → Testing Strategy.

Commands used below:

```
Build: dotnet build Translator.sln -c Debug
Test:  dotnet test Translator.sln -c Debug
```

---

## Phase 1: Restructure

- [x] **T1: Move the four settings into `ApplicationSettings.UserSettings` (no behaviour change)**

  **Description:** Add the `UserSettings` options class and move `DefaultSearchEngine`, `Culture`,
  `AllowedFullscreenApps` and `Popup` into it, both in C# and in the shipped `appsettings.json`.
  Update every consumer to read from `.UserSettings`. This is one atomic rename, and the compiler
  finds every call site. It is larger than the usual file budget, but each edit is a one-line path
  change.

  **Acceptance criteria:**
  - `ApplicationSettings` has only `SearchEngines` and `UserSettings`. `UserSettings` has the four
    moved properties, with the XML doc comment from the spec.
  - The shipped `appsettings.json` has the four keys under `ApplicationSettings.UserSettings` with
    identical values.
  - Spec test #14 exists and passes: the repo's `appsettings.json` binds with every `UserSettings`
    property non-null and `SearchEngines` non-empty.

  **Verification:**
  - [x] Build succeeds in Debug (warnings are errors)
  - [x] Tests pass, including the updated `MainWindowViewModelTests` and new #14
  - [x] Manual: the app starts, the default engine is Oxford, and the pop-up size is unchanged

  **Dependencies:** None

  **Files likely touched:**
  - `Translator/Configuration/UserSettings.cs` (new)
  - `Translator/Configuration/ApplicationSettings.cs`
  - `Translator/appsettings.json`
  - `Translator/App.xaml.cs` (Culture)
  - `Translator/MainWindowViewModel.cs`
  - `Translator/NotificationStateChecker.cs`
  - `Translator/PopupSizeLocationProvider.cs`
  - `Translator.Tests/MainWindowViewModelTests.cs`
  - `Translator.Tests/UserSettingsFileTests.cs` (new, #14 only)

  **Estimated scope:** M (9 files, all mechanical)

### Checkpoint A
- [x] Tests green in Debug and Release. The app behaves exactly as before.

---

## Phase 2: Overlay

- [x] **T2: User file overrides values and nested objects, end-to-end**

  **Description:** Add `UserSettingsFile` with `DefaultPath` and `Apply(userConfig, userSettings,
  logger)`. For now, `Apply` binds scalars and nested objects only. Wire it into `App`: build the
  user `IConfiguration` eagerly from `DefaultPath` (optional, no reload) inside the existing `try`,
  and register `PostConfigure<ApplicationSettings>` to call `Apply`. This is the first slice a user
  can see.

  **Acceptance criteria:**
  - With no user file, the settings equal the shipped values (#1).
  - User `Culture`/`DefaultSearchEngine` win (#2). `{"Popup":{"DefaultWidth":800}}` changes only
    the width (#3).
  - Malformed JSON throws with the file path in the message (#12). Comments and trailing commas
    load (#13).

  **Verification:**
  - [x] Tests pass (#1, #2, #3, #12, #13)
  - [x] Build succeeds in Debug
  - [x] Manual: hand-create `%APPDATA%\Translator\usersettings.json` with `{"Culture":"ru-RU"}`
        and confirm the tray menu is in Russian. Break the JSON and confirm the startup dialog names
        the file. Delete the file afterwards.
        *Done with `{"DefaultSearchEngine":"Oxfrod"}` instead: the running app logged its
        "not in the configured list" warning. The Russian tray menu is still to be checked by eye
        at Checkpoint B. The malformed-file dialog was read via UI Automation, and its first line
        names the file.*

  **Dependencies:** T1

  **Files likely touched:**
  - `Translator/Configuration/UserSettingsFile.cs` (new)
  - `Translator/App.xaml.cs`
  - `Translator.Tests/UserSettingsFileTests.cs`

  **Estimated scope:** S

- [x] **T3: Lists in the user file replace the shipped list**

  **Description:** Before binding, `Apply` resets to empty every array-typed property of
  `UserSettings` that the user file mentions. Found by reflection, so future lists are covered.
  Presence must be detected for `"X": []` too, which .NET 8 stores as key `X` with a `null` value.
  Write the `[]` test first.

  **Acceptance criteria:**
  - A user list shorter than (#4) or longer than (#5) the shipped list gives exactly the user
    list.
  - `[]` gives an empty list (#6). No key keeps the shipped list (#7).

  **Verification:**
  - [x] Tests pass (#4–#7)
  - [x] Build succeeds in Debug
  - [x] Manual: `{"AllowedFullscreenApps":["chrome"]}` shows `["chrome"]` in the "Allowed
        fullscreen apps" line in `log.txt`

  **Dependencies:** T2

  **Files likely touched:**
  - `Translator/Configuration/UserSettingsFile.cs`
  - `Translator.Tests/UserSettingsFileTests.cs`

  **Estimated scope:** S

- [x] **T4: Keys outside `UserSettings` are ignored with a warning**

  **Description:** After overlaying, `Apply` logs a single warning listing every top-level key in
  the user file that doesn't match a `UserSettings` property (case-insensitive, like the binder).
  Tests use a small capturing `ILogger` defined in the test file, because the test project has no
  mocking library.

  **Acceptance criteria:**
  - A user file with `SearchEngines`, `Serilog` and `ApplicationSettings` doesn't change the
    settings, and one warning names all three (#8).
  - A file with only valid keys logs no warning.

  **Verification:**
  - [x] Tests pass (#8, plus the "no warning" case)
  - [x] Build succeeds in Debug
  - [x] Manual: add `"SearchEngines": []` to the user file. All seven engines still show, and
        `log.txt` has the warning.

  **Dependencies:** T3

  **Files likely touched:**
  - `Translator/Configuration/UserSettingsFile.cs`
  - `Translator.Tests/UserSettingsFileTests.cs`

  **Estimated scope:** XS

### Checkpoint B
- [x] Spec tests #1–#8, #12–#14 pass in Debug and Release
- [x] Manual checks from T2–T4 done, and the user file deleted afterwards
- [x] **Review with the user before Phase 3** (approved 2026-09-26)

---

## Phase 3: Discoverability

- [x] **T5: Create the starter file on first run**

  **Description:** Add `UserSettingsFile.EnsureCreated(path, logger)`. It writes the spec's
  starter file if the file is missing, never overwrites an existing file and never throws. It logs
  a warning when it can't write. `App` calls it before building the user configuration, using a
  logger from `new SerilogLoggerFactory(Log.Logger)`, because DI isn't built yet at that point.

  **Acceptance criteria:**
  - A missing file is created. It loads with no overrides and no warnings (#9).
  - An existing file is byte-for-byte unchanged (#10).
  - An unwritable path doesn't throw (#11). The test uses a path whose parent is an existing
    *file*, so it doesn't depend on ACLs.

  **Verification:**
  - [x] Tests pass (#9–#11)
  - [x] Build succeeds in Debug
  - [x] Manual: delete `%APPDATA%\Translator`, start the app, and confirm the starter file
        appears with the spec's comment text. Edit it, restart, and confirm the edit is kept.

  **Dependencies:** T2

  **Files likely touched:**
  - `Translator/Configuration/UserSettingsFile.cs`
  - `Translator/App.xaml.cs`
  - `Translator.Tests/UserSettingsFileTests.cs`

  **Estimated scope:** S

- [x] **T6: "Open settings folder" tray item**

  **Description:** Add a tray menu item below *Open folder* that opens the folder containing
  `UserSettingsFile.DefaultPath` in Explorer. It creates the folder first if it is missing, and
  logs a warning if that fails, following `MenuItemOpenFolder_Click`. Add the resource string in
  English and Russian, and hand-edit `Resources.Designer.cs`, because `dotnet build` doesn't run
  the resx generator.

  **Acceptance criteria:**
  - The tray menu shows "Open settings folder" in en-US and "Открыть папку настроек" in ru-RU.
  - Clicking it opens `%APPDATA%\Translator` in Explorer.

  **Verification:**
  - [x] Build succeeds in Debug **from the command line** (proves the Designer.cs edit)
  - [x] Tests pass
  - [ ] Manual: click the item in both cultures *(left for the user: the tray menu can't be driven from here. Localised text is covered by ResourcesTests.)*

  **Dependencies:** T2 (for `DefaultPath`)

  **Files likely touched:**
  - `Translator/MainWindow.xaml`
  - `Translator/MainWindow.xaml.cs`
  - `Translator/Properties/Resources.resx`
  - `Translator/Properties/Resources.ru-RU.resx`
  - `Translator/Properties/Resources.Designer.cs`

  **Estimated scope:** M (5 files, small edits)

- [x] **T7: README section and migration note**

  **Description:** Add a "Customising settings" section to the README. It covers where the file
  lives, the tray item, the flat format with an example, the override rules table (values, `Popup`
  merge, list replace, other keys ignored) and that a bad `Culture` or malformed JSON stops
  startup with an error. Add a one-time migration note for users upgrading from an older release.
  Remove the claim that full-screen apps or the default engine are configured in the shipped file.

  **Acceptance criteria:**
  - A user can create a working override from the README alone.
  - The migration note says the upgrade release overwrites `appsettings.json` one last time.

  **Verification:**
  - [x] Manual: follow the README from a clean `%APPDATA%` and confirm the example works
  - [x] The README's Usage table and "How it works" still match the app

  **Dependencies:** T3, T4, T5, T6

  **Files likely touched:**
  - `README.md`

  **Estimated scope:** XS

### Checkpoint C (done)
- [x] All 14 spec tests pass: `dotnet test Translator.sln -c Debug` and `-c Release`
- [x] Spec manual check passes, including replacing a real `dotnet publish` output folder
- [x] Every Success Criteria box in SPEC.md is ticked
- [x] Release-notes line about the one-time migration is drafted for the maintainer:

  > **Your settings now survive updates.** Settings you change go in
  > `%APPDATA%\Translator\usersettings.json` (tray menu → *Open settings folder*). This update
  > overwrites the install folder's `appsettings.json` one last time. If you had edited it, copy
  > your changes into `usersettings.json`. See the README's Settings section.

Still to be checked by eye: the Russian tray menu from a user file, and clicking *Open settings
folder*.
