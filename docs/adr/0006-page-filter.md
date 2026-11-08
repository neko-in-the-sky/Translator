# 6. Engines hide parts of their pages with CSS selectors, applied before the first paint

- **Status:** Accepted
- **Date:** 2026-11-08

## Context

Three scripts in `Translator/js/` cleaned up Oxford, Free Dictionary and SpanishDict by calling
`document.getElementById(...).remove()`, once, at `DOMContentLoaded`. A check of the live sites on
2026-11-08 found all three broken:

| Script | Gone from the site | Effect |
|---|---|---|
| `oxford.js` | `topslot_container` | Threw on line 3. The ad slot was now `#ad_topslot`, which the site's CSS gives `min-height: 250px`, so a blank 250 px band sat above every entry even though the blocklist stopped the ad |
| `free_dictionary.js` | `ep1` | Threw on line 2 |
| `spanishdict.js` | `sidebar-container-video` | Did nothing |

Nobody noticed, because script errors weren't logged. The approach had three faults: one missing
element threw and skipped every line after it; the full page was drawn before the script ran; and
deleting nodes that a site's framework manages, such as SpanishDict's React app, can upset its
scripts. The other engines had no cleanup at all.

## Decision

1. **Each engine lists CSS selectors to hide.** `JsFileName` is replaced by an optional `Hide`
   list on each engine in `appsettings.json`. Engines stay in `ApplicationSettings.SearchEngines`,
   which users can't override (ADR 0001), so the selectors ship and update with each release.

2. **One script, registered before any page loads.** `Translator/js/page_filter.js` replaces the
   site scripts. `PageFilterScript` fills in the selectors as JSON, keyed by each engine's domain
   (by its host since ADR 0007). `MainWindow` registers it
   once with `AddScriptToExecuteOnDocumentCreatedAsync`, and no navigation starts until that's
   done. In the top frame of a matching host, the script adds one `display: none !important` rule
   per selector through `document.adoptedStyleSheets`, which works before `<head>` exists. So the
   hidden parts are never drawn, parts the site adds later are hidden too, and nothing is removed
   from the page.

3. **A selector that stops working is reported, and nothing else breaks.** Each selector is
   checked with `querySelector` first: an invalid one is skipped and reported. At the `load` event,
   every selector that matches nothing is reported. Reports go through
   `chrome.webview.postMessage` to `PageFilterReports`, which logs each engine, selector and reason
   once per run as a warning. Any page could post such a message, so messages of any other shape
   are ignored and fields are truncated.

4. **Selectors follow three rules**, learnt from the live pages:
   - They target the site's own page, not elements that ad and consent scripts would add.
     Translator's blocklist stops most of those scripts, so such a selector would match nothing and
     log a warning on every run.
   - They avoid generated class names (SpanishDict's `FMkT7lnD`, Google's `zQTmif`) in favour of
     IDs, structure, `data-testid` attributes, ARIA roles and library class names.
   - A `:has()` selector says exactly where its anchor sits, with `>`, so it can't also match a
     container of the content. `div:has(> #translation)` once hid all of Multitran's translations,
     because in its narrow layout the search form sits directly in the column that holds them.

The selectors and the reasons for each are in the spec (see below). Multitran needs a note: it
stores the window width in a cookie and lays out the next page for it, so its layout classes change
with the width, and its selectors were checked in all three layouts.

## Alternatives rejected

- **Fixing the scripts.** They'd still remove elements late, after the page was drawn, stop at the
  first missing element, and fail silently.
- **Naming what to keep instead of what to hide.** Each engine would name its entry, and
  everything else would be hidden. It was built and rejected: a page without the kept part, such as
  a "did you mean" page, needed a fallback, and the result was judged worse than hiding the known
  clutter.
- **Extracting the entries and drawing Translator's own page.** SpanishDict (JSON in the page),
  Wiktionary (a REST API), Urban Dictionary (a JSON endpoint) and Oxford (semantic markup) would
  allow it, but Free Dictionary, Multitran and DeepL wouldn't. It fails harder when a site changes,
  loses coverage (idioms, "did you mean" pages, links), and raises the sites' terms of use. It was
  put off.

## Consequences

- When a site changes, the log names the engine and the selector that stopped matching.
- A page that legitimately lacks a part, such as an ad slot on a "did you mean" page, logs one
  `no-match` warning per run although nothing is broken.
- Urban Dictionary's consent dialog comes from `consentmanager.net`, which isn't blocked, and it
  stops the page scrolling while open, so it can't be hidden with CSS. Blocking that host in
  `Blocklist/my.txt` is the way to remove it.
- Multitran's very first visit has no width cookie yet, so it's laid out for a wide window, with
  narrow translations and the logo.
- The blank page shown while a page loads (ADR 0005's "no-flash") is unchanged.

The original spec is in the history of the `page-filter` branch (`SPEC.md`, from commit `c55ebcd`).
