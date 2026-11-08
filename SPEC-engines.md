# Spec: Wiktionary for Spanish and English, Google Translate for DeepL

## Objective

Change three of the pop-up's engines:

1. **Replace SpanishDict with Wiktionary Spanish:** `https://en.wiktionary.org/wiki/{0}#Spanish`.
2. **Replace DeepL with Google Translate**, detecting the source language and translating into
   English: `https://translate.google.com/?sl=auto&tl=en&text={0}&op=translate`.
3. **Add Wiktionary English:** `https://en.wiktionary.org/wiki/{0}#English`.

This builds on the page filter (`SPEC.md`, branch `page-filter`): each engine still names the
parts of its page to hide.

**Acceptance criteria**

1. The toolbar shows, in this order: Oxford, Free Dictionary, Multitran, Google Translate, Urban
   Dictionary, Wiktionary English, Wiktionary Spanish, Wiktionary German. SpanishDict and DeepL are
   gone.
2. **Wiktionary Spanish** "casa" opens the Spanish section of Wiktionary's "casa" page. **Wiktionary
   English** "house" opens its English section. Both hide the same parts as Wiktionary German:
   header, sticky header, page toolbar, languages button, categories, links to other Wikimedia
   projects, and footer.
3. **Google Translate** "Das Haus ist groß" shows the German detected and the English translation.
   The site header and the two navigation bars (translation types; History and Saved) are hidden.
4. **Google's consent page** (`consent.google.com`, shown before the first translation) is left
   untouched. No page filter applies there, and no warning is logged for it.
5. Each new engine has its own toolbar icon: a round Spanish flag, a round British flag, and Google
   Translate's own icon.
6. A user whose `usersettings.json` names SpanishDict or DeepL as `DefaultSearchEngine` gets the
   first engine, Oxford, with the existing warning in the log. Nothing else breaks.
7. The three Wiktionary engines share one page filter. A warning from it names all three engines.

## Design

### Engines

In `appsettings.json`, in toolbar order:

| Name | `UrlTemplate` | `Hide` |
|---|---|---|
| Google Translate | `https://translate.google.com/?sl=auto&tl=en&text={0}&op=translate` | `header[role="banner"]`, `nav` |
| Wiktionary English | `https://en.wiktionary.org/wiki/{0}#English` | as Wiktionary German |
| Wiktionary Spanish | `https://en.wiktionary.org/wiki/{0}#Spanish` | as Wiktionary German |

The query goes into the URL through `Uri.EscapeDataString`, as now, so text with spaces, `&` or
non-Latin letters stays intact.

**Google Translate**, checked in the app's built-in browser on 2026-11-08 at 768×500:

- The page is already compact, about 570 px tall.
- Its class names are minified codes such as `zQTmif`, so the selectors use the page's semantic
  markup instead. `header[role="banner"]` is the 64 px top bar. The page's only two `nav` elements
  are the translation-type tabs (Text, Images, Documents, Websites) and the History and Saved
  buttons. Neither contains the translator (`c-wiz[role="main"]`).
- A sentence is detected correctly ("German - Detected"). A single word can be misdetected:
  "Haus" came back as English, untranslated.

**Wiktionary:** the `#Spanish` and `#English` anchors exist on "casa" and "house". All seven
Wiktionary German selectors match both pages.

### Filters keyed by host, not domain

`PageFilterScript` keys filters by `WebHelpers.GetDomain`, the last two parts of the host. For
Google Translate that's `google.com`, so the filter would also apply on `consent.google.com`. There
it would hide any `header` or `nav` on the consent page and log `no-match` warnings for the rest.

Filters are keyed by the engine's host instead, without a leading `www.`:

| Engine | Key |
|---|---|
| Oxford | `oxfordlearnersdictionaries.com` |
| Google Translate | `translate.google.com` |
| Wiktionary (all three) | `en.wiktionary.org` |

The script still matches the key and its subdomains, so `www.oxfordlearnersdictionaries.com`
matches, and `consent.google.com` no longer does. The other engines' hosts all start with `www.`,
so their keys don't change.

