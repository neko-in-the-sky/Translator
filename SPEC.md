# Capability Map: A more modern pop-up

This is the index for two changes to the pop-up. They go in one branch (`popup-style`) and one PR.
Each module has its own spec. This file holds what they share.

| Module id | Responsibility | Depends on | Spec |
|---|---|---|---|
| search-bar | Search box look and behaviour, toolbar layout, the accent colour, shared colours and button styles | — | [SPEC-search-bar.md](SPEC-search-bar.md) |
| engine-buttons | Engine button look, grouping and the highlight on the engine being shown | search-bar | [SPEC-engine-buttons.md](SPEC-engine-buttons.md) |

Build order: search-bar → engine-buttons, one or more commits each.

**Dropped: no-flash.** A third module was to keep the page hidden until the site script had
cleaned it up, with a loading bar meanwhile. It was built, but the user's check still showed a
brief flash, first of the previous entry (a hidden WebView2 stops drawing, so its first frame on
being shown was stale) and then, after parking the page off-screen instead, of something too
quick to identify. The user dropped it: the site scripts, which remove elements only once at
`DOMContentLoaded`, need improving first, and that's out of scope here. It's in this branch's
history (`fff02b0`, `6a18832`) if it's picked up again.

## Shared decisions

- **Light theme only.** Every colour is a named resource in `Translator/Styles/Popup.xaml`, so a
  dark theme can later swap them in one place.
- **Accent colour:** the shade of the Windows accent colour that Windows 11's light theme uses for
  controls, read once when the window is created with
  `Windows.UI.ViewManagement.UISettings.GetColorValue(UIColorType.AccentDark1)`. The target
  framework (`net8.0-windows10.0.17763.0`) already includes it. If the call fails, use `#005FB8`,
  that shade of the default blue. It isn't updated while the app runs. The window stores it as
  `AccentBrush` in its own resources, which override `Popup.xaml`'s fallback, so every use of
  `AccentBrush` is a `DynamicResource`.
- **Fonts:** `Segoe UI Variable Text, Segoe UI` for text. `Segoe Fluent Icons, Segoe MDL2 Assets`
  for glyphs, so Windows 10 falls back to its own icon font. No new image files.
- **Airspace:** WebView2 is a native window, and WPF can't draw on top of it. Nothing may overlap
  the page area: dividers and anything else get their own rows.
- **Out of scope:** dark mode, a .NET upgrade, the tray menu, the confirmation page's HTML,
  replacing engine icons.

## Tech Stack

.NET 8 WPF, WebView2 1.0.2210.55, xUnit 2.9. No new packages.

## Commands

```
Build:  dotnet build Translator.sln -c Debug
Test:   dotnet test Translator.sln -c Debug --no-build
Run:    dotnet run --project Translator/Translator.csproj -c Debug
```

Quit any installed copy of Translator first. It would otherwise take the <kbd>Ctrl</kbd>+<kbd>Space</kbd>
hotkey.

## Project Structure

```
Translator/Styles/Popup.xaml     → new: colours, brushes and styles for the pop-up, merged into
                                   MainWindow.Resources
Translator/MainWindow.xaml(.cs)  → layout and WebView2 handling
Translator/MainWindowViewModel.cs → view models, including NavigationButtonViewModel
Translator/Properties/Resources*.resx, Resources.Designer.cs → strings (Designer.cs by hand; only
                                   Visual Studio regenerates it)
Translator.Tests/                → xUnit tests
```

## Code Style

Match the surrounding code. XAML comments explain *why*. Resource keys are PascalCase and end in
their type: `AccentBrush`, `SearchBoxStyle`, `IconButtonStyle`. View-model changes follow the
existing `SetField` pattern:

```csharp
public bool IsActive
{
    get => _isActive;
    set => SetField(ref _isActive, value);
}
```

## Boundaries

- **Always:** build Debug (warnings as errors) and run the tests before each commit; keep each
  change inside its module's files where possible.
- **Ask first:** new packages; target framework changes; anything that drives the user's desktop
  (keystrokes, clipboard); pushing or opening the PR.
- **Never:** `AllowsTransparency="True"` (see ADR 0004); anything that overlaps the WebView2; push
  before the user has checked everything by hand.

## Delivery

When the user has checked both modules by hand, turn the specs into
`docs/adr/0005-popup-style.md`, delete the three `SPEC*.md` files, and open one PR after the user
agrees.
