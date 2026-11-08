# Spec: hide page parts with CSS, and report rules that stop matching

## Objective

Three per-site scripts in `Translator/js/` clean up pages by calling
`document.getElementById(...).remove()`. `MainWindow.InitializeWebView2` runs them once, at
`DOMContentLoaded`. A check of the live sites in Chrome on 2026-11-08 found all three broken:

| Script | IDs | Gone from the site | Effect |
|---|---|---|---|
| `oxford.js` | `searchbar`, `ox-header`, `topslot_container` | `topslot_container` | Throws on line 3. The ad slot is now `#ad_topslot`, which the site's CSS gives `min-height: 250px`, so a blank 250 px band sits above every entry even though the blocklist stops the ad |
| `free_dictionary.js` | `header`, `ep1` | `ep1` | Throws on line 2 |
| `spanishdict.js` | `sidebar-container-video` | it | Does nothing |

Nobody noticed, because script errors aren't logged. The approach has three faults:

- One missing element throws and skips every line after it.
- The full page is drawn before the script runs.
- Deleting nodes that a site's framework manages, such as SpanishDict's React app, can upset its
  scripts.

This change replaces the scripts with **CSS selectors that hide** what isn't wanted. Each
engine lists its selectors in `appsettings.json`, and they're applied before the first paint. A
selector that matches nothing, or isn't valid, is logged as a warning and doesn't affect the
other selectors.

**Acceptance criteria**

1. **Oxford:** the site header, search bar, footer and all ad slots are hidden. There's no blank
   band above the entry and no gaps between its senses.
2. **Free Dictionary:** the site header, the ad slots and banner strips, and the floating share
   bar are hidden.
3. **SpanishDict:** the top navigation, the site's search box, the Premium sidebar, the ad slots,
   the video ad beside the headword and the sign-up pop-up are hidden.
4. **Multitran:** the top links and menu, the search form, the Google and Forvo links,
   the phrases-by-subject list and the footer links are hidden.
5. **DeepL:** the site header, the translation-mode tabs, the marketing sections below the
   translator and the footer are hidden. The dictionary panel still shows.
6. **Urban Dictionary:** the top ad band, the site header and category bar, the merchandise box,
   the ad panels between definitions, the "Get the … mug" links and the footer are hidden.
7. **Wiktionary German:** the site header (including the one that sticks on scrolling), the page
   toolbar, the languages button, the category list and the footer are hidden.
8. None of these is ever visible, not even while the page loads.
9. A selector that matches nothing by the page's `load` event doesn't stop the other selectors. It
   logs one warning naming the engine and the selector. The warning is logged at most once per
   engine and selector each time the app runs.
10. If a selector isn't valid CSS, the same warning is logged with the reason `invalid-selector`,
    and the other selectors still apply.
11. Pages from other sites, the confirmation page and `about:blank` are unchanged.
12. The site's own scripts keep working, for example the pronunciation buttons on Oxford and
    SpanishDict, and typing a new text into DeepL.
13. `Translator/js/oxford.js`, `free_dictionary.js`, `spanishdict.js`, `JavaScriptProvider` and
   `SearchEngine.JsFileName` are gone.

## Design

### Configuration

`JsFileName` is replaced by an optional `Hide` list of CSS selectors on each engine in
`appsettings.json`:

```json
{
  "Name": "Oxford",
  "UrlTemplate": "https://www.oxfordlearnersdictionaries.com/search/english/?q={0}",
  "IconFileName": "oxford.ico",
  "Hide": [ "#ox-header", "#searchbar", "#ox-footer", "[id^=\"ad_\"]" ],
  "AutoSearchRegex": "^[a-zA-Z]+$"
}
```

Engines stay in `ApplicationSettings.SearchEngines`, which users can't override (ADR 0001). So the
selectors ship and update with each release, like the scripts did.

### Selectors

Checked on 2026-11-08: the first three in Chrome at desktop width, all seven in the app's built-in
browser at 768×500.

Selectors only target parts of the site's own page. Translator's blocklist stops most ad and
consent scripts, so the elements those scripts would add never appear in the app. A selector
for one of them would match nothing and log a warning on every run.