**Engines on the same host share one filter.** The script can only tell pages apart by host, and
the three Wiktionary engines are on `en.wiktionary.org`. Their selectors are merged, without
duplicates, and the filter's `engine` name joins theirs: "Wiktionary English, Wiktionary Spanish,
Wiktionary German". At present the last engine silently replaces the earlier ones.

### Icons

Saved in `Translator/icons` and listed in `Translator.csproj` as `Resource`, like the others:

- **`spain_flag.ico` and `uk_flag.ico`:** drawn with Pillow to match `germany_flag.ico`, a round
  flag 102 px across on a transparent 128 px canvas. The Spanish flag has no coat of arms, which
  wouldn't be visible at 20 px anyway.
- **`google_translate.ico`:** Google Translate's own favicon, as the other engines use their
  sites' icons. It has to be downloaded from `https://translate.google.com/favicon.ico`. I'll
  ask before downloading it.

`spanishdict.ico` and `deepl.ico` are deleted.

### Google's consent page

On its first visit, Google redirects to `consent.google.com` ("Before you continue") until the
user accepts or rejects cookies. The app shows that page as it is, and the user answers it once
in the pop-up. The answer is a cookie in WebView2's data folder next to the exe, which is kept
across runs (ADR 0003), so it isn't asked again until that cookie expires. The
uninstaller deletes that folder, so a reinstall asks again.

## Files

- `Translator/appsettings.json`
- `Translator/PageFilterScript.cs`: keys by host, and merges engines on the same host
- `Translator/WebHelpers.cs`: a `GetHost` helper next to `GetDomain`, or replacing it if nothing
  else uses `GetDomain`
- `Translator/icons/`: `spain_flag.ico`, `uk_flag.ico` and `google_translate.ico` added;
  `spanishdict.ico` and `deepl.ico` deleted
- `Translator/Translator.csproj`
- `Translator.Tests/PageFilterScriptTests.cs`
- `SPEC.md`: the engine table and the "keyed by domain" wording, so the page filter's spec stays
  true

## Testing

- **Automated (xUnit, `PageFilterScriptTests`):**
  - A `www.` host is keyed without it, and `translate.google.com` keeps its subdomain.
  - Two engines on the same host give one filter, with merged selectors and both names.
  - The existing tests pass.
- **By hand (user, in the pop-up):**
  - [ ] The toolbar order and icons are right, with no SpanishDict or DeepL (1, 5)
  - [ ] Wiktionary Spanish "casa" and Wiktionary English "house": the right section, with the
        site's header, toolbar and footer hidden (2)
  - [ ] Google Translate, first time: the consent page shows as Google made it. Answer it (4)
  - [ ] Google Translate "Das Haus ist groß": German detected, English translation, no header or
        navigation bars (3)
  - [ ] Restart Translator and use Google Translate again: no consent page (4)
  - [ ] The log has no page-filter warning from the consent page (4)
  - [ ] With `"DefaultSearchEngine": "SpanishDict"` in `usersettings.json`, Oxford is used and one
        warning is logged (6)

## Boundaries

- A new branch, `engines`, from `page-filter`, because it needs the `Hide` lists. Its PR follows
  the page filter's. Push only after the by-hand check.
- **Out of scope:**
  - a user setting for Google Translate's target language
  - auto-searching with Google Translate (only Oxford has an `AutoSearchRegex`)
  - Wiktionary being case-sensitive: "House" at the start of a sentence opens a different page
    from "house"
  - hiding the other languages' sections on Wiktionary pages

## Open questions

1. **What SpanishDict did that Wiktionary won't.** SpanishDict translated both ways, English to
   Spanish and Spanish to English. Wiktionary Spanish only explains Spanish words, in English. For
   English to Spanish, Google Translate would need `tl=es`, and it's fixed to English. Is that
   loss acceptable?
2. **The flag for English:** British is proposed. American, or a plain "EN" badge, are the
   alternatives.
3. **Toolbar order:** Google Translate takes DeepL's place, and the three Wiktionary engines are
   grouped at the end. Would you prefer Wiktionary Spanish in SpanishDict's place instead?
