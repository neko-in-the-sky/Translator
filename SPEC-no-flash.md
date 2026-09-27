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
5. The `about:blank` step never shows the page and never stops the bar.
6. Hiding with <kbd>Esc</kbd> or clicking away still clears the page, and the next pop-up never
   shows the previous entry, not even for a frame.
7. When nothing is loading, the bar's row is empty, not a grey track.

## Design

```
NavigationStarting (any URL)           → WebBrowser.Visibility = Hidden, LoadingBar.Visible
DOMContentLoaded, not about:blank      → run the site script (as today), then RevealPage()
NavigationCompleted, not about:blank   → RevealPage()      (fallback for failures)
RevealPage()                           → WebBrowser.Visibility = Visible, LoadingBar.Collapsed
```

- `Hidden`, not `Collapsed`, so the layout doesn't change. The WPF WebView2 control passes its
  visibility to `CoreWebView2Controller.IsVisible`. Navigation and scripts keep running while
  it's hidden.
- **Risk:** when the controller is made visible again, it might show its last frame, the previous
  page, before repainting. If the user's check shows that, switch to the fallback. The fallback
  keeps the WebView visible and hides the document instead: a document-created script
  (`AddScriptToExecuteOnDocumentCreatedAsync`) adds `html { visibility: hidden }`, and
  `RevealPage()` removes it with `ExecuteScriptAsync`.
- The existing `about:blank` step stays. The hidden page makes it invisible, and removing it isn't
  needed for this change.
- The loading bar is a `ProgressBar` with `IsIndeterminate="True"` and its own template, in its own
  2 px grid row. Its style is `LoadingBarStyle` in `Popup.xaml`.

## Files

`Translator/MainWindow.xaml`, `Translator/MainWindow.xaml.cs`, `Translator/Styles/Popup.xaml`.

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