A `:has()` selector says exactly where its anchor sits, with `>`, so it can't also match a
container of the content. `div:has(> #translation)` once hid all of Multitran's translations,
because in its narrow layout the search form sits directly in the column that holds them.

| Engine | `Hide` | Notes |
|---|---|---|
| Oxford | `#ox-header`, `#searchbar`, `#ox-footer`, `[id^="ad_"]`, `#ring-links-box`, `.definition-title`, `.wotd-box.opal_written` | All 13 ad slots have IDs starting `ad_`, including `#ad_topslot` and the five `#ad_contentslot_N` slots (280 px each) inside the entry. They also share the class `am-entry_long`. The last three were added during the by-hand check |
| Free Dictionary | `#header`, `[id^="yk"]`, `.TopBannerAd`, `.afs_ads`, `#fixedBlock` | The ad slots are `#yk1`, `#yk5` and `#yk6`. They can't be hidden by their class `.yk`, because the content container `#content` has it too |
| SpanishDict | `#root header`, `#sd-search-wrapper`, `#main-container-flex > :not(#main-container-video)`, `[id^="spdAd"]`, `div:has(> #sdICVPrimisVideoAdContainer)`, `.ReactModalPortal` | Class names are generated codes such as `FMkT7lnD` that change when the site is rebuilt, so only IDs, structure and library class names are used. The navigation is the page's only `header`. The ad slots are `#spdAdTopLarge-container` and others. The video ad's own container collapses, but its parent keeps a 320×180 grey box, hence `:has()`. `.ReactModalPortal` is the react-modal library's class; it hides the "Unlock the Best of SpanishDictionary.com" sign-up pop-up, and with it any pop-up the site opens |
| Multitran | `.container > div:has(> .offset1)`, `.container > div:has(> .menu1)`, `#translation`, `div:has(> .small > a[title="Google"])`, `table:has(a[name="phrases"])`, `table:has(td.phras)`, `div.grayline` | The site stores the window width in a cookie and lays out the next page for it, so its layout classes change with the width. The pop-up gets the narrow layout (`left_col_mobile`); wider windows get `mclass160_10` or `mclass160_240`. The selectors anchor on what's inside instead: the `offset1` and `menu1` links, the search form, the Google link. They were checked in all three layouts, with every translation cell still visible. The narrow layout has no logo, so the logo isn't hidden. `.grayline` is also on the phrases heading, which is hidden anyway |
| DeepL | `[data-testid="dl-header"]`, `nav:has([data-testid="doctrans-tabs-switch-text"])`, `[data-testid="translator"] > :has([data-testid^="he-platform"])`, `[data-testid="dl-footer"]` | DeepL marks its parts with `data-testid` attributes, which are made for its own tests and tend to stay stable. The marketing sections ("Trusted by over 200,000 businesses" and on, about 6,900 px) are the translator's child that holds the `he-platform-*` sections. Its cookie notice comes from `usercentrics.eu`, which is blocked |
| Urban Dictionary | `#ud-root > div:has(> div > [class~="leaderboard:block"])`, `.udh`, `#definitions-feed > section`, `.ad-panel`, `.definition div:has(> a[href*="/go/store/"])`, `footer.udf` | The top ad band reserves 114 px. `[class~=…]` matches the Tailwind class `leaderboard:block` without escaping its colon. The merchandise box is the feed's only `section` |
| Wiktionary German | `.vector-header-container`, `.vector-sticky-header`, `.vector-page-toolbar`, `#p-lang-btn`, `#catlinks`, `.mw-footer-container`, `.interproject-box` | Classes of MediaWiki's Vector 2022 skin. `.interproject-box`, the links to other Wikimedia projects, was added during the by-hand check. The other languages' sections stay: they're flat siblings of the German one with no container to hide, and the URL's `#German` scrolls to it |

SpanishDict and DeepL were later replaced by Wiktionary Spanish and Google Translate, and
Wiktionary English was added. `SPEC-engines.md` has their selectors and why filters became keyed
by host instead of by domain.

### The script

