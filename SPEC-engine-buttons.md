# Spec: engine-buttons — grouped buttons that show the active engine

Module of [SPEC.md](SPEC.md). Depends on search-bar for `IconButtonStyle`, the colours and the
toolbar layout.

## Objective

The engine buttons are 24×24 `ToolBar` buttons with the favicon stretched to fill them. Nothing
shows which engine's page is on screen, so after switching a couple of times the user can't tell
Oxford from DeepL without reading the page.

Make the buttons larger, group them, and highlight the one whose page is showing.

**Acceptance criteria**

1. Each button is 32×32 with a 20×20 icon, corner radius 4, and uses `IconButtonStyle`'s hover and
   pressed backgrounds.
2. The buttons sit in one group to the right of the search box, 8 px from it: a container with
   corner radius 6, `SubtleFillBrush` background, 2 px padding and 2 px between buttons.
3. **Active engine:** the button whose page is showing has an accent tint (`AccentBrush` at about
   15 % opacity), plus a 3×12 px accent line at the bottom centre, like the Windows 11 taskbar. At
   most one button is active.
4. The active engine is the one that last ran a search: by click, by <kbd>Enter</kbd> or the
   magnifier (the default engine), or automatically from <kbd>Ctrl</kbd>+<kbd>Space</kbd> (the
   default engine).
5. When the confirmation page is shown, no button is active.
6. Tooltips still show engine names. Each button's accessible name is the engine name.
   <kbd>Tab</kbd> reaches every button, and keyboard focus shows a rounded focus outline.
7. Icons are drawn with high-quality scaling. Free Dictionary and Multitran ship only a 16 px icon,
   so they may look slightly soft; replacing icons is out of scope.
8. With the default width (650) the search box stays at least 300 px wide.

## Design

- `NavigationButtonViewModel` implements `INotifyPropertyChanged` and gets `IsActive`, which
  notifies only when it changes.
- `MainWindowViewModel` makes a button active when its command executes. Each button's action
  passes the button itself to `RequestNavigation`, which sets that one active and every other one
  inactive. The confirmation page path in `TranslateFromClipboard` sets all inactive.
- The engine `ItemsControl` keeps its horizontal `StackPanel`, wrapped in a rounded `Border` (the
  group). The `ItemsControl` itself isn't a tab stop, so <kbd>Tab</kbd> goes straight to the buttons. `EngineButtonStyle` is based on `IconButtonStyle` and adds a trigger on
  `IsActive` for the tint and the line.
- `FlatButtonStyle` is removed.

## Files

`Translator/MainWindowViewModel.cs`, `Translator/MainWindow.xaml`, `Translator/Styles/Popup.xaml`,
`Translator.Tests/NavigationButtonViewModelTests.cs`, `Translator.Tests/MainWindowViewModelTests.cs`.

## Testing

- **Automated (xUnit), written first:**
  - A new button isn't active.
  - Executing an engine's command makes it active and every other one inactive.
  - Executing the default engine's command (the <kbd>Enter</kbd>, magnifier and hotkey path) makes the
    default one active.
  - `IsActive` raises `PropertyChanged` only when it changes.
  - The confirmation-page path isn't unit-tested: `TranslateFromClipboard` reads the real
    clipboard. It's covered by the check by hand.
- **By hand (user, Windows 11):**
  - [ ] Size, grouping and spacing as in criteria 1–2, 8
  - [ ] <kbd>Ctrl</kbd>+<kbd>Space</kbd> on a word: Oxford highlighted; click Multitran: only Multitran
        highlighted (3, 4)
  - [ ] Type a word, press <kbd>Enter</kbd>: the default engine highlighted (4)
  - [ ] Copy a sentence, <kbd>Ctrl</kbd>+<kbd>Space</kbd>: the confirmation page, nothing highlighted (5)
  - [ ] Hover, press, <kbd>Tab</kbd> through the buttons with a visible focus outline (1, 6)
  - [ ] Icons look sharp enough, apart from the two 16 px ones (7)
