# Spec: no-flash — keep the page hidden until it's ready

Module of [SPEC.md](SPEC.md). Shared decisions, commands, style and boundaries are there.

## Objective

Two things show before a dictionary entry is ready to read:

1. **A blank white page.** Every search navigates to `about:blank` first
   (`MainWindow.OnNavigationRequested`). When the user switches engines, the current entry
   disappears and the page area stays empty until the next site paints.
2. **The site's own layout.** The per-site scripts in `Translator/js/` that remove headers, search
   bars and ad slots run at `DOMContentLoaded`. Until then the site's full layout shows, then parts
   of it vanish and the entry jumps up.

Instead, the page area stays empty (the window's background colour) while a thin loading bar runs,
and the page appears once, already cleaned up.

**Acceptance criteria**

1. From the start of a navigation until the page is shown, the page area is empty and a 2 px
   accent-coloured bar slides left to right in its own row between the toolbar and the page.
2. The page appears once its site script has run. For engines without a script, and for the
   confirmation page, it appears at `DOMContentLoaded`.
3. On Oxford, Free Dictionary and SpanishDict, which have scripts, the parts those scripts remove
   are never visible.
4. If a navigation fails or `DOMContentLoaded` never fires, the page (or WebView2's error page) is
   still shown at `NavigationCompleted`, and the bar stops. The page never stays hidden.
5. Nothing shows the page or stops the bar before the requested page is ready. In particular, the
   `about:blank` navigation when the window hides doesn't.
6. Hiding with <kbd>Esc</kbd> or clicking away still clears the page, and the next pop-up never
   shows the previous entry, not even for a frame.
7. When nothing is loading, the bar's row is empty, not a grey track.

## Design

```
NavigationStarting    → HidePage(): park the WebView2 below the window, loading bar shown
DOMContentLoaded      → run the site script (as today), then RevealPage()
NavigationCompleted   → RevealPage()      (fallback for failures)
RevealPage(id)        → only for the latest navigation (the id NavigationStarting last saw),
                        because a cancelled one can complete after its replacement starts,
                        and only while the window is visible:
                        move the WebView2 back, loading bar hidden
HideWindow()          → HidePage(), navigate to about:blank, Hide()   (as today, plus HidePage)
```

- **The `about:blank` step before each search is removed.** It was there to clear the previous
  entry, which hiding the page now does. Keeping it would need a reliable way to tell it apart
  from the confirmation page, and `NavigateToString` pages also report `about:blank` as their
  address. The `about:blank` navigation when the window hides stays. It stops the old page (and
  any sound) and frees memory. Because `RevealPage()` does nothing while the window is hidden,
  that navigation can't make the page visible again, and the next pop-up starts with it hidden.

- **The page is parked, not hidden.** `HidePage()` moves the WebView2 below the window's client
  area with a margin, keeping its size, and `RevealPage()` moves it back. Windows clips a child
  window to its parent, so the parked page can't be seen.
- **Why not `Visibility = Hidden`:** that was the first design, and the user's check showed the
  previous entry flashing. The WPF WebView2 control passes its visibility to
  `CoreWebView2Controller.IsVisible`, and a hidden controller stops drawing. When shown again, its
  first frame was the last one it drew before being hidden: the old entry. A parked WebView2 stays
  visible as far as the controller knows, so it keeps drawing the new page, and its last frame is
  current when it moves back. The page's viewport keeps its size, so nothing is laid out again.
- **Reopening the pop-up:** hiding the window also stops the controller drawing, so the frame it
  keeps is the old entry. The page is parked before the window hides, so that frame is off-screen
  when the window shows again. The controller draws the new page while parked.
- **Why not the CSS fallback:** keeping the WebView2 visible and hiding the document with injected
  `html { visibility: hidden }` fixes switching engines, but not reopening. The last frame before
  the window hides would still be the old entry, unless every hide waited a few frames for the
  hidden document to be drawn.
- **Remaining risk:** WebView2 might pause drawing while it's entirely outside the window. The
  user's check shows whether it does.
- The loading bar is a `ProgressBar` with its own template, in its own 2 px grid row. Its style is
  `LoadingBarStyle` in `Popup.xaml`. The template slides an accent segment across by animating an
  opacity mask, and the animation runs only while the bar is visible.
- This module creates `Popup.xaml` with `AccentBrush` and `LoadingBarStyle`, and
  `AccentColor.TryGet()`, which reads the Windows accent colour.

## Files

`Translator/MainWindow.xaml`, `Translator/MainWindow.xaml.cs`, `Translator/Styles/Popup.xaml` (new),
`Translator/AccentColor.cs` (new).

## Testing

- **Automated:** none. The logic is WebView2 event wiring in the window's code-behind, and a unit
  test would have to fake WebView2. The existing tests must pass.
- **By hand (user, Windows 11):**
  - [ ] <kbd>Ctrl</kbd>+<kbd>Space</kbd> on "cat" with Oxford: empty area and a sliding bar, then the
        cleaned entry appears in one step. No Oxford header, even briefly (criteria 1–3)
  - [ ] Switch to Free Dictionary, then Multitran: the same, with no white flash of the old entry (1, 2)
  - [ ] A sentence copied to the clipboard: the confirmation page appears the same way (2)
  - [ ] Wi-Fi off, then search: WebView2's error page appears and the bar stops (4)
  - [ ] Open, <kbd>Esc</kbd>, open on another word: the old entry never shows (6)
  - [ ] Nothing loading: no grey line under the toolbar (7)
