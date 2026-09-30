# 5. The pop-up's toolbar follows Windows 11's light theme, from one resource file

- **Status:** Accepted
- **Date:** 2026-09-28

## Context

The pop-up's toolbar used stock WPF controls: a 25 px square `TextBox` with a grey 3D-era border
and no hint of its purpose, and 24×24 `ToolBar` buttons with favicons stretched to fill them. Its
margins were uneven (`0,10,20,0` inside a 5 px frame), and the page floated inside that frame.
Nothing showed which engine's page was on screen.

ADR 0004 gave the window Windows 11's rounded corners and shadow, and the toolbar looked dated
inside it. WebView2 is a native child window, so WPF can't draw over the page.

## Decision

1. **One resource file.** `Translator/Styles/Popup.xaml`, merged into `MainWindow.Resources`,
   holds every colour, both font families and the styles (`SearchBoxStyle`, `IconButtonStyle`,
   `EngineButtonStyle`, `FocusVisualStyle`). The colours are Windows 11's light theme, flattened
   onto white. There's only a light theme, but a dark one has only this file's colours to swap.

2. **The accent colour comes from Windows.** `AccentColor.TryGet()` reads
   `UISettings.GetColorValue(UIColorType.AccentDark1)`. That's the shade Windows 11's light theme
   uses for controls, and the target framework already includes the API. `MainWindow` stores it as
   `AccentBrush` in its own resources, which override `Popup.xaml`'s `#005FB8` fallback. So
   `AccentBrush` is always a `DynamicResource`. It's read once, when the window is created.

3. **Fonts built into Windows.** Text uses `Segoe UI Variable Text, Segoe UI` and glyphs use
   `Segoe Fluent Icons, Segoe MDL2 Assets`. Windows 10 falls back to the second of each, which has
   the same glyphs at the same code points. There are no new image files or packages.

4. **The search box works like Windows 11's.** It's 32 px tall with corner radius 4, a darker
   bottom edge that becomes a 2 px accent line with keyboard focus, and a placeholder (*"Word or
   phrase"* / *"Слово или выражение"*, `QueryTextBox_Placeholder`). It has a clear button, shown
   when there's text, and a magnifier that searches with the default engine like
   <kbd>Enter</kbd>. The placeholder reaches the template through `Tag`, so the style doesn't
   depend on the app's strings. Neither button can take focus, so clicking one leaves the cursor
   in the box with no code-behind. The clear button runs `MainWindowViewModel.ClearQueryCommand`.

5. **The toolbar is evenly spaced, and the page fills the window.** The toolbar has 12 px padding
   (10 px at the bottom) and a 1 px divider, with the page edge to edge below it.

6. **Engine buttons are grouped, and the one showing is highlighted.** The buttons are 32×32 with
   20 px icons, in one rounded strip. `NavigationButtonViewModel.IsActive` marks the engine whose
   page is on screen: the last one to search, by click, <kbd>Enter</kbd>, the magnifier or the
   hotkey. The confirmation page clears it. That button gets an accent tint and a short accent
   line at the bottom, like an open app on the Windows 11 taskbar.

## Alternatives rejected

- **.NET 9's `ThemeMode="System"`.** It restyles every control, the tray menu and dark mode at
  once, but it means a .NET upgrade, and it's marked experimental (`WPF0001`, an error under the
  Debug build's warnings-as-errors).
- **A control library such as WPF-UI.** A new dependency, for a window with one text box and a
  row of buttons.
- **`AllowsTransparency="True"` for softer effects.** The WebView2 page wouldn't render
  (ADR 0004).
- **Keeping the page hidden until it's ready ("no-flash").** This was planned alongside the rest,
  to stop the blank page and the site's full layout showing before the site script cleans it up.
  Two designs were built and dropped:
  - With `WebBrowser.Visibility = Hidden` while loading, the previous entry flashed when the page
    was shown. The WPF control passes its visibility to `CoreWebView2Controller.IsVisible`. A
    hidden controller stops drawing, so its first frame on being shown is stale.
  - Parking the WebView2 below the window instead, where it keeps drawing, still left a flash too
    brief to identify.

  The site scripts in `Translator/js/` remove elements once, at `DOMContentLoaded`. They need
  improving first, for example by hiding those elements with CSS before the first paint. Both
  attempts are in the history of the `popup-style` branch (`fff02b0`, `6a18832`).

## Consequences

- The accent colour doesn't follow Windows while the app runs. A change applies on the next
  start.
- Free Dictionary and Multitran ship only a 16 px icon, so at 20 px they look slightly soft.
- <kbd>Tab</kbd> goes from the search box straight to the engine buttons. The clear and magnifier
  buttons are for the mouse only, and <kbd>Enter</kbd> already searches.
- A dark theme needs a second set of colours, and a way to switch them when Windows changes
  theme.
- `Resources.Designer.cs` was edited by hand for the new string, as before. `ResourcesTests`
  checks that it exists in both languages.

The original specs are in the history of the `popup-style` branch (`SPEC*.md`, from commit
`1af32a1`).
