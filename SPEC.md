# Spec: Windows installer alongside the zip

## Objective

Translator is released only as `Translator-<version>-win-x64.zip`. Users unzip it somewhere and run
`Translator.exe`. They get no Start menu entry, no uninstall entry, no way to start with Windows,
and no warning if the WebView2 Runtime is missing. Each release also means unzipping a new folder
by hand.

Each release adds `Translator-<version>-win-x64-setup.exe`, built from the same published files
as the zip. The zip stays exactly as it is.

**Users:** people installing Translator on their own Windows PC. They don't have to be admins.

**Acceptance criteria**

1. Every GitHub release has both the zip and `Translator-<version>-win-x64-setup.exe`, built from
   the same `publish/` folder.
2. The installer installs for the current user only, into `%LOCALAPPDATA%\Programs\Translator`,
   with no UAC prompt.
3. It always creates a Start menu shortcut. It offers two checkboxes, both unticked by default:
   **Create a desktop shortcut** and **Start Translator when Windows starts**. The final page
   offers **Launch Translator**, ticked.
4. If the WebView2 Runtime is missing, the installer says so and offers to open Microsoft's
   download page. The install then continues.
5. Running a newer installer upgrades in place, keeping the chosen options. If Translator is
   running, it is closed first. It is not restarted automatically; **Launch Translator** does that.
6. Translator appears in *Settings → Apps*. Uninstalling removes the program files, the shortcuts,
   the startup entry and the WebView2 data folder inside the install folder. It keeps
   `%APPDATA%\Translator` (settings) and `%LOCALAPPDATA%\Translator` (logs).
7. The installer is in English and Russian, chosen from the Windows display language.
8. A CI job builds the installer on every PR and smoke-tests it: silent install, check the files,
   silent uninstall, check that everything is gone. Installer problems are caught before a
   release is tagged.

## Why per-user

The app writes next to its own exe. WebView2 creates `Translator.exe.WebView2` there as its data
folder, because `MainWindow` calls `EnsureCoreWebView2Async()` without a data folder. And users edit
`Blocklist\my.txt` there. Under `C:\Program Files` neither is writable without admin rights, and the
pop-up's browser would fail to start. `%LOCALAPPDATA%\Programs` is writable by the user, so the app
works unchanged. It's also where Inno Setup's `{autopf}` points when no admin rights are required.

## Tech Stack

- **Inno Setup 6.** It is preinstalled on GitHub's `windows-latest` runner, which is Windows Server
  2025 with 6.7.1. The script uses only Inno Setup 6 features, so it compiles with Inno Setup 7 too.
- The existing `dotnet publish` command: self-contained, single-file, win-x64, as in `release.yml`
  today.
- PowerShell for the build and smoke-test scripts. They must run on both Windows PowerShell 5.1,
  which is what this machine has, and PowerShell 7 (`pwsh`), which the workflows use. So no `&&`,
  `??` or ternaries, and no cmdlets that exist only in PowerShell 7.
- No new NuGet packages, and no changes to the app's code.

## Design

### Files

```
installer/Translator.iss        → Inno Setup script
build/Build-Release.ps1         → publish → verify payload → zip → installer   (CI and local)
build/Test-Installer.ps1        → silent install → checks → silent uninstall → checks
.github/workflows/release.yml   → calls Build-Release.ps1, uploads zip + setup.exe
.github/workflows/build.yml     → new "installer" job: Build-Release.ps1 + Test-Installer.ps1
```

`Build-Release.ps1` becomes the only place that has the `dotnet publish` flags and the list of
required files. Today they live in `release.yml`, including the warning comment about
`IncludeNativeLibrariesForSelfExtract`. Moving them into the script lets the PR job and the
release job build exactly the same payload.

```
Build-Release.ps1 -Version 1.2.3 [-OutputDir artifacts]
  1. dotnet publish Translator/Translator.csproj -c Release -r win-x64 --self-contained true
       -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:Version=$Version
       -o $OutputDir/publish
  2. Fail if any required file is missing (the list now in release.yml)
  3. Remove *.pdb
  4. Compress to $OutputDir/Translator-$Version-win-x64.zip
  5. ISCC /DAppVersion=$Version /DSourceDir=$OutputDir/publish /O$OutputDir installer/Translator.iss
       → $OutputDir/Translator-$Version-win-x64-setup.exe
  ISCC is found on PATH, then in "Inno Setup 6" under Program Files (x86), Program Files and
  %LOCALAPPDATA%\Programs (where a per-user winget install puts it), then in "Inno Setup 7".
  If it isn't found, the script fails with a message that names the winget package.
```

### Installer script, in outline

