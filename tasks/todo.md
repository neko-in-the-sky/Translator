# Tasks: Log files in the user's profile, with bounded rotation

Plan: [plan.md](plan.md). Spec: [SPEC.md](../SPEC.md). Test names refer to the table in
SPEC.md → Testing Strategy.

Commands used below:

```
Build: dotnet build Translator.sln -c Debug
Test:  dotnet test Translator.sln -c Debug
Run:   dotnet run --project Translator -c Debug
```

---

## Phase 1: Logs move and rotate

- [x] **T1: Write logs to `%LOCALAPPDATA%\Translator\Logs` with bounded rotation**

  **Description:** Change the File sink in `appsettings.json` to
  `%LOCALAPPDATA%\Translator\Logs\log-.txt`, with `fileSizeLimitBytes: 10485760`,
  `rollOnFileSizeLimit: true` and `retainedFileCountLimit: 10`. Keep `rollingInterval: "Day"`.
  Add `LoggingTests` with its non-parallel collection, and the three `ShippedConfig_*` tests. Write
  `ShippedConfig_WritesLogsUnderLocalAppData` first and run it against the *old* config: it should
  fail, which confirms the test can tell the two apart. Then change the config.

  **Acceptance criteria:**
  - `ShippedConfig_WritesLogsUnderLocalAppData` passes. A logger built from the shipped config,
    with `LOCALAPPDATA` pointing at a temp directory, writes to `<temp>\Translator\Logs\log-*.txt`.
  - `ShippedConfig_RollsOnSizeAndKeepsTenFiles` passes. With only `fileSizeLimitBytes` overridden
    to 1 KB, writing enough to roll more than 10 times leaves exactly 10 files.
  - `ShippedConfig_LimitsFileSizeTo10MB` passes, and nothing else in `appsettings.json` changes.

  **Verification:**
  - [x] Build succeeds in Debug (warnings are errors)
  - [x] Tests pass, including the existing `ShippedAppSettings_BindsEveryUserSetting`
  - [x] Manual: `Run`. `%LOCALAPPDATA%\Translator\Logs\log-<today>.txt` appears and has the startup
        entries, and no new `log*.txt` appears in `Translator\bin\Debug\net8.0-windows10.0.17763.0\`

  **Dependencies:** None

  **Files likely touched:**
  - `Translator/appsettings.json`
  - `Translator.Tests/LoggingTests.cs` (new)

  **Estimated scope:** S

  **If the expansion test fails after the config change:** stop and ask. Don't move the path into
  C# without approval.

### Checkpoint A
- [x] `dotnet test Translator.sln -c Debug` and `-c Release` both pass
- [x] The T1 manual check is done
- [ ] Review with the user before Phase 2

---

## Phase 2: Tray access

- [x] **T2: Find the logs folder from configuration**

  **Description:** Add `Translator/Logging/LogFolder.cs` with `Find(IConfiguration)`, as sketched
  in SPEC → Code Style. It looks for the first `Serilog:WriteTo` entry whose `Name` is `File`
  (ignoring case), and returns the directory of its `Args:path`. It expands environment variables
  and resolves a relative path against `AppContext.BaseDirectory`. It returns `null` when there is
  no such entry or it has no path. Add the three `LogFolder_*` tests.

  **Acceptance criteria:**
  - `LogFolder_FindsShippedFolder` passes: with `LOCALAPPDATA` overridden, the shipped config
    gives `<temp>\Translator\Logs`.
  - `LogFolder_ResolvesRelativePathAgainstBaseDirectory` and `LogFolder_ReturnsNullWithoutFileSink`
    pass. The null test covers both a Console-only config and a File sink with no path.
  - `LogFolder.cs` has no log path or limit, and no `string?`, because nullable is off in the app project.

  **Verification:**
  - [x] Build succeeds in Debug
  - [x] Tests pass

  **Dependencies:** T1, because `LogFolder_FindsShippedFolder` uses the new shipped path

  **Files likely touched:**
  - `Translator/Logging/LogFolder.cs` (new)
  - `Translator.Tests/LoggingTests.cs`

  **Estimated scope:** S

- [ ] **T3: Add the "Open logs folder" tray item**

  **Description:** Add `TrayIcon_MenuItem_OpenLogsFolder`: "Open logs folder" in `Resources.resx`,
  «Открыть папку логов» in `Resources.ru-RU.resx`, plus its property in `Resources.Designer.cs`,
  added by hand. Put the menu item right after **Open settings folder** in `MainWindow.xaml`. Inject
  `IConfiguration` into `MainWindow`. `MenuItemOpenLogsFolder_Click` calls `LogFolder.Find`. On
  null, it logs a warning. Otherwise it runs `Directory.CreateDirectory` and then `explorer.exe`,
  and logs a warning on `IOException` or `UnauthorizedAccessException`, as
  `MenuItemOpenSettingsFolder_Click` does. Add an `OpenLogsFolder_IsLocalised` theory to
  `ResourcesTests`.

  **Acceptance criteria:**
  - `OpenLogsFolder_IsLocalised` passes for `en-US` and `ru-RU`.
  - The tray menu order is Translate / — / Open folder / Open settings folder / Open logs folder / Exit.
  - The handler opens the folder `LogFolder.Find` returns, and creates it first if it's missing.

  **Verification:**
  - [x] Build succeeds in Debug
  - [x] Tests pass
  - [ ] Manual: `Run`, then use the tray item and check that Explorer opens `%LOCALAPPDATA%\Translator\Logs`
  - [ ] Manual: point the File sink in `bin\Debug\…\appsettings.json` at a folder that can't be
        created, such as a path under a file. `Run`, and use the tray item: a warning is logged to the
        console, and the app keeps running. Undo the change afterwards. (The simpler case, a folder
        that was deleted, can't be tested by hand: Serilog keeps its log file open, so the folder
        can't be deleted while Translator runs, and Serilog recreates it on the next start.)
  - [ ] Manual: set `"Culture": "ru-RU"` in `%APPDATA%\Translator\usersettings.json`, `Run`, and
        check that the tray shows «Открыть папку логов». Undo the change afterwards

  **Dependencies:** T2

  **Files likely touched:**
  - `Translator/Properties/Resources.resx`
  - `Translator/Properties/Resources.ru-RU.resx`
  - `Translator/Properties/Resources.Designer.cs`
  - `Translator/MainWindow.xaml`
  - `Translator/MainWindow.xaml.cs`
  - `Translator.Tests/ResourcesTests.cs`

  **Estimated scope:** M. There are six files, but the three resource files are one string each.

### Checkpoint B
- [ ] `dotnet test Translator.sln -c Debug` and `-c Release` both pass
- [ ] The T3 manual checks are done
- [ ] Review with the user before Phase 3

---

## Phase 3: Docs

- [x] **T4: Update the README**

  **Description:** In the Usage table, change the tray row to include opening the logs folder. In
  Settings, replace "a warning in `log.txt` names it" with wording that points to the logs in
  `%LOCALAPPDATA%\Translator\Logs`, which **Open logs folder** in the tray opens. Don't add an
  upgrade note about old log files: those go in the release notes (see commit `f2999d9`).

  **Acceptance criteria:**
  - `README.md` doesn't mention `log.txt`.
  - The README names `%LOCALAPPDATA%\Translator\Logs` and the tray item.

  **Verification:**
  - [x] `git grep -n "log.txt" README.md` finds nothing
  - [x] Read the rendered Markdown and check that the tray row and Settings section are accurate

  **Dependencies:** T1, T3

  **Files likely touched:**
  - `README.md`

  **Estimated scope:** XS

- [ ] **T5: Replace SPEC.md and tasks/ with ADR 0002**

  **Description:** Write `docs/adr/0002-log-files.md` in the style of ADR 0001: Context, Decision,
  Alternatives rejected and Consequences. It should cover the `%LOCALAPPDATA%` location, the
  10 × 10 MB rotation, keeping the settings in config rather than code, and `LogFolder` as the
  single source of the path. Then delete `SPEC.md` and `tasks/`, and point to the commit that
  added them.

  **Acceptance criteria:**
  - ADR 0002 exists, with Status: Accepted and today's date.
  - `SPEC.md` and `tasks/` are gone. The ADR names the commit where they can be found.

  **Verification:**
  - [ ] Build and tests pass, as a final check
  - [ ] Every SPEC.md success criterion is checked against the code before the file is deleted

  **Dependencies:** T4

  **Files likely touched:**
  - `docs/adr/0002-log-files.md` (new)
  - `SPEC.md` (deleted)
  - `tasks/plan.md`, `tasks/todo.md` (deleted)

  **Estimated scope:** S

### Checkpoint C
- [ ] Every SPEC.md success criterion is met
- [ ] `dotnet test Translator.sln` passes in Debug and Release
- [ ] Ready for PR
