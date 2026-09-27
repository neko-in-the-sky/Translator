# 3. A per-user Inno Setup installer is released alongside the zip

- **Status:** Accepted
- **Date:** 2026-09-27

## Context

Translator was released only as `Translator-<version>-win-x64.zip`. Users unzipped it somewhere and
ran `Translator.exe`. There was no Start menu entry, no uninstall entry, no way to start with
Windows, and no warning when the WebView2 Runtime was missing. Every update meant unzipping a new
folder by hand. The zip is still wanted as a portable option.

The app writes next to its own exe. WebView2 creates `Translator.exe.WebView2` there as its data
folder, because `MainWindow` calls `EnsureCoreWebView2Async()` without a data folder. And users edit
`Blocklist\my.txt` there.

## Decision

1. **Each release has two assets built from one publish folder.** `build/Build-Release.ps1`
   publishes, checks the required files, and builds both `Translator-<v>-win-x64.zip` and
   `Translator-<v>-win-x64-setup.exe`. It is now the only place with the publish flags and the
   file list. `release.yml` calls it, and so does the `installer` job in `build.yml` on every PR,
   so a PR tests exactly what a release ships.

2. **Inno Setup 6, per user.** `installer/Translator.iss` sets `PrivilegesRequired=lowest` and
   installs into `{autopf}\Translator`, which is `%LOCALAPPDATA%\Programs\Translator`. There is no
   UAC prompt, and the app needs no changes, because its install folder is writable. GitHub's
   Windows runner has Inno Setup preinstalled. The script also compiles with Inno Setup 7.

3. **What the installer does:**

   | | |
   |---|---|
   | Always | Start menu shortcut, *Settings → Apps* entry |
   | Optional, unticked | Desktop shortcut. **Start Translator when Windows starts**, an HKCU `Run` value |
   | Last page | **Launch Translator**, ticked |
   | Upgrade | Restart Manager closes a running Translator, and earlier choices are remembered. Unticking an option removes what the earlier install created |
   | WebView2 missing | On the Ready page, offers to open Microsoft's download page, and continues either way. Never asks in a silent install |
   | Uninstall | Closes a Translator running from the install folder, after an OK/Cancel prompt unless silent. Removes the program files, including the WebView2 data folder, the shortcuts and the `Run` value. Keeps `%APPDATA%\Translator` and `%LOCALAPPDATA%\Translator` |
   | Language | English or Russian, from the Windows display language |

4. **The uninstaller closes Translator itself.** Setup uses Restart Manager, but Inno's
   uninstaller doesn't. When Translator was running, the uninstaller left the files locked and
   still removed the uninstall entry. `InitializeUninstall` finds Translator processes started from
   `{app}\Translator.exe` with WMI and ends them. The app keeps no unsaved state. A copy running
   from anywhere else, such as an unzipped folder, keeps running.

5. **WebView2 detection follows Microsoft's documented check.** The Runtime is installed if `pv` is
   set, and isn't `0.0.0.0`, under
   `HKLM\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}`
   or the same path under `HKCU\Software`. `/DSimulateMissingWebView2` forces the prompt for
   testing.

6. **The installer's identity is fixed:** `AppId` `{27CEFC81-BB7D-4993-A1E4-D7AC625FF4BA}`,
   publisher "Neko in the Sky".

## Alternatives rejected

- **WiX (MSI).** It suits Group Policy deployment, which Translator doesn't need. Per-user MSIs
  are awkward, and the source is much more verbose.
- **MSIX.** It needs a code-signing certificate, or users must turn on developer mode. Its
  sandbox redirects `%APPDATA%`, which conflicts with the settings file and logs.
- **Velopack.** It adds automatic updates, at the cost of a new dependency and startup code in the
  app. That's more than was asked for.
- **Installing for all users into Program Files.** It needs admin rights, and the WebView2 data
  folder would first have to move out of the install folder in code.
- **An `AppMutex` so the uninstaller asks the user to exit Translator.** It changes the app's code
  and adds a manual step. Ending the process is safe here.
- **Removing settings and logs on uninstall.** A reinstall should pick up the user's settings, as
  is usual on Windows.

## Consequences

- The zip is unchanged. Its file list matches earlier releases, apart from an empty directory
  entry. The script writes its entries itself, so it builds the same zip on Windows PowerShell 5.1,
  whose zip commands use backslash separators, and on PowerShell 7.
- The installer isn't code-signed, so SmartScreen may warn about an unknown publisher. The README
  says how to get past it.
- There are no automatic updates. Users run the next version's setup.
- `Blocklist\my.txt` is still replaced on every upgrade, as with the zip (see ADR 0001).
- A zip copy and an installed copy on the same PC share settings and logs. Running both at once
  still isn't prevented.
- `AppId` must never change, or upgrades will install a second copy.
- `installer/Translator.iss` must stay UTF-8 with a BOM, or Inno Setup garbles the Russian messages.
- `build/Test-Installer.ps1` refuses to run on a machine where Translator is installed, because it
  would uninstall it. It needs an absolute log folder (it resolves one itself), because the
  uninstaller reruns from a temp folder. It builds its install path from `%LOCALAPPDATA%` rather
  than `%TEMP%`, which can be an 8.3 short path.
- The release workflow's upload step has only been checked by reading it. Everything before it is
  the script that the PR job runs.

The original spec and implementation plan are in the history of the `installer` branch
(`SPEC.md`, `tasks/`, from commit `a2ba178`).
