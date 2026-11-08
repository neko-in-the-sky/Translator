# 7. Wiktionary covers Spanish and English, and Google Translate replaces DeepL

- **Status:** Accepted
- **Date:** 2026-11-08

## Context

The pop-up had SpanishDict for Spanish, DeepL for translation (English to Russian) and Wiktionary
for German only. The engines were to change to Wiktionary for Spanish and English too, and Google
Translate, detecting the source language and translating into English.

## Decision

1. **The engines, in toolbar order:** Oxford, Free Dictionary, Multitran, Google Translate, Urban
   Dictionary, Wiktionary English, Wiktionary Spanish, Wiktionary German. SpanishDict and DeepL are
   removed.
   - Google Translate: `https://translate.google.com/?sl=auto&tl=en&text={0}&op=translate`.
   - Wiktionary English and Spanish: `https://en.wiktionary.org/wiki/{0}#English` and `#Spanish`,
     with the same `Hide` list as Wiktionary German.

2. **Google Translate hides `header[role="banner"]` and `nav`.** Its class names are minified codes,
   so it uses the page's semantic markup instead. Its two `nav` elements are the translation-type
   tabs and the History and Saved buttons; neither contains the translator.

3. **Page filters are keyed by host, not domain.** `PageFilterScript` keyed filters by the last two
   parts of the host. For Google Translate that's `google.com`, so the filter would also have run on
   `consent.google.com`. Filters are now keyed by the host without a leading `www.`
   (`WebHelpers.GetHost`), such as `translate.google.com`. Engines on the same host, the three
   Wiktionary ones, share one filter, with their selectors merged and their names joined in
   warnings. Before, the last engine on a host silently replaced the others.

4. **Icons.** `spain_flag.ico` and `uk_flag.ico` are round flags drawn to match `germany_flag.ico`
   (102 px across on a transparent 128 px canvas). `google_translate.ico` is Google Translate's
   own favicon, as the other engines use their sites' icons.

## Alternatives rejected

- **An American flag or an "EN" badge for Wiktionary English.** The British flag was chosen.
- **Wiktionary Spanish in SpanishDict's old place.** The Wiktionary engines are grouped at the end
  instead.

## Consequences

- English to Spanish is gone: SpanishDict translated both ways, Wiktionary Spanish only explains
  Spanish words in English, and Google Translate always translates into English.
- On its first visit, Google shows its consent page (`consent.google.com`), which the user answers
  once in the pop-up. The answer is a cookie in WebView2's data folder next to the exe, which is
  kept across runs (ADR 0003) and deleted by the uninstaller.
- Google's language detection can misread a single word: "Haus" came back as English. Sentences
  are detected reliably.
- Wiktionary's page names are case-sensitive, so "House" at the start of a sentence opens a
  different page from "house".
- A `usersettings.json` naming SpanishDict or DeepL as `DefaultSearchEngine` falls back to the first
  engine, with a warning in the log.

The original spec is in the history of the `engines` branch (`SPEC-engines.md`, from commit
`7933610`).
