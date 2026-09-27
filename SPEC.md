# Spec: Log files in the user's profile, with bounded rotation

## Objective

Translator writes its log files next to `Translator.exe`. That is the wrong place for them:

- The install folder is for what ships in the release zip. Users unzip new releases over it or
  next to it, so logs get mixed in with program files and spread across old install folders.
- If Translator is unzipped into a read-only folder, such as `C:\Program Files`, Serilog can't
  create the file and logging stops without any error.

Log growth is not bounded in practice. `appsettings.json` sets `rollingInterval: "Day"` and relies
on the File sink's defaults: 31 files of up to 1 GB each, about 31 GB in the worst case. After a
file reaches 1 GB, Serilog stops writing until the next day, and nothing reports it.

**Users:** people who run Translator and occasionally need to find or send a log, and the
developer who reads those logs.

**Acceptance criteria**

1. Log files are written to `%LOCALAPPDATA%\Translator\Logs\`. Nothing is written next to the exe.
2. A new file starts every day, and also when the current file reaches 10 MB. The newest 10 files
   are kept and older ones are deleted, so the logs never take more than about 100 MB.
3. All log settings stay in the `Serilog` section of `appsettings.json`, as they are today. That
   covers the folder, file name, rotation and retention limits, and levels. No log path or limit
   is written in code.
4. The tray menu has **Open logs folder** («Открыть папку логов» in Russian), directly after
   **Open settings folder**. It opens the folder that the configured File sink actually writes to,
   so it still opens the right folder if the path in `appsettings.json` changes.
5. The README says where the logs are, instead of referring to `log.txt`.

`%LOCALAPPDATA%` is used rather than `%APPDATA%`, where `usersettings.json` lives, because logs
are machine-specific data. Windows syncs roaming profiles to other machines, and logs don't belong
there.

## Tech Stack

- .NET 8, WPF, `net8.0-windows10.0.17763.0`
- Serilog through `Serilog.Extensions.Hosting` 8.0.0, `Serilog.Settings.Configuration` 8.0.0 and
  `Serilog.Sinks.File` 5.0.0. These packages are already referenced, and this change adds none.
- xUnit 2.9 in `Translator.Tests`

## Design

### Configuration

Only the `File` entry in `Serilog.WriteTo` changes:

```jsonc
{
  "Name": "File",
  "Args": {
    "path": "%LOCALAPPDATA%\\Translator\\Logs\\log-.txt",  // log-20260927.txt, log-20260927_001.txt, …
    "rollingInterval": "Day",
    "fileSizeLimitBytes": 10485760,                        // 10 MB
    "rollOnFileSizeLimit": true,
    "retainedFileCountLimit": 10
  }
}
```

`Serilog.Settings.Configuration` expands `%VAR%` in string arguments with
`Environment.ExpandEnvironmentVariables`. The 8.0.0 assembly references it, but its README doesn't
document it, so a test checks the expansion end to end (see Testing Strategy). `App.xaml.cs` needs
no changes.

The File sink creates the directory itself. If the directory can't be written, Serilog writes the
failure to `SelfLog` and the app keeps running. This matches how `UserSettingsFile.EnsureCreated`
never blocks startup.

### Finding the logs folder for the tray item

The tray item must not hard-code a second copy of the path. A small helper reads it from the same
configuration that Serilog reads:

```
Translator/Logging/LogFolder.cs   (new)
  Find(IConfiguration) → the directory of Args:path in the first Serilog:WriteTo entry named "File"
                         (case-insensitive), with environment variables expanded. A relative path
                         is resolved against AppContext.BaseDirectory, which is the directory
                         App.xaml.cs makes current, just as Serilog would resolve it.
                         Returns null if there is no File sink or it has no path.
```

`MainWindow` gets `IConfiguration` from DI. The new handler follows
`MenuItemOpenSettingsFolder_Click`:

- It calls `LogFolder.Find`. If that returns null, it logs a warning and stops.
- Otherwise it runs `Directory.CreateDirectory`, because the user may have deleted the folder,
  and then `explorer.exe`.
- On `IOException` or `UnauthorizedAccessException`, it logs a warning.

**Not changing:**

- Log levels. `MinimumLevel.Default` stays `Debug`.
- The Console sink.
- `App.xaml.cs` and how Serilog is set up.
- Old `log*.txt` files already next to the exe. They are left where they are. Mention them in the
  release notes, not in the README.
- Running a second copy of Translator. It still can't open the locked log file, as today. A
  single-instance guard is a separate change.
- The user settings file. As ADR 0001 decided, it can't change logging.

## Commands

```
Restore: dotnet restore Translator.sln
Build:   dotnet build Translator.sln -c Debug      (TreatWarningsAsErrors is on in Debug)
Test:    dotnet test Translator.sln -c Debug
Release: dotnet test Translator.sln -c Release     (the same checks CI runs in both configurations)
```

## Project Structure

```
Translator/appsettings.json              → File sink path and rotation arguments
Translator/Logging/LogFolder.cs          → new: finds the File sink's folder in configuration
Translator/MainWindow.xaml(.cs)          → "Open logs folder" tray item and handler
Translator/Properties/Resources*.resx    → TrayIcon_MenuItem_OpenLogsFolder (en-US, ru-RU)
Translator/Properties/Resources.Designer.cs → regenerated property for the new string
Translator.Tests/LoggingTests.cs         → new
README.md                                → Usage table and Settings section mention the logs folder
docs/adr/0002-log-files.md               → written when the work is finished, replacing this SPEC.md
```

## Code Style

Follow `Configuration/UserSettingsFile.cs`: a static class, file-scoped namespace, XML doc
comments that explain *why*, and comments only where the reason isn't obvious. The `Translator`
project doesn't enable nullable reference types, and Debug treats warnings as errors, so don't
write `string?` in it.

```csharp
namespace Translator.Logging;

