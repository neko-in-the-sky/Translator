# Tasks: Windows installer alongside the zip

Plan: [plan.md](plan.md). Spec: [SPEC.md](../SPEC.md). "Manual check N" refers to
SPEC.md → Testing Strategy.

Commands used below:

```
Build:  powershell -NoProfile -File build/Build-Release.ps1 -Version 0.0.0
Smoke:  powershell -NoProfile -File build/Test-Installer.ps1 -Setup artifacts/Translator-0.0.0-win-x64-setup.exe
Tests:  dotnet test Translator.sln -c Debug
```

---

## Phase 1: Minimal installer, tested in CI

- [x] **T1: Build the zip and a minimal per-user installer from one script**

  **Description:** Add `build/Build-Release.ps1`. It takes the publish flags and the
  required-file list from `release.yml`, including the `IncludeNativeLibrariesForSelfExtract`
  warning, then removes the `.pdb` files, zips, and runs ISCC. Add a minimal
  `installer/Translator.iss`:
  - `AppId` (a new GUID), name, version, publisher and URL
  - `PrivilegesRequired=lowest` and `{autopf}\Translator`
  - x64 and `MinVersion`, the setup icon and `UninstallDisplayIcon`
  - `[Files]` taking the whole publish folder
  - the Start menu shortcut
  - `[UninstallDelete]` for `Translator.exe.WebView2`
  - `CloseApplications=yes` and `RestartApplications=no`

  Add `artifacts/` to `.gitignore`. `release.yml` stays as it is until T5.

  **Acceptance criteria:**
  - `Build` produces `artifacts/Translator-0.0.0-win-x64.zip` and
    `artifacts/Translator-0.0.0-win-x64-setup.exe`, and exits non-zero if ISCC or any required
    file is missing.
  - The new zip has the same file list as the zip from the latest GitHub release, apart from the
    version, with no `.pdb` files.
  - The script runs on Windows PowerShell 5.1.

  **Verification:**
  - [x] `Build` succeeds locally
  - [x] Zip comparison: `gh release download --pattern '*.zip'` from the latest release, then
        compare the sorted entry names. The only difference is that the old zip has a`n        directory-only entry `runtimes/win-x64/`, which doesn't affect extraction`n  - [x] Install, launch from the Start menu shortcut, upgrade and uninstall, all done silently.`n        The wizard itself is covered in Checkpoint B. Uninstalling while Translator runs closes`n        only the installed copy (SPEC → Decisions #3)`n  - [ ] Manual: install interactively. There is no UAC prompt, it installs to
        `%LOCALAPPDATA%\Programs\Translator`, the Start menu shortcut starts Translator, and the
        pop-up shows a page. Uninstall from *Settings → Apps*: the folder, including
        `Translator.exe.WebView2`, and the shortcut are gone

  **Dependencies:** None

  **Files likely touched:**
  - `build/Build-Release.ps1` (new)
  - `installer/Translator.iss` (new)
  - `.gitignore`

  **Estimated scope:** M

- [ ] **T2: Smoke-test the installer on every PR**

  **Description:** Add `build/Test-Installer.ps1 -Setup <path>`. It covers SPEC smoke-test steps
  1, 2, 4, 5, 6 and 7, and checks the Start menu shortcut. The desktop shortcut and `Run` checks
  come in T3. Add an `installer` job to `build.yml` on `windows-latest` that runs `Build` then
  `Smoke` with `shell: pwsh`, and uploads the setup exe as a workflow artifact so it can be
  tested by hand. The existing matrix job doesn't change.

  **Acceptance criteria:**
  - `Smoke` passes locally and in CI. It fails, naming the check, when a check is broken on
    purpose, for example by pointing it at a file the installer doesn't ship.
  - The script waits until uninstalling has really finished. This is checked by the WebView2
    stand-in folder being gone, not by a sleep.
  - `%APPDATA%\Translator\marker.txt` survives the uninstall and is then deleted by the script.

  **Verification:**
  - [x] `Smoke` passes locally, and fails as expected with a check broken on purpose, which is
        then undone
  - [ ] Push the branch and open a draft PR: the `installer` job and the existing matrix are green

  **Dependencies:** T1

  **Files likely touched:**
  - `build/Test-Installer.ps1` (new)
  - `.github/workflows/build.yml`

  **Estimated scope:** S

### Checkpoint A
- [ ] The `installer` job is green on the draft PR
- [ ] A local install and uninstall work without UAC (T1 manual check)
- [ ] Review with the user before Phase 2

---

## Phase 2: Installer features

- [x] **T3: Add the shortcut, startup and launch options, and Russian**

  **Description:** Add `[Languages]` with `en` and `ru`, with `ShowLanguageDialog=auto`. Add
  `[CustomMessages]` for the two task labels in both languages. Add `[Tasks]` `desktopicon` and
  `startup`, both unchecked. Add the desktop `[Icons]` entry. Add the `[Registry]` HKCU `Run`
  value (`Tasks: startup`, `uninsdeletevalue`) and a delete entry (`Tasks: not startup`). Add the
  `[Run]` postinstall launch (`nowait postinstall skipifsilent`). Extend `Smoke` to install with
  `/TASKS="desktopicon,startup"` and check the desktop shortcut and the `Run` value. Then install
  again over it with `/TASKS=""` and check both are gone, before uninstalling.

  **Acceptance criteria:**
  - `Smoke` covers both tasks, including removing the `Run` value when upgrading with the task
    unticked.
  - Every label in the installer comes from `.isl` files or `[CustomMessages]`. None is hard-coded.

  **Verification:**
  - [x] `Build` and `Smoke` pass locally
  - [ ] CI is green
  - [ ] Manual check 1: English, tasks unticked by default, launch on finish
  - [ ] Manual check 3: `setup.exe /LANG=ru`, the wizard and the two task labels are in Russian

  **Dependencies:** T2

  **Files likely touched:**
  - `installer/Translator.iss`
  - `build/Test-Installer.ps1`

  **Estimated scope:** S

- [x] **T4: Warn when the WebView2 Runtime is missing**

  **Description:** In `[Code]`, `IsWebView2Installed` reads `pv` from the HKLM `WOW6432Node` key
  and the HKCU key (SPEC → Design) and treats empty or `0.0.0.0` as missing. `#ifdef
  SimulateMissingWebView2` forces it to report "missing". When the Ready page appears, and only
  if the Runtime is missing and the install isn't silent, show a Yes/No `[CustomMessages]` prompt
  in `en` and `ru`. **Yes** opens the consumer download page with `ShellExec`. The install
  continues either way.

  **Acceptance criteria:**
  - A normal build shows no prompt on this machine, which has WebView2. A build with
    `/DSimulateMissingWebView2` shows it.
  - A silent install never prompts, so `Smoke` is unaffected.

  **Verification:**
  - [x] `Build` and `Smoke` pass locally; a `/DSimulateMissingWebView2` build compiles
  - [ ] CI is green
  - [ ] Manual check 6: the simulated build in `en` and in `ru`, **Yes** opens the page, and **No**
        continues the install

  **Dependencies:** T3, for `[CustomMessages]` and the languages

  **Files likely touched:**
  - `installer/Translator.iss`
  - `build/Build-Release.ps1`, to pass extra ISCC defines through, such as `-IsccDefines SimulateMissingWebView2`

  **Estimated scope:** S

### Checkpoint B
- [ ] The `installer` job is green
- [ ] Manual checks 1–6 pass as a user without admin rights. Check 2 is the upgrade while
      Translator is running. Check 4 is the startup entry after signing out and back in.
- [ ] If check 2 fails because Restart Manager can't close Translator, stop and ask. The fix is
      an `AppMutex`, which changes the app's code
- [ ] Review with the user before Phase 3

---

## Phase 3: Release and docs

- [ ] **T5: Release both assets from the build script**

  **Description:** In `release.yml`, replace the Publish, Verify payload and Package steps with
  one `Build-Release.ps1 -Version ${{ steps.v.outputs.version }}` step, using `shell: pwsh`. Pass
  both `artifacts/*.zip` and `artifacts/*-setup.exe` to `gh release create`. Keep the tests step
  and the version resolution as they are.

  **Acceptance criteria:**
  - `release.yml` has no publish flags and no list of required files. It calls the script.
  - The release uploads both assets.

  **Verification:**
  - [ ] Read the workflow diff; the steps before the upload are the same commands the
        `installer` job runs
  - [ ] Check the YAML with `gh workflow view release.yml` after pushing, to confirm it parses
  - [ ] The first real release is run by the user and isn't part of this task

  **Dependencies:** T2

  **Files likely touched:**
  - `.github/workflows/release.yml`

  **Estimated scope:** XS

- [ ] **T6: Document both install options in the README**

  **Description:** Rewrite the Install section:
  - the installer is recommended, installs per-user with no admin rights, and offers the two
    options
  - the zip is the portable choice
  - both use the same settings file and logs
  - SmartScreen may warn about an unknown publisher (*More info → Run anyway*)
  - uninstalling keeps the settings and logs

  **Acceptance criteria:**
  - Both assets are named as they appear on the Releases page, and the text matches the
    installer's actual behaviour from Checkpoint B.

  **Verification:**
  - [ ] Read the rendered README

  **Dependencies:** T4

  **Files likely touched:**
  - `README.md`

  **Estimated scope:** XS

- [ ] **T7: Replace SPEC.md and tasks/ with ADR 0003**

  **Description:** Write `docs/adr/0003-installer.md` in the style of ADRs 0001 and 0002:
  - Context and Decision: Inno Setup, per-user and why, one build script, the WebView2 prompt, and
    keeping user data on uninstall
  - Alternatives rejected: WiX/MSI, MSIX, Velopack, installing for all users, removing user data
  - Consequences: SmartScreen, no auto-update, `my.txt` still replaced, `AppId` fixed for good

  Delete `SPEC.md` and `tasks/`, and point to the commit that added them.

  **Acceptance criteria:**
  - ADR 0003 exists, with Status: Accepted and today's date, and records the `AppId`.
  - `SPEC.md` and `tasks/` are gone. The ADR names the commit where they can be found.

  **Verification:**
  - [ ] Every SPEC success criterion is checked before the file is deleted
  - [ ] `Tests` and `Build` pass one last time

  **Dependencies:** T5, T6

  **Files likely touched:**
  - `docs/adr/0003-installer.md` (new)
  - `SPEC.md` and `tasks/` (deleted)

  **Estimated scope:** S

### Checkpoint C
- [ ] Every SPEC success criterion is met, apart from the first real release
- [ ] CI is green on the PR
- [ ] Ready for review and merge
