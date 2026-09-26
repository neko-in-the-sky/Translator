# Spec: User settings that survive updates

## Objective

**Problem.** `Translator/appsettings.json` ships in every release zip and is copied to the output
folder with `CopyToOutputDirectory=Always`. A user who customises it (culture, pop-up size, default
engine, full-screen allow-list) loses those changes when they unzip a new release over the old one.
The only workaround is to back up the file and merge it back by hand after each update.

**Solution.** Put the settings a user may change into one explicit section,
`ApplicationSettings.UserSettings`, and let a separate file that updates never touch override
**that section only**.

| Layer | Path | Owner | Contains | Touched by updates? |
|---|---|---|---|---|
| Shipped | `<install>\appsettings.json` | Release | Everything, including the default `UserSettings` | Yes, replaced every release |
| User | `%APPDATA%\Translator\usersettings.json` | User | Only `UserSettings` values | Never |

Everything outside `UserSettings` is controlled by the release and cannot be overridden. That
includes `SearchEngines`, `Serilog` and `AllowedHosts`. A setting that must stay under release
control can't leak into the user layer, because the user file is **not** added to
`builder.Configuration`. It is read into its own configuration and applied only to the
`UserSettings` object. There is no filtering to get wrong.

**Users.** People who install Translator from the release zip and want to change one or two
settings without editing, backing up or re-merging the shipped file.

### New config structure

```jsonc
{
  "Serilog": { ... },                    // unchanged, not overridable
  "AllowedHosts": "*",                   // unchanged, not overridable
  "ApplicationSettings": {
    "SearchEngines": [ ... ],            // unchanged, not overridable
    "UserSettings": {                    // NEW, the only overridable section
      "DefaultSearchEngine": "Oxford",   // moved from ApplicationSettings
      "Culture": "en-US",                // moved from ApplicationSettings
      "AllowedFullscreenApps": [ ... ],  // moved from ApplicationSettings
      "Popup": {                         // moved from ApplicationSettings
        "DefaultWidth": 650,
        "DefaultHeight": 500,
        "VerticalOffsetFromCursor": 25
      }
    }
  }
}
```

In C#, `ApplicationSettings` keeps `SearchEngines` and gains `UserSettings UserSettings`. The new
`UserSettings` class holds `DefaultSearchEngine`, `Culture`, `AllowedFullscreenApps` and `Popup`.
Current consumers read these through `.UserSettings`: `App` (Culture), `MainWindowViewModel`
(DefaultSearchEngine), `NotificationStateChecker` (AllowedFullscreenApps) and
`PopupSizeLocationProvider` (Popup).

### User file format

The root of the user file **is** the `UserSettings` object, with no wrapper sections:

```jsonc
{
  "Culture": "ru-RU",
  "Popup": { "DefaultWidth": 800 }
}
```

The user never types a path to the section (`ApplicationSettings` → `UserSettings`), so there is
no nesting to get wrong. The file also can't express anything except user settings.

### Override rules

| Kind of value in `UserSettings` | Behaviour when the user file sets it |
|---|---|
| Scalar (`Culture`, `DefaultSearchEngine`) | Replaces the shipped value |
| Nested object (`Popup`) | Merged field by field: `Popup.DefaultWidth` alone leaves `DefaultHeight` shipped |
| List (`AllowedFullscreenApps`) | **Replaces** the shipped list completely, including `[]` |
| Key that isn't a `UserSettings` property | Ignored, and a warning is logged naming the key |

About lists: .NET configuration merges JSON arrays **by index**, and `ConfigurationBinder.Bind`
*appends* to an existing array. Neither behaviour means "the user's list", so lists get explicit
replace semantics. The rule applies to **every** array-typed property of `UserSettings`, not just
`AllowedFullscreenApps`, so a list added later behaves the same way without extra code.

Unknown keys produce a warning rather than an error on purpose. A setting that a future release
removes from `UserSettings` must not stop the app from starting after the update, which is exactly
the scenario this work fixes.