```ini
[Setup]
AppId={{<GUID fixed once and never changed>}
AppName=Translator
AppVersion={#AppVersion}                 ; from /DAppVersion, "0.0.0" when not given
AppPublisher=Neko in the Sky
AppPublisherURL=https://github.com/neko-in-the-sky/Translator
PrivilegesRequired=lowest                ; per-user, no UAC
DefaultDirName={autopf}\Translator       ; = %LOCALAPPDATA%\Programs\Translator
DefaultGroupName=Translator
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763                    ; matches the app's TFM
SetupIconFile=..\Translator\icons\stack-of-books.ico
UninstallDisplayIcon={app}\Translator.exe
OutputBaseFilename=Translator-{#AppVersion}-win-x64-setup
CloseApplications=yes
RestartApplications=no                   ; "Launch Translator" on the last page does this
ShowLanguageDialog=auto                  ; asks only when the display language is neither English nor Russian

[Languages]
en: compiler:Default.isl
ru: compiler:Languages\Russian.isl       ; plus [CustomMessages] for the two task labels and the WebView2 prompt

[Tasks]
desktopicon  (unchecked)
startup      (unchecked)                 ; "Start Translator when Windows starts"

[Files]
{#SourceDir}\*  → {app}, recursesubdirs, ignoreversion

[Icons]
{autoprograms}\Translator                → {app}\Translator.exe
{autodesktop}\Translator                 → Tasks: desktopicon

[Registry]
HKCU\...\CurrentVersion\Run  "Translator" = "{app}\Translator.exe"   Tasks: startup; uninsdeletevalue
HKCU\...\CurrentVersion\Run  "Translator" deleted                    Tasks: not startup
                                         ; so unticking the task on upgrade removes the old value

[Run]
{app}\Translator.exe   postinstall nowait skipifsilent   ; "Launch Translator"

[UninstallDelete]
filesandordirs {app}\Translator.exe.WebView2   ; created by the app, not by the installer

[Code]
WebView2 check in InitializeWizard / CurPageChanged(wpReady):
  installed := pv > 0.0.0.0 under
    HKLM\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}
    or HKCU\Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}
  if not installed and not WizardSilent:
    Yes/No message: "Translator needs the Microsoft Edge WebView2 Runtime, which isn't installed.
                     Open the download page?"  → Yes opens
    https://developer.microsoft.com/microsoft-edge/webview2/consumer/
  The install continues either way.
```

The registry keys and the rule that a value of `0.0.0.0` means "missing" come from Microsoft's
*Distribute your app and the WebView2 Runtime* page.

### CI

- **`build.yml`** gets a new `installer` job on `windows-latest`. It runs
  `build/Build-Release.ps1 -Version 0.0.0`, then `build/Test-Installer.ps1` on the setup exe. The
  existing Debug/Release build-and-test matrix is unchanged.
- **`release.yml`**: the Publish, Verify payload and Package steps are replaced by one
  `Build-Release.ps1 -Version <resolved version>` step. The tests and the version resolution stay.
  `gh release create` uploads both files.

### Smoke test (`Test-Installer.ps1 -Setup <path>`)

1. Install silently: `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR=<temp>\Translator
   /TASKS="desktopicon,startup"`, and wait for the exit code, which must be 0.
2. Check that `Translator.exe`, `appsettings.json`, `Blocklist\ads.txt`, `js\oxford.js` and
   `templates\info_template.html` exist under the install dir.
3. Check the Start menu shortcut, the desktop shortcut, and the HKCU `Run` value, which must point
   to the installed exe.
4. Create `<install dir>\Translator.exe.WebView2\x.txt`, standing in for the folder the app makes.
5. Uninstall silently with `unins000.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART`, and wait.
6. Check that the install dir, both shortcuts and the `Run` value are gone.
7. Create `%APPDATA%\Translator\marker.txt` before step 1, and check that it is still there after
   step 5, then delete it. This proves that uninstalling keeps user data.

The script fails with a clear message at the first failed check.

### Not changing

- **The zip:** same name, same contents, same layout.
- **The app's code:** the WebView2 data folder stays next to the exe. That is fine for a per-user
  install.
- **Code signing:** none. Windows SmartScreen will warn about an unknown publisher for the setup exe,
  as it already can for the exe inside the zip.
- **Automatic updates:** none. Users download and run the new installer.
- **`Blocklist\my.txt`:** it is still replaced on every upgrade, as happens with the zip (see
  ADR 0001, Consequences).
- **A zip copy and an installed copy on the same PC:** they share the settings file and logs. Running
  both at once isn't guarded against, as today.
- **Installing for all users.** It would need the WebView2 data folder moved in code first.

## Commands

