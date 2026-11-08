# Spec: reopen the pop-up with the last engine used

## Objective

Today the hotkey always searches with `DefaultSearchEngine` from the settings (Oxford, unless the
user changed it), whichever engine was used last. After this change, the engine the user last
picked becomes the one the hotkey, Enter in the search box and the tray's Translate item use. It's
remembered across restarts.

**Acceptance criteria**

1. Clicking an engine's button makes it the remembered engine.
2. The next time the pop-up opens, from the hotkey or the tray's Translate item, the clipboard text
   is searched with the remembered engine, and its button is highlighted.
3. Enter in the search box searches with the remembered engine.
4. The remembered engine survives a restart of Translator.
5. On the first run, or if the remembered engine no longer exists (for example SpanishDict, removed
   in an update), `DefaultSearchEngine` from the settings is used, as now.
6. Whether the hotkey searches at once or shows the confirmation page is decided by the
   remembered engine's `AutoSearchRegex` (see the design and open question 1).
7. If the remembered engine can't be read or saved, Translator still works, falls back as in 5,
   and logs a warning.

## Design

### What counts as "used"

Only a click on an engine's button changes the remembered engine. The hotkey, Enter and the tray's
Translate item search with the remembered engine, so they don't change it. The confirmation page
belongs to no engine and doesn't change it either.

### Where it's kept

In a new file, `%LOCALAPPDATA%\Translator\state.json`:

```json
{ "LastSearchEngine": "Google Translate" }
```

- **Not in `usersettings.json`:** that's the user's own commented file, and Translator only reads
  it (ADR 0001). Writing to it would rewrite the user's comments and formatting.
- **`%LOCALAPPDATA%`, not `%APPDATA%`:** this is the app's state rather than a setting, like the
  logs already in `%LOCALAPPDATA%\Translator\Logs`. The uninstaller keeps this folder (ADR 0003),
  so the engine survives a reinstall.

A new `AppState` class reads the file once at startup and writes it whenever the remembered engine
changes. Writes go to a temporary file that then replaces `state.json`, so a crash mid-write can't
leave a broken file. A missing, unreadable or malformed file counts as "nothing remembered", with a
warning in the log except when the file is simply missing.

### The view model

- `DefaultSearchCommand` becomes `CurrentSearchCommand`, settable, with `PropertyChanged`.
- At startup it's the remembered engine, otherwise `DefaultSearchEngine`, otherwise the first
  engine. The last fallback, with its warning, is today's behaviour.
- A click on an engine's button sets it and saves it.
- `TranslateFromClipboard` and Enter (`MainWindow.QueryTextBox_KeyDown`) use it.

### Auto-search

The confirmation page exists so that a stray clipboard full of text never turns into a web
request on its own (README). Today the check uses the default engine's `AutoSearchRegex`, and an
engine without one never searches automatically.

The remembered engine's own regex decides. Only Oxford has one now, so with any other engine
remembered, every hotkey press would show the confirmation page. To keep the hotkey useful, the
other engines get a regex in `appsettings.json` too:

| Engine | `AutoSearchRegex` | Matches |
|---|---|---|
| Oxford | `^[a-zA-Z]+$` | one English word, as now |
| Free Dictionary, Urban Dictionary, Wiktionary English | `^[a-zA-Z]+(?:[-'’ ][a-zA-Z]+){0,3}$` | an English word or a short phrase such as "give up", "mother-in-law" or "don’t" |
| Multitran | the same as Free Dictionary | Multitran is searched in English here (`l1=1`) |
| Wiktionary Spanish, Wiktionary German | `^\p{L}+(?:[-'’ ]\p{L}+){0,3}$` | the same, with any letters: "niño", "Straße" |
| Google Translate | `^(?!.*(?:[\p{L}\p{M}]\p{P}*\p{N}\|\p{N}\p{P}*\p{L}))[\p{L}\p{M}\p{N}\p{P} -[#%&*/\\@_\[\]{}]]{1,200}$` | text that looks like language, not a password: see below |