### User stories and acceptance criteria

1. **As a user, my settings survive an update.**
   - A value in `%APPDATA%\Translator\usersettings.json` is still in effect after the install
     folder is replaced with a newer release.
   - The release zip never contains anything that writes to `%APPDATA%\Translator`.

2. **As a user, I can find where to put my settings.**
   - On startup, if the user file doesn't exist, the app creates the folder and writes a starter
     file (see *Starter file* below).
   - An existing user file is **never** overwritten or modified by the app.
   - If the folder or file cannot be created (read-only profile, permissions), the app logs a
     warning and starts normally with the shipped defaults.
   - The tray menu has a new item, **Open settings folder**, which opens `%APPDATA%\Translator` in
     Explorer. Its text is localised in `Resources.resx` and `Resources.ru-RU.resx`. The existing
     *Open folder* item is unchanged.

3. **As a user, I only write what I change.**
   - A user file containing only `{"Culture":"ru-RU"}` changes the culture. Every other setting
     comes from the shipped file.
   - `{"Popup":{"DefaultWidth":800}}` changes only the width.

4. **As a user, a list I write is the list I get.**
   - A user `AllowedFullscreenApps` of `["chrome"]` results in exactly `["chrome"]`, even though
     the shipped file lists four apps.
   - A user `AllowedFullscreenApps` of `[]` results in an empty list, so the pop-up is suppressed
     over every full-screen app.

5. **As a maintainer, only `UserSettings` can be overridden.**
   - A user file containing `SearchEngines`, `Serilog`, `ApplicationSettings` or any other key that
     isn't a `UserSettings` property has no effect on the loaded settings or on logging.
   - Each such key is named in one logged warning.

6. **As a user, a broken file is reported, not silently ignored.**
   - If the user file is malformed JSON, the app shows the same startup error dialog it shows today
     and exits. The message includes the user file's full path.
   - Comments (`//`, `/* */`) and trailing commas are accepted, because .NET's JSON configuration
     provider accepts them.

### Starter file

```jsonc
// Your personal Translator settings. Updates never change this file.
//
// Copy any setting from the "UserSettings" section of appsettings.json in the
// Translator install folder to here, and change its value. Anything you leave
// out uses the default. A list you set here replaces the default list completely.
//
// Example:
//   "Culture": "ru-RU",
//   "Popup": { "DefaultWidth": 800 }
{
}
```

The starter file must load without errors and produce **no** overrides.

### Migration for existing users

The release that introduces this feature still overwrites the old install-folder
`appsettings.json`, as every release does today. Existing users must move their changes into the
new user file **once**. The README and the release notes say this explicitly. Automatic migration
is out of scope.

## Tech Stack

- .NET 8 (`net8.0-windows10.0.17763.0`), WPF
- `Microsoft.Extensions.Hosting` 8.0.0, which provides `Microsoft.Extensions.Configuration.Json`,
  `ConfigurationBinder` and the Options pattern
- Serilog 8.x via `Serilog.Settings.Configuration`
- xUnit 2.9 for tests
- **No new package references.**

## Design

```
App ctor
  UserSettingsFile.EnsureCreated(path)               // starter file if missing; never throws
  userConfig = new ConfigurationBuilder()
      .AddJsonFile(path, optional: true)              // separate: NOT builder.Configuration
      .Build()                                        // malformed JSON throws here, and the
                                                      // existing catch shows the dialog
  Configure<ApplicationSettings>(section)             // unchanged: shipped values
  PostConfigure<ApplicationSettings>(s =>
      UserSettingsFile.Apply(userConfig, s.UserSettings, logger))
```

`UserSettingsFile.Apply` overlays the user configuration onto the already-bound `UserSettings`:

1. For each array-typed property whose key is present in `userConfig`, including an empty array,
   reset that property to an empty array.