/// <summary>
/// Finds the folder that the File sink in appsettings.json writes to. The tray item opens this
/// folder, so the path lives only in configuration and there is no second copy that could drift.
/// </summary>
public static class LogFolder
{
    /// <returns>The folder, or null if no File sink with a path is configured.</returns>
    public static string Find(IConfiguration configuration)
    {
        var path = configuration.GetSection("Serilog:WriteTo").GetChildren()
            .Where(sink => string.Equals(sink["Name"], "File", StringComparison.OrdinalIgnoreCase))
            .Select(sink => sink["Args:path"])
            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));

        return path == null
            ? null
            : Path.GetDirectoryName(Path.GetFullPath(
                Environment.ExpandEnvironmentVariables(path), AppContext.BaseDirectory));
    }
}
```

- UI text goes in `.resx` files, never in XAML or code. Every new key is added to both
  `Resources.resx` and `Resources.ru-RU.resx`, and `ResourcesTests` checks this.
- Commit messages are imperative and in sentence case, like the existing history.

## Testing Strategy

Tests are xUnit, in `Translator.Tests/LoggingTests.cs`. They load the shipped `appsettings.json`
from the test output folder, as `UserSettingsFileTests.ShippedAppSettings_BindsEveryUserSetting`
does. Each test gets its own temp directory.

Some tests point `LOCALAPPDATA` at the temp directory and restore it in `Dispose`. That changes the
whole test process, so the class goes in an xUnit collection with
`[CollectionDefinition(DisableParallelization = true)]`.

| Test | Checks |
|---|---|
| `ShippedConfig_WritesLogsUnderLocalAppData` | With `LOCALAPPDATA` pointing at the temp directory, a logger built with `ReadFrom.Configuration` from the shipped file writes a message. After disposal, `<temp>\Translator\Logs\log-*.txt` exists and contains it. This proves the `%LOCALAPPDATA%` expansion end to end |
| `ShippedConfig_RollsOnSizeAndKeepsTenFiles` | Same setup, but an in-memory source sets only the File sink's `fileSizeLimitBytes` to 1 KB. Writing enough to roll more than 10 times leaves exactly 10 files. This fails if `rollOnFileSizeLimit` or `retainedFileCountLimit` is dropped or changed |
| `ShippedConfig_LimitsFileSizeTo10MB` | The shipped File sink has `fileSizeLimitBytes` = `10485760` and `rollingInterval` = `Day` |
| `LogFolder_FindsShippedFolder` | With `LOCALAPPDATA` overridden, `LogFolder.Find` on the shipped file returns `<temp>\Translator\Logs` |
| `LogFolder_ResolvesRelativePathAgainstBaseDirectory` | `"path": "logs\\log-.txt"` gives `AppContext.BaseDirectory\logs` |
| `LogFolder_ReturnsNullWithoutFileSink` | A config with only a Console sink, or a File sink with no path, gives `null` |

To check manually, run the Debug build, then check each of these:

1. The logs are in `%LOCALAPPDATA%\Translator\Logs\log-<today>.txt`.
2. No new `log*.txt` is written in `bin\Debug\…`.
3. The tray item opens the logs folder, in both `en-US` and `ru-RU`.

## Boundaries

- **Always:** run `dotnet build` and `dotnet test` in Debug, which treats warnings as errors,
  before every commit. Add every new UI string in both languages. Keep all log settings in
  `appsettings.json`. Keep startup working when the logs folder can't be written.
- **Ask first:** adding or upgrading NuGet packages; changing log levels or the Console sink;
  changing `App.xaml.cs`'s Serilog setup; changing the release workflow's payload check; adding a
  single-instance guard; deleting any file outside the logs folder.
- **Never:** hard-code the log path or limits in C#; ship a config that writes logs under the
  install folder; let `usersettings.json` affect logging; delete users' old log files in the
  install folder; commit log files.

## Success Criteria

- [ ] With a clean profile, starting Translator creates `%LOCALAPPDATA%\Translator\Logs\log-YYYYMMDD.txt`,
      and no new `log*.txt` appears in the exe folder.
- [ ] The shipped config limits logs to 10 files of 10 MB each, and `LoggingTests` checks the
      roll and retention behaviour using the shipped config.
- [ ] No log path or limit appears in C#. `LogFolder` reads the path from configuration.
- [ ] **Open logs folder** in the tray opens that folder, and creates it first if it was deleted.
      It is labelled in `en-US` and `ru-RU`.
- [ ] The README no longer mentions `log.txt`, and says where the logs are and how to open them.
- [ ] `dotnet test Translator.sln` passes in both Debug and Release.
- [ ] When the work is done, `SPEC.md` is replaced by `docs/adr/0002-log-files.md`, as was done for ADR 0001.

## Decisions

1. The Russian label is **«Открыть папку логов»**.
2. The menu order is Translate / — / Open folder / Open settings folder / **Open logs folder** / Exit.
3. All log settings stay in `appsettings.json`, as they are today. The rejected alternative was
   to set the path and limits in C#, which would stop edits to the JSON from breaking them.