One static file, `Translator/js/page_filter.js`, replaces the three site scripts. Its code sits
inside a function, so none of its names can clash with names the page declares. At startup a new
`PageFilterScript` class reads it and replaces the placeholder `/* filters */ {}` with the
selectors as JSON. They're keyed by each engine's host without a leading `www.`
(`WebHelpers.GetHost`), and engines on the same host share one filter, with their selectors
merged and their names joined. They're serialised with `System.Text.Json`, so quotes in
selectors stay valid:

```js
const filters = {"oxfordlearnersdictionaries.com": {"engine": "Oxford", "hide": ["#ox-header", ...]}, ...};
```

`MainWindow` registers the result once with `CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync`.
This runs the script in every new document before the page's own scripts. The registration must
finish before the first navigation, so `NavigateToUrl` awaits it.

In the page, the script:

1. Returns at once unless it's in the top frame and `location.hostname` is a filter's host or a
   subdomain of it.
2. Checks each selector with `document.createDocumentFragment().querySelector(selector)`, which
   throws on invalid CSS. An invalid selector is reported and skipped.
3. Builds one rule per valid selector, `<selector> { display: none !important; }`. It adds them as
   a `CSSStyleSheet` through `document.adoptedStyleSheets`, which works before `<head>` exists. So
   the hidden parts are never drawn, and elements the site adds later are hidden too. Nothing is
   removed from the page, so the site's scripts still find their nodes.
4. At the `load` event, reports every valid selector that `document.querySelector` doesn't match.

Reports go through `window.chrome.webview.postMessage`:

```js
{ type: "page-filter", engine: "Oxford", selector: "#ox-header", reason: "no-match" }
```

The `reason` is `no-match` or `invalid-selector`. `MainWindow` handles `WebMessageReceived` and
passes the message to a new `PageFilterReports` class, which logs a warning with the engine, the selector, the reason and the page URL. It keeps a set of
(engine, selector, reason) triples already logged, so each is logged once per run. Any page could
send such a message, so the handler ignores messages of any other shape and truncates the
fields.

### Removed

These are removed:

- `JavaScriptProvider` and its registration in `App.xaml.cs`
- the `DOMContentLoaded` handler that ran the site scripts
- `SearchEngine.JsFileName`
- the three `js/*.js` files

The files are also listed in `Translator.csproj`, `build/Build-Release.ps1` (its `$required`
list) and `build/Test-Installer.ps1` (its file check). Those three list `js/page_filter.js`
instead.

## Files

New:

- `Translator/js/page_filter.js`
- `Translator/PageFilterScript.cs`, which replaces `JavaScriptProvider.cs`
- `Translator/PageFilterReports.cs`
- `Translator.Tests/PageFilterScriptTests.cs`
- `Translator.Tests/PageFilterReportsTests.cs`

Changed:

- `Translator/Configuration/SearchEngine.cs`
- `Translator/appsettings.json`
- `Translator/MainWindow.xaml.cs`
- `Translator/App.xaml.cs`
- `Translator/Translator.csproj`
- `build/Build-Release.ps1`
- `build/Test-Installer.ps1`
- `README.md` (step 5 of "How it works")

## Testing

- **Automated (xUnit, `PageFilterScriptTests`):**
  - Engines with a `Hide` list appear in the JSON under their host. Engines without one, or with
    an empty one, don't.
  - Selectors containing quotes and backslashes survive as valid JSON inside the script.
  - The shipped `page_filter.js` has the placeholder exactly once.
- **Automated (xUnit, `PageFilterReportsTests`):** a report logs one warning, a repeated report
  logs nothing more, other messages are ignored, and long fields are truncated.