2. `userConfig.Bind(userSettings)` overlays scalars and nested objects field by field. Arrays
   are appended to the now-empty arrays, so the user's list replaces the default.
3. Log one warning listing top-level keys in `userConfig` that don't match a `UserSettings`
   property.

About detecting an empty array: .NET 8's JSON provider stores `"X": []` as key `X` with a
`null` value, so `GetSection("X").Exists()` returns false. The implementation must detect presence
through the provider (`TryGet`) or by enumerating child keys. Test 6 locks this in.

Serilog keeps reading `builder.Configuration`, which the user file never joins, so logging can't be
overridden.

### New and changed code

- `Translator/Configuration/UserSettings.cs`: new options class with the four moved properties.
- `Translator/Configuration/ApplicationSettings.cs`: remove the moved properties and add
  `public UserSettings UserSettings { get; set; }`.
- `Translator/Configuration/UserSettingsFile.cs`: new static class with `DefaultPath`,
  `EnsureCreated(path, logger)` and `Apply(userConfig, userSettings, logger)`. Every member takes
  its inputs as parameters, so tests use a temp directory, following the
  `BlocklistManager(string directory)` precedent. The name avoids a clash with the `UserSettings`
  options class.
- Consumers switch to `.UserSettings.X`: `App.xaml.cs`, `MainWindowViewModel.cs`,
  `NotificationStateChecker.cs`, `PopupSizeLocationProvider.cs`.
- `Translator/appsettings.json`: move the four keys under `ApplicationSettings.UserSettings`.
  Their values stay the same.
- A `MenuItemOpenSettingsFolder_Click` handler in `MainWindow.xaml.cs`, and a new tray item in
  `MainWindow.xaml`.

## Commands

```
Restore: dotnet restore Translator.sln
Build:   dotnet build Translator.sln -c Debug      (TreatWarningsAsErrors in Debug)
Test:    dotnet test Translator.sln -c Debug
Release: dotnet publish Translator/Translator.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish
```

## Project Structure

```
Translator/Configuration/UserSettings.cs        → new: overridable options
Translator/Configuration/UserSettingsFile.cs    → new: path, bootstrap, overlay
Translator/Configuration/ApplicationSettings.cs → loses 4 properties, gains UserSettings
Translator/appsettings.json                     → 4 keys move under ApplicationSettings.UserSettings
Translator/App.xaml.cs                          → build user config, PostConfigure, Culture path
Translator/{MainWindowViewModel,NotificationStateChecker,PopupSizeLocationProvider}.cs → .UserSettings.X
Translator/MainWindow.xaml(.cs)                 → "Open settings folder" tray item + handler
Translator/Properties/Resources*.resx           → TrayIcon_MenuItem_OpenSettingsFolder (en, ru)
Translator.Tests/UserSettingsFileTests.cs       → new tests
Translator.Tests/MainWindowViewModelTests.cs    → construct settings with the new shape
README.md                                       → "Customising settings" section + migration note
```

## Code Style

Follow the existing code: file-scoped namespaces, `_camelCase` private fields, comments that
explain *why* rather than *what*, and XML doc comments on public members whose purpose isn't
obvious.

```csharp
namespace Translator.Configuration;

/// <summary>
/// The settings a user may override from <see cref="UserSettingsFile.DefaultPath"/>.
/// Everything else in <see cref="ApplicationSettings"/> is controlled by the release.
/// </summary>
public class UserSettings
{
    public string DefaultSearchEngine { get; set; }

    public string Culture { get; set; }

    public string[] AllowedFullscreenApps { get; set; }

    public PopupSettings Popup { get; set; }
}
```

## Testing Strategy

xUnit tests in `Translator.Tests/UserSettingsFileTests.cs`. Each test gets its own temp directory,
following the `BlocklistManagerTests` pattern. Tests bind a fixture "shipped" `UserSettings`, apply
a fixture user file through `UserSettingsFile.Apply`, and assert on the result. A capturing
`ILogger` checks the warnings.