The word patterns accept both the straight apostrophe `'` and the curly one `’`, which web pages
and Word put in place of it, so "don’t" copied from a page still searches at once.

**Google Translate's pattern** has two rules, so a copied password, link or address doesn't go to
Google on its own:

1. **Only letters, digits, spaces and ordinary punctuation, up to 200 of them.** The character
   class is letters (`\p{L}`, with accents as `\p{M}`), digits (`\p{N}`), the space and punctuation
   (`\p{P}`), minus `# % & * / \ @ _ [ ] { }`. Symbols such as `$ + = ^ < > |` aren't punctuation
   in Unicode, so they're out as well. A line break isn't in the class, so the text must be one
   line.
2. **No letter touching a digit, even through punctuation**: the lookahead `(?!…)` rejects a letter
   followed by a digit, or a digit followed by a letter, with only punctuation between them.

Checked with .NET's regex engine:

- **Searches at once:** "Das Haus ist groß.", "Где находится вокзал?", "¿Dónde está la estación?",
  "I have 3 cats.", "It costs 1,000.50 euros", "Meet me at 10:30.", "«Bonjour», dit-il.",
  "Don’t worry, be happy!", "你好，世界。".
- **Confirmation page:** "hunter2", "Tr0ub4dor&3", "P@ssw0rd!", "Summer-2024!", "x7#kQ9$mL2",
  `https://example.com/page`, `someone@example.com`, `C:\Users\me`, "It costs $5", "50%",
  "COVID-19", and anything with a line break.
- **What it can't catch:** a password made only of letters, such as "sunshine" or "correct horse
  battery staple". No pattern can tell those from words.

Anything else, such as several lines or a long paragraph, still gets the confirmation page.

## Files

- `Translator/AppState.cs` (new)
- `Translator/MainWindowViewModel.cs`
- `Translator/MainWindow.xaml.cs` (Enter) and `Translator/Styles/Popup.xaml` (the magnifier
  button binds `DefaultSearchCommand.Command`)
- `Translator/App.xaml.cs` (registers `AppState`)
- `Translator/appsettings.json` (the regexes)
- `README.md`: "How it works", the auto-search paragraph, and the `DefaultSearchEngine` row of the
  settings table
- `Translator.Tests/AppStateTests.cs` (new), `Translator.Tests/MainWindowViewModelTests.cs`

## Testing

- **Automated (xUnit):**
  - `AppState`: a saved engine reads back. A missing file, malformed JSON or an unknown key give
    "nothing remembered" without throwing. A save into a folder that can't be written logs a
    warning and doesn't throw.
  - View model: a click makes that engine current and saves it. At startup the remembered engine
    wins over `DefaultSearchEngine`, and an engine that no longer exists falls back to it.
  - Each shipped `AutoSearchRegex` accepts and rejects the examples in the table above.
- **By hand (user, in the pop-up):**
  - [ ] Click Google Translate, close the pop-up, copy a sentence and press the hotkey: it's
        translated with Google Translate, whose button is highlighted (1, 2, 6)
  - [ ] Type a word and press Enter: Google Translate is used (3)
  - [ ] Restart Translator and press the hotkey: still Google Translate (4)
  - [ ] Set `LastSearchEngine` to "SpanishDict" in `state.json`, restart: the default engine is
        used, with a warning in the log (5)
  - [ ] Copy several lines and press the hotkey: the confirmation page (6)

## Boundaries

- A new branch, `last-engine`, from `engines`. Its PR follows the other two. Push only after the
  by-hand check.
- **Out of scope:** remembering anything else, such as the query or the pop-up's size;
  a setting to turn this off.

## Decisions

Agreed on 2026-11-08:

1. **Auto-search is per engine.** Every engine without an `AutoSearchRegex` gets one, as in the
   table above.
2. **The remembered engine survives a restart**, in `state.json`.
3. **`DefaultSearchEngine` keeps its name.** It now applies on the first run and as the fallback,
   and the README's settings table says so.
4. **Later the same day:** the word patterns also accept the curly apostrophe `’`, and Google
   Translate's pattern no longer lets a password-like line through (it was `^[^\r\n]{1,200}$`).