```
Build + test:        dotnet test Translator.sln -c Debug
Release payload:     powershell -NoProfile -File build/Build-Release.ps1 -Version 0.0.0     (needs Inno Setup 6 or 7)
Installer smoke:     powershell -NoProfile -File build/Test-Installer.ps1 -Setup artifacts/Translator-0.0.0-win-x64-setup.exe
                     (CI runs the same scripts with pwsh)
Install Inno Setup:  winget install --id JRSoftware.InnoSetup -e          (local machine, one time)
```

## Project Structure

```
installer/Translator.iss          → new
build/Build-Release.ps1           → new
build/Test-Installer.ps1          → new
.github/workflows/build.yml       → new installer job
.github/workflows/release.yml     → uses Build-Release.ps1, uploads two assets
.gitignore                        → artifacts/
README.md                         → Install section: installer (recommended) or zip
docs/adr/0003-installer.md        → written when finished, replacing this SPEC.md
```

## Code Style

- **`.iss`:** sections in the order Inno Setup documents them. `#define`s at the top with defaults
  (`#ifndef AppVersion` / `#define AppVersion "0.0.0"`). A comment only where the reason isn't
  obvious, such as `RestartApplications=no` or the WebView2 key.
- **PowerShell:** `Set-StrictMode -Version Latest` and `$ErrorActionPreference = 'Stop'`. Named
  parameters. Check `$LASTEXITCODE` after every native command. Messages say what is missing and
  where, as the current "Verify payload" step does.
- **User-facing installer text:** every custom message is in `[CustomMessages]` for both `en` and
  `ru`. There is no hard-coded English in `[Code]`.
- Commit messages are imperative and in sentence case, like the existing history.

## Testing Strategy

There are no unit tests for the `.iss` file. It is covered by:

| Level | What | Where |
|---|---|---|
| Automated, every PR | Compile, then silent install, files, shortcuts, `Run` value, uninstall, clean-up, user data kept | `build.yml` → `installer` job |
| Automated, every release | The same build script, so the release assets match what the PR job tested | `release.yml` |
| Existing | App unit tests, Debug and Release | `build.yml` matrix, unchanged |
| Manual, once, on this machine | See below | — |

**Manual checks**, as a normal user without admin rights:

1. Interactive install in English: no UAC prompt, the tasks default to unticked, it installs into
   `%LOCALAPPDATA%\Programs\Translator`, and **Launch Translator** starts it. The hotkey and
   pop-up work, including WebView2 pages.
2. Run the same installer again while Translator is running. It offers to close Translator,
   upgrades, and keeps the earlier task choices.
3. Russian: run `setup.exe /LANG=ru`. The wizard and the two custom checkboxes are in Russian.
4. Tick **Start Translator when Windows starts**, then sign out and back in. Translator is in the tray.
5. Uninstall from *Settings → Apps*. The folder and shortcuts are gone, and `usersettings.json`
   and the logs are still there.
6. WebView2 prompt: build locally with `/DSimulateMissingWebView2`, a test-only define that makes
   the check report "missing". The prompt appears in both languages, and **Yes** opens the
   download page.

## Boundaries

- **Always:** keep the zip unchanged. Build the zip and the installer from the same `publish/`
  folder in one script. Keep the installer per-user, with no elevation. Add every piece of
  installer text in both English and Russian. Make the build fail loudly on a missing file or
  a failed ISCC run.
- **Ask first:** installing software on this machine, including Inno Setup via winget; changing the
  app's code; changing `AppId` after it is first released; adding code signing or any secret to CI;
  changing the existing build/test matrix; deleting anything under `%APPDATA%\Translator` or
  `%LOCALAPPDATA%\Translator`.
- **Never:** require admin rights; remove or rename the zip asset; delete user settings or logs on
  uninstall; download the WebView2 Runtime or anything else during install without the user
  clicking **Yes**; commit build output (`artifacts/`).

## Success Criteria

- [ ] `build.yml`'s `installer` job builds the setup exe and passes `Test-Installer.ps1` on a PR.
- [ ] A release run uploads `Translator-<v>-win-x64.zip`, unchanged, and `Translator-<v>-win-x64-setup.exe`.
- [ ] `release.yml` no longer has its own copy of the publish flags and the payload list; `Build-Release.ps1` does.
- [ ] Manual checks 1–6 pass on this machine without admin rights.
- [ ] The README's Install section explains both options.
- [ ] When the work is done, `SPEC.md` is replaced by `docs/adr/0003-installer.md`.

## Decisions

1. Inno Setup 6 is installed on this machine with `winget install --id JRSoftware.InnoSetup -e`,
   which is 6.7.3. CI uses the runner's preinstalled 6.7.1.
2. The publisher shown in *Settings → Apps* is **Neko in the Sky**.
