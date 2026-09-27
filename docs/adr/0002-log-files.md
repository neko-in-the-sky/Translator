# 2. Log files live in the user's local app data and are capped at 100 MB

- **Status:** Accepted
- **Date:** 2026-09-27

## Context

The Serilog File sink wrote `log.txt` relative to the current directory, which `App.xaml.cs` sets
to the exe's folder. So logs were mixed in with the files of each release. They spread across the
old install folders of users who unzip each release somewhere new. And they failed silently
wherever the install folder is read-only, such as `C:\Program Files`.

`rollingInterval: "Day"` was the only limit set, so the sink's defaults applied: 31 files of up to
1 GB each, about 31 GB in total. When a file reached 1 GB, Serilog stopped writing until the next
day, and nothing reported it.

## Decision

1. **Logs go to `%LOCALAPPDATA%\Translator\Logs`.** They don't go to `%APPDATA%`, where
   `usersettings.json` lives, because logs belong to one machine and shouldn't sync with a roaming
   profile. Files are named `log-YYYYMMDD.txt`, and files after the first on the same day get
   `_001`, `_002` and so on.

2. **A new file starts each day, or sooner at 10 MB. The newest 10 files are kept.** That caps the
   logs at about 100 MB. `rollOnFileSizeLimit` means reaching the size limit starts a new file
   instead of stopping logging.

3. **All log settings stay in the `Serilog` section of `appsettings.json`.** None are written in
   C#. The path is `%LOCALAPPDATA%\\Translator\\Logs\\log-.txt`. `Serilog.Settings.Configuration`
   expands `%VAR%` in string arguments, but its README doesn't document this, so
   `LoggingTests.ShippedConfig_WritesLogsUnderLocalAppData` checks it against the shipped file.
   The logging tests read the shipped `appsettings.json` rather than a copy of it, so a change to
   the real file is what they catch.

4. **The tray item gets the folder from the same configuration.** `LogFolder.Find` returns the
   directory of the first `File` sink's path, with environment variables expanded and relative
   paths resolved the way Serilog resolves them. **Open logs folder** opens that directory, so the
   path is never written a second time in code.

## Alternatives rejected

- **Setting the path and limits in C#, in a `LogFiles` class.** Editing the JSON couldn't then
  move the logs or remove the cap. We rejected it to keep all logging configuration in the one
  file that already holds it.
- **`%APPDATA%\Translator\Logs`, next to the settings file.** There would be one folder to find,
  but logs would roam with the profile.
- **`%TEMP%\Translator`.** Windows may clean it up unpredictably, and users are unlikely to find it.
- **A tray item with its own hard-coded path.** It would open the wrong folder as soon as the
  configured path changed.
- **Deleting old `log*.txt` files from the install folder at startup.** The app would be deleting
  files outside its own logs folder. Those files stop growing and are left to the user.

## Consequences

- Logs no longer depend on the install folder: they survive updates and work from a read-only
  install. They can't use more than about 100 MB.
- Anyone who edits `appsettings.json` can move the logs or remove the cap. Every release replaces
  that file, so such an edit is lost at the next update.
- Existing users keep their old `log*.txt` files in the install folder until they delete them. The
  release notes for this release say so.
- `LoggingTests` changes `LOCALAPPDATA` for the whole test process. Its collection turns off
  parallel runs, and other tests must not depend on `LOCALAPPDATA`.
- If the File sink is removed or renamed, **Open logs folder** logs a warning instead of opening
  anything.
- A second running copy of Translator still can't open the locked log file, as before this change.

The original spec and implementation plan are in the history of the `log-files` branch
(`SPEC.md`, `tasks/`, from commit `a44ef54`).