- The existing tests pass.
- **Live pages (in the app's built-in browser, 2026-11-08, at 768×500):**
  - The script, with the shipped selectors, was run on Oxford "house", Free Dictionary "house"
    and SpanishDict "house" and "casa". No selector reported `no-match`, and the hidden parts
    were gone.
  - The other four lists were applied the same way, as a style sheet, on Multitran "house",
    DeepL "house", Urban Dictionary "house" and Wiktionary "Haus". Every selector matched, and the
    entry or translator was at the top. The pages shrank from 9,639 to 5,945 px (Multitran),
    10,418 to 1,026 px (DeepL), 6,073 to 3,297 px (Urban Dictionary) and 15,232 to 12,352 px
    (Wiktionary).
  - A narrower viewport made the browser send a phone user agent, and SpanishDict then served its
    mobile site, so 650 px wasn't tested. Translator's WebView2 sends a desktop user agent.
- **By hand (user, Windows 11, in the pop-up at its normal size):**
  - [ ] Oxford "house": no site header, search bar or footer, no blank band above the entry, and
        no gaps between senses (1, 8)
  - [ ] Free Dictionary "house": no header, ad slots, banner strips or share bar (2, 8)
  - [ ] SpanishDict "house" and "casa": no top navigation, search box, Premium sidebar, ad slot,
        grey video box or sign-up pop-up (3, 8)
  - [ ] Multitran "house", searched twice: the translation table near the top, with no menu,
        search form, phrase list or footer links (4, 8)
  - [ ] DeepL "house": the source and translation boxes at the top and the dictionary panel below,
        with no header, tabs, marketing or footer. Typing a new word translates it (5, 8, 12)
  - [ ] Urban Dictionary "house": definitions only, with no ad band, header, merchandise, ad panels,
        mug links or footer (6, 8)
  - [ ] Wiktionary German "Haus": no header, toolbar, languages button, categories or footer, and
        the page opens at the German section (7, 8)
  - [ ] No engine shows a hidden part first, even briefly (8)
  - [ ] Change one Oxford selector to `#does-not-exist` in the built `appsettings.json`: the
        other parts are still hidden, and the log has one warning for it, even after two
        searches (9)
  - [ ] Change one to `#[`: one `invalid-selector` warning, and the other parts are still
        hidden (10)
  - [ ] A sentence copied to the clipboard: the confirmation page looks as before (11)
  - [ ] The pronunciation buttons work on Oxford and SpanishDict (12)

## Boundaries

- Work goes on a new branch, `page-filter`, from `main`, as one PR. Push and open the PR only
  after the by-hand check.
- When merged, this spec becomes ADR 0006, as with ADR 0005.
- **Out of scope:**
  - showing only the entry (the keep-list design)
  - request blocking and the blocklists
  - cookie consent pop-ups (most of their hosts are already blocked; see open question 4)
  - hiding the other languages' sections on Wiktionary
  - a scheduled CI check of the selectors
  - the blank page shown while loading (ADR 0005)

## Open questions

1. **Layout at the pop-up's width.** At 768 px, Oxford's empty left column
   (`.responsive_entry_left`) still takes up space, and its right column
   (`.responsive_entry_center_right`: "Other results", "Nearby words") sits beside the entry,
   narrowing it. Adding both to `Hide` would give the entry the full width, at the cost of those
   links. SpanishDict's desktop layout is 1024 px wide, so it scrolls sideways in the pop-up, as it
   did before this change. The by-hand check at 650 px will show what's worth changing.
2. **Warnings for pages that aren't entries.** An Oxford "did you mean" page, or a page reached by
   a link, may lack some parts, such as the ad slots, so their selectors report `no-match` when
   nothing is broken. Logging once per run keeps this to one line. Is that acceptable, or should
   only the first page after a search be checked?
3. **SpanishDict's video ad** may not load on every page, in which case
   `div:has(> #sdICVPrimisVideoAdContainer)` reports `no-match` once per run, as in question 2.
4. **Urban Dictionary's consent dialog** comes from `consentmanager.net`, which isn't blocked, so it
   probably shows in the app until it's answered there. It also stops the page scrolling while it's
   open (`body { overflow: hidden }`), so hiding it with CSS would leave a page that can't scroll.
   Adding `consentmanager.net` to `Blocklist/my.txt` would stop it loading, as with the other
   sites' consent tools, but the blocklists are out of scope here.
5. **Multitran's first visit.** Until the site has the width cookie, it lays the page out for a
   wide window, with ad columns 160 px and 240 px wide on either side, leaving the translations
   about 320 px wide, and the logo shows. From the second search on, it uses the pop-up's width.
   The selectors match every layout.
