# Spec: Rounded corners and a real shadow for the pop-up

## Objective

The pop-up is a square, borderless window with a 1 px black border (`MainWindow.xaml`). On
Windows 11 it looks out of place next to the system's own flyouts, which have rounded corners, a
thin system border and a soft shadow. Over a white page, like the Wikipedia article in
`docs/main_window.jpg`, the black line is the only thing separating the pop-up from what's behind
it.

On Windows 11 the pop-up gets rounded corners, the system border and a shadow, all drawn by the
Desktop Window Manager (DWM). Windows 10 keeps today's look.

**Users:** anyone who opens the pop-up with <kbd>Ctrl</kbd>+<kbd>Space</kbd> or the tray menu.

**Acceptance criteria**

1. On Windows 11 the pop-up has rounded corners. The WebView2 page inside is clipped to them too,
   so no square page corner shows through.
2. On Windows 11 the pop-up casts the DWM shadow on all four sides, and the black 1 px border is
   gone. The DWM's own thin border replaces it.
3. On Windows 10, where DWM can't round corners, the pop-up keeps square corners and the black
   1 px border. A shadow is welcome there but not required.
4. The pop-up still can't be resized by dragging its edges, can't be moved by dragging, and shows
   no title bar, caption buttons or white strip at any edge.
5. Nothing else changes: size and position next to the cursor, <kbd>Esc</kbd> and click-away to
   hide, <kbd>Enter</kbd> in the search box, the engine buttons, the tray icon and its menu
   position, the confirmation page, and page loading.
6. The Debug build, which treats warnings as errors, and all existing tests pass.

## Tech Stack

- .NET 8 WPF, `net8.0-windows10.0.17763.0`, unchanged.
- `System.Windows.Shell.WindowChrome`, which is built into WPF.
- `DwmSetWindowAttribute` from `dwmapi.dll`, called through P/Invoke with
  `DWMWA_WINDOW_CORNER_PREFERENCE` (33) = `DWMWCP_ROUND` (2). Windows 11 (build 22000) added that
  attribute. On Windows 10 the call fails with `E_INVALIDARG`, which is how the app tells the two
  apart. There's no OS version check.
- No new NuGet packages, no .NET upgrade and no settings changes.

## Design

### Why WindowChrome, and not `AllowsTransparency`

The usual WPF trick for custom corners and shadows is `AllowsTransparency="True"` plus a
`DropShadowEffect`. It can't be used here. WebView2 is a native child window (HWND), and a layered
transparent WPF window doesn't render child HWNDs, so the page would disappear. Everything has to
come from the DWM instead, which composes the whole top-level window, WebView2 included.

### Shadow

The DWM draws a shadow only when the window's frame is extended into its client area. A
`WindowChrome` with `GlassFrameThickness="1"` does that. `CaptionHeight="0"` means no area acts as
a title bar, so the window can't be dragged by it. `ResizeBorderThickness="0"` means no edge acts
as a resize handle.

The DWM may also require a thick frame (`WS_THICKFRAME`) before it draws a shadow. WPF sets that
style only when `ResizeMode` allows resizing. If the shadow is missing with `ResizeMode="NoResize"`,
switch to `ResizeMode="CanResize"`. The zero-width resize border still stops edge dragging, so
criterion 4 holds. The trade-off, which has been accepted: while the pop-up is open, Windows'
keyboard shortcuts like <kbd>Win</kbd>+<kbd>↑</kbd> could still maximize or snap it.

### Corners and border

```
MainWindow ctor
  SourceInitialized (the window's handle exists, the window isn't shown yet)
    WindowCorners.TryRound(this)
      true  → WindowBorder.BorderThickness = 0   (Windows 11: the DWM draws a border)
      false → leave the 1 px black border        (Windows 10)
```

### Files

```
Translator/WindowCorners.cs       → new: P/Invoke wrapper, TryRound(Window) → bool
Translator/MainWindow.xaml        → WindowChrome; the Border gets x:Name="WindowBorder";
                                    ResizeMode may change (see Shadow)
Translator/MainWindow.xaml.cs     → SourceInitialized handler above
docs/adr/0004-rounded-corners.md  → written before the PR, and this SPEC.md is deleted
```

## Commands

```
Build:  dotnet build Translator.sln -c Debug
Test:   dotnet test Translator.sln -c Debug --no-build
Run:    Translator\bin\Debug\net8.0-windows10.0.17763.0\Translator.exe
```

Quit any installed copy of Translator from its tray menu before running the Debug build. The
installed copy registers the <kbd>Ctrl</kbd>+<kbd>Space</kbd> hotkey first, and the Debug build
would lose it.

## Code Style

Follow the existing P/Invoke classes (`HotkeyManager`, `PopupSizeLocationProvider`): `DllImport`,
a constant per Win32 value, and an XML doc comment that links the Microsoft Learn page.

```csharp
/// <summary>
/// Sets the value of a Desktop Window Manager attribute for a window.
/// See https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmsetwindowattribute.
/// </summary>
[DllImport("dwmapi.dll")]
private static extern int DwmSetWindowAttribute(
    [In] IntPtr hwnd,
    [In] int dwAttribute,
    [In] ref int pvAttribute,
    [In] int cbAttribute);
```

- XAML comments explain *why* a setting is there, for example why the glass frame is extended.
- No implicit usings; add `using System;` explicitly.

## Testing Strategy

- **Automated:** nothing new. Corner rounding and shadows are drawn by the DWM on a real desktop,
  and the xUnit project has no UI tests, so a test would only check that a P/Invoke call returns.
  The existing tests must still pass.
- **By hand, on Windows 11, done by the user.** Show the pop-up over a white page and a dark one,
  both with <kbd>Ctrl</kbd>+<kbd>Space</kbd> and with **Translate** in the tray menu, and check:
  - [ ] Corners are rounded, and the page is clipped at the bottom corners (criterion 1)
  - [ ] Shadow on all four sides; no black border (2)
  - [ ] Edges can't be dragged to resize, and the window can't be dragged to move it (4)
  - [ ] No title bar, white strip or caption buttons at any edge (4)
  - [ ] <kbd>Esc</kbd>, click-away, <kbd>Enter</kbd>, an engine button, the tray menu position (5)
  - [ ] A pop-up at a screen edge, e.g. with the cursor near the bottom right, still fits
- **Windows 10:** not tested by hand. The only path there is `TryRound` returning `false`, which
  leaves the XAML exactly as it was apart from the added `WindowChrome`.

## Boundaries

- **Always:** build Debug (warnings as errors) and run the tests before each commit; keep the
  change to the files listed above.
- **Ask first:** new packages; changing the target framework; anything that simulates keystrokes,
  changes the clipboard or otherwise drives the user's desktop; pushing or opening the PR.
- **Never:** `AllowsTransparency="True"`; push before the user has checked the pop-up by hand;
  restyle other parts of the pop-up in this change.

## Success Criteria

- Every acceptance criterion above is ticked in the user's Windows 11 check.
- The Debug and Release builds and all tests pass locally and in CI.
- `docs/adr/0004-rounded-corners.md` records the WindowChrome/DWM decision, the Windows 10
  fallback and the `ResizeMode` trade-off, if it was needed. `SPEC.md` is removed in the same PR.

## Open Questions

- Whether `ResizeMode="NoResize"` is enough for the shadow. This is only settled when the user
  checks the pop-up by hand; the design above covers both answers.