| # | Case | Expected |
|---|---|---|
| 1 | No user file | `UserSettings` equals shipped |
| 2 | User sets `Culture`, `DefaultSearchEngine` | User values win |
| 3 | User sets only `Popup.DefaultWidth` | Width is the user's, height and offset are shipped |
| 4 | User `AllowedFullscreenApps` shorter than shipped | Exactly the user list |
| 5 | User `AllowedFullscreenApps` longer than shipped | Exactly the user list (no append) |
| 6 | User `AllowedFullscreenApps: []` | Empty list |
| 7 | User file without `AllowedFullscreenApps` | Shipped list |
| 8 | User file with `SearchEngines`, `Serilog`, `ApplicationSettings` | No effect, and one warning naming all three |
| 9 | `EnsureCreated`, no file | File created, loads, and produces no overrides and no warnings |
| 10 | `EnsureCreated`, file exists | Contents byte-for-byte unchanged |
| 11 | `EnsureCreated`, path not writable | Does not throw |
| 12 | Malformed user JSON | Building the user config throws, and the message contains the path |
| 13 | User file with comments and trailing commas | Loads |
| 14 | Shipped `appsettings.json` from the repo binds | Every `UserSettings` property is non-null, and `SearchEngines` is non-empty |

Test 14 catches a typo made while restructuring the shipped file. Without it, a typo would only
show up at runtime as a null.

Manual check before merge:
1. Run the app with no user file and confirm the starter file is created.
2. Set `"Culture": "ru-RU"` in the user file, restart, and confirm the UI language changes.
3. Replace the install folder with a fresh build and confirm the setting persists.
4. Use the new tray item and confirm it opens the folder.

## Boundaries

- **Always:** run `dotnet test Translator.sln` before committing. Keep the user file optional. Keep
  the user file untouched once it exists. Localise every new UI string in both `.resx` files. Keep
  shipped default values identical when moving keys.
- **Ask first:** adding NuGet packages. Changing the release workflow or the list of files in the
  zip. Moving any further setting into or out of `UserSettings`.
- **Never:** add the user file to `builder.Configuration`. Write to or delete a user file that
  already exists. Ship a file inside `%APPDATA%\Translator`. Touch `Blocklist/my.txt` handling,
  which is out of scope.

## Success Criteria

- [x] All 14 test cases above pass in Debug and Release.
- [x] Only `UserSettings` values can be overridden. `SearchEngines`, `Serilog` and all other keys
      in the user file are ignored with a warning.
- [x] A user list replaces the shipped list, including `[]`.
- [x] A value in the user file survives replacing the install folder (manual check).
- [x] First run creates the starter file. Later runs leave it untouched.
- [x] A malformed user file produces a startup error that names the file.
- [x] The tray has an "Open settings folder" item in en-US and ru-RU. (Text verified by test; clicking it is left for a manual check.)
- [x] The README documents the user file, the override rules and the one-time migration.
- [x] No new package references, and the release workflow is unchanged.

## Out of Scope

- `Blocklist/my.txt`, which has the same overwrite-on-update problem. It is a candidate for a
  follow-up that uses the same `%APPDATA%\Translator` folder.
- `log.txt` location (still relative to the install folder).
- Settings UI, and live reload of settings while the app is running.
- Automatic migration of edits from an old install-folder `appsettings.json`.

## Decisions

Resolved during spec review on 2026-09-26:

1. The section is named `UserSettings`.
2. `UserSettings` contains `DefaultSearchEngine`, `Culture`, `AllowedFullscreenApps` and `Popup`.
3. The user file is `%APPDATA%\Translator\usersettings.json`.
4. Users who override `AllowedFullscreenApps` don't get apps added to the shipped list in later
   releases. This is the accepted trade-off of replace semantics.
5. The tray item opens the `%APPDATA%\Translator` folder, not the file.
