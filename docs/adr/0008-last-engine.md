# 8. The pop-up reopens with the last engine used, and every engine can search at once

- **Status:** Accepted
- **Date:** 2026-11-08

## Context

The hotkey, <kbd>Enter</kbd> and the magnifier button always searched with `DefaultSearchEngine`
from the settings, whichever engine the user had last picked. And only Oxford had an
`AutoSearchRegex`, the pattern copied text must match for the hotkey to search at once instead of
showing the confirmation page. That page exists so that a stray clipboard full of text never turns
into a web request on its own.

## Decision

1. **The last engine clicked becomes the current one.** `MainWindowViewModel.CurrentSearchCommand`
   (formerly `DefaultSearchCommand`) is the engine the hotkey, the tray's Translate item,
   <kbd>Enter</kbd> and the magnifier button use. A click on an engine's button sets it. At startup
   it's the remembered engine, then `DefaultSearchEngine`, then the first engine.

2. **It's remembered in its own file.** `AppState` keeps it in
   `%LOCALAPPDATA%\Translator\state.json` as `{ "LastSearchEngine": "…" }`. It isn't written to
   `usersettings.json`, which is the user's own commented file and which Translator only reads
   (ADR 0001). It's app state rather than a setting, so it sits in `%LOCALAPPDATA%` with the logs,
   which the uninstaller keeps (ADR 0003). Writes go to a temporary file that then replaces the
   real one. A missing, unreadable or malformed file means nothing is remembered; a failed read or
   write logs a warning and changes nothing else.

3. **`DefaultSearchEngine` keeps its name.** It now applies on the first run and when the remembered
   engine no longer exists. Renaming it would break existing settings files.

4. **Every engine has its own `AutoSearchRegex`**, and the current engine's decides:

   | Engine | Pattern | Searches at once for |
   |---|---|---|
   | Oxford | `^[a-zA-Z]+$` | one English word |
   | Free Dictionary, Multitran, Urban Dictionary, Wiktionary English | `^[a-zA-Z]+(?:[-'’ ][a-zA-Z]+){0,3}$` | an English word or a phrase of up to four words, with straight or curly apostrophes |
   | Wiktionary Spanish, Wiktionary German | `^\p{L}+(?:[-'’ ]\p{L}+){0,3}$` | the same, with any letters |
   | Google Translate | `^(?!.*(?:[\p{L}\p{M}]\p{P}*\p{N}\|\p{N}\p{P}*\p{L}))[\p{L}\p{M}\p{N}\p{P} -[#%&*/\\@_\[\]{}]]{1,200}$` | one line of up to 200 characters that looks like language |

   Google Translate's pattern keeps copied passwords, links and addresses out with two rules:
   only letters, digits, spaces and ordinary punctuation (not `# % & * / \ @ _ [ ] { }`, and no
   symbols such as `$`), and no letter touching a digit, even through punctuation. It was checked
   with .NET's regex engine, and `AutoSearchRegexTests` holds the examples.

## Alternatives rejected

- **Remembering the engine only until Translator closes.** It should survive a restart.
- **Leaving the patterns as they were.** With any engine but Oxford remembered, every hotkey press
  would have shown the confirmation page.
- **Any single line up to 200 characters for Google Translate** (`^[^\r\n]{1,200}$`). A copied
  password, link or email address would have gone to Google on a hotkey press.
- **A token-by-token pattern for Google Translate.** It gave the same results but was about 600
  characters long, too hard to read in `appsettings.json`.

## Consequences

- A password made only of letters, such as "sunshine" or "correct horse battery staple", still
  passes Google Translate's pattern. No pattern can tell those from words.
- Some ordinary text gets the confirmation page with Google Translate: "COVID-19", prices with `$`,
  and percentages.
- The word patterns allow one separator between words, so "rock ’n’ roll" gets the confirmation
  page.

The original spec is in the history of the `last-engine` branch (`SPEC-last-engine.md`, from commit
`f137751`).
