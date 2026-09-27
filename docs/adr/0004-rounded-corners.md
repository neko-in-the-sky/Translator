# 4. The pop-up's rounded corners and shadow come from the Desktop Window Manager

- **Status:** Accepted
- **Date:** 2026-09-28

## Context

The pop-up was a square, borderless window (`WindowStyle="None"`) with a 1 px black border. On
Windows 11 it looked out of place next to the system's flyouts, which have rounded corners, a thin
system border and a soft shadow. Over a white page the black line was all that separated it from
the window behind.

The pop-up hosts WebView2, and WebView2 is a native child window (HWND). WPF can't draw over it,
make it transparent or apply effects to it.

## Decision

1. **The Desktop Window Manager draws everything.** It composes the whole top-level window,
   WebView2 included, so its corners and shadow apply to the page as well.

2. **Shadow: a `WindowChrome` with a 1 px glass frame.** In `MainWindow.xaml`,
   `GlassFrameThickness="1"` extends the frame into the client area, which is what makes the DWM
   draw its shadow. `CaptionHeight="0"` and `ResizeBorderThickness="0"` mean the window can't be
   dragged or resized. `ResizeMode="NoResize"` stays; checked on Windows 11 22631, it was enough
   for the shadow.

3. **Corners: `DWMWA_WINDOW_CORNER_PREFERENCE` = `DWMWCP_ROUND`.** `WindowCorners.TryRound` calls
   `DwmSetWindowAttribute` once the window's handle exists, in `SourceInitialized`. Windows 11
   added the attribute. On Windows 10 the call fails, which is how the app tells the two apart.
   There's no OS version check.

4. **The black border is a Windows 10 fallback.** When `TryRound` succeeds, the DWM draws its own
   thin border, so `WindowBorder`'s thickness is set to 0. When it fails, the pop-up looks as it
   did before.

## Alternatives rejected

- **`AllowsTransparency="True"` with a rounded `Border` and a `DropShadowEffect`.** This is the
  usual WPF approach, but a layered transparent window doesn't render child HWNDs, so the WebView2
  page would disappear.
- **`ResizeMode="CanResize"` with a zero-width resize border.** It was planned in case the DWM
  drew a shadow only for windows with a thick frame. It wasn't needed, and it would have let
  <kbd>Win</kbd>+<kbd>↑</kbd> maximize or snap the pop-up.
- **Upgrading to .NET 9 for `ThemeMode`.** It restyles the controls, not the window frame, and
  it's a much bigger change.
- **An OS version check.** Asking the DWM and falling back on failure also covers Windows builds
  that don't support the attribute, with no version numbers to keep up to date.

## Consequences

- On Windows 11 the pop-up has rounded corners, the system border and a shadow. The page is
  clipped to the corners.
- Windows 10 behaviour was not checked by hand. Only the fallback path runs there, and it leaves
  the black border in place. Whether a shadow shows there wasn't checked.
- Never set `AllowsTransparency="True"` on `MainWindow`, and never remove the `WindowChrome`
  glass frame, or the page or the shadow disappears.
- The border colour, the corner radius and the shadow belong to Windows, so they follow the
  system theme but can't be customized.

The original spec is in the history of the `rounded-corners` branch (`SPEC.md`, from commit
`f55ae0e`).
