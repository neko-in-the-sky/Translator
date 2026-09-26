# 1. User settings live in a separate file that updates never touch

- **Status:** Accepted
- **Date:** 2026-09-26

## Context

`appsettings.json` ships in every release zip and is replaced whenever a user unzips a new release.
Any change a user made to it (UI language, pop-up size, default engine, full-screen allow-list) was
lost on update unless they backed it up and merged it back by hand.

## Decision

1. **One section is overridable.** `ApplicationSettings.UserSettings` holds `DefaultSearchEngine`,
   `Culture`, `AllowedFullscreenApps` and `Popup`. Nothing else can be overridden. That includes
   `SearchEngines` (engines depend on icons and page scripts that ship with each release), `Serilog`
   and every other top-level key.

2. **The file is `%APPDATA%\Translator\usersettings.json`.** Its root *is* the `UserSettings`
   object, e.g. `{ "Culture": "ru-RU" }`, so users never type a path to the section. The app writes
   a commented starter file on first run and never modifies an existing one. The tray menu has
   *Open settings folder*.

3. **The file is never added to the host's configuration.** It is loaded into an `IConfiguration`
   of its own and overlaid onto `UserSettings` in `PostConfigure<ApplicationSettings>`. This keeps
   "only `UserSettings` is overridable" true by construction, with no key filtering to get wrong.
   It also means the user file cannot change logging.

4. **Override rules:**

   | In the user file | Effect |
   |---|---|
   | Scalar | Replaces the shipped value |
   | Nested object (`Popup`) | Merged field by field |
   | List (any array property) | Replaces the shipped list completely, including `[]` |
   | Key that isn't a `UserSettings` property | Ignored, and named in one log warning |
   | Malformed JSON | Existing startup error dialog, whose message names the file |

   Lists need explicit handling: .NET configuration merges arrays by index, and
   `ConfigurationBinder.Bind` appends to an existing array. `UserSettingsFile.Apply` empties every
   array the file mentions before binding. It detects presence through `GetChildren()`, because the
   JSON provider stores `[]` as a null value that `GetSection().Exists()` reports as absent.

   Unknown keys give a warning rather than an error, so that a setting removed in a later release
   cannot stop the app from starting after the very update this file exists to survive.

## Alternatives rejected

- **`appsettings.user.json` next to the exe.** It stays portable, but a user who unzips a new
  release into a *different* folder loses their settings.
- **Adding the user file as another source on the host configuration.** Plain layering merges lists
  by index (a two-item user list keeps the shipped items 3 and 4). It would also let the file
  override `SearchEngines` and `Serilog`, which then needs a filter that has to be kept in sync with
  every new setting.
- **Every setting overridable, with list replace semantics.** A user-defined engine could reference
  icons or scripts that don't exist, and a partial override of engine *N* would inherit fields from
  shipped engine *N*.
- **`BinderOptions.ErrorOnUnknownConfiguration`.** It would turn a setting removed in a later
  release into a startup failure.

## Consequences

- Updates no longer lose user settings.
- A user who sets their own `AllowedFullscreenApps` doesn't get apps added to the shipped list in
  later releases. This is accepted as the cost of predictable replace semantics.
- Users upgrading from a release before this change lose their `appsettings.json` edits one last
  time and must move them to `usersettings.json`. The release notes for that release say so.
- Making another setting overridable means moving it into `UserSettings`, both in the class and in
  `appsettings.json`. That is a deliberate, reviewable change.
- `Blocklist/my.txt` has the same overwrite-on-update problem and is not covered by this decision.

The original spec and implementation plan are in the history of the `user-settings` branch
(`SPEC.md`, `tasks/`, from commit `feeae27`).
