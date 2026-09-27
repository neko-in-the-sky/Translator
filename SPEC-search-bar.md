# Spec: search-bar — the search box and the toolbar layout

Module of [SPEC.md](SPEC.md). Shared decisions, commands, style and boundaries are there.

## Objective

The search box is a default WPF `TextBox`: 25 px tall, square, with a grey 3D-era border and no
hint of what it's for. The toolbar's margins are uneven (`Margin="0,10,20,0"` inside a 5 px grid
margin), and the page floats 10 px below it inside a 5 px frame.

Make the box look and behave like a Windows 11 search box. Give the toolbar even spacing, and let
the page fill the window below it.

**Acceptance criteria**

1. **Layout:** the toolbar has 12 px padding on the left, right and top, and 10 px at the bottom.
   A 1 px divider in `DividerBrush` runs the full width under it. The page fills everything below,
   edge to edge, with no margin. The loading bar row from no-flash sits between the divider and
   the page.
2. **Box:** 32 px tall, corner radius 4, `Segoe UI Variable Text` 14 px, text vertically centred,
   8 px padding on the left. At rest it has a 1 px `ControlBorderBrush` border. On hover the
   background darkens slightly. With keyboard focus the bottom border becomes 2 px in `AccentBrush`,
   as in Windows 11.
3. **Placeholder:** when the box is empty it shows *"Word or phrase"* in English and *"Слово или
   выражение"* in Russian, in `PlaceholderBrush`. It disappears as soon as there's text, and stays
   while the box is focused but empty.
4. **Clear button (×, glyph `E711`):** inside the box on the right, shown only when the box has
   text. Clicking it empties the box and keeps focus in it. It isn't a tab stop.
5. **Magnifier button (glyph `E721`):** inside the box at the far right, always shown. Clicking it
   does what <kbd>Enter</kbd> does: searches with the default engine. It isn't a tab stop.
6. Both glyph buttons are 24×24 inside the box, with a 12 px glyph, and use `IconButtonStyle`:
   transparent at rest, a subtle rounded background on hover and a slightly stronger one while
   pressed. engine-buttons reuses this style.
7. <kbd>Enter</kbd>, <kbd>Esc</kbd> (hides the window), the tooltip, focus on show and the binding to
   `QueryText` all work as before.
8. On Windows 10 the icon font falls back to Segoe MDL2 Assets and the text to Segoe UI, and
   nothing shows as a missing-glyph box.

## Design

- `Translator/Styles/Popup.xaml`, created by no-flash, gains the other colour resources
  (Windows 11's light theme colours, flattened onto white: text, placeholder, divider, control
  fill/hover/focused/border, subtle fill/hover/pressed), font families (`TextFontFamily`,
  `IconFontFamily`), `FocusVisualStyle`, `SearchBoxStyle` (a `TextBox` template) and
  `IconButtonStyle`. The placeholder text reaches the template through the `TextBox`'s `Tag`, so
  the style doesn't depend on the app's strings.
  `AccentBrush` is always used as a `DynamicResource` (see SPEC.md).
- The clear and magnifier buttons are part of `SearchBoxStyle`'s template. The magnifier binds
  `Command` to `DefaultSearchCommand.Command` through the `TextBox`'s `DataContext`. The clear
  button runs a new `ClearQueryCommand` on `MainWindowViewModel` that sets `QueryText` to empty.
  Neither button can take focus, so the cursor stays in the box without any code-behind.
- New string `QueryTextBox_Placeholder` in `Resources.resx` and `Resources.ru-RU.resx`, with a
  hand-written property in `Resources.Designer.cs`.
- `FlatButtonStyle` is removed once nothing uses it (after engine-buttons).

## Files

`Translator/Styles/Popup.xaml`, `Translator/MainWindow.xaml(.cs)`,
`Translator/MainWindowViewModel.cs`, `Translator/Properties/Resources.resx`,
`Resources.ru-RU.resx`, `Resources.Designer.cs`, `Translator.Tests/ResourcesTests.cs`,
`Translator.Tests/MainWindowViewModelTests.cs`.

## Testing

- **Automated (xUnit):**
  - `QueryTextBox_Placeholder` is localised for `en-US` and `ru-RU`, like the existing
    `ResourcesTests`.
  - `ClearQueryCommand` empties `QueryText` and raises `PropertyChanged` for it.
- **By hand (user, Windows 11):**
  - [ ] Box, divider and page edges match the layout in criteria 1–2; the page reaches the window edges
  - [ ] Placeholder in English, and in Russian with `"Culture": "ru-RU"` in the settings file (3)
  - [ ] Type a word: × appears; click it: box empty, cursor still in it (4)
  - [ ] Type a word, click the magnifier: default engine searches (5)
  - [ ] Hover and press on × and the magnifier (6); focus underline in the accent colour (2)
  - [ ] <kbd>Enter</kbd>, <kbd>Esc</kbd>, tooltip, focus on show (7)
