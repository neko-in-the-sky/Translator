// UseWPF projects drop System.IO from the implicit usings, because System.Windows.Shapes.Path
// would collide with System.IO.Path, so it has to be imported explicitly here.
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Translator.Configuration;

namespace Translator.Tests;

/// <summary>
/// Checks the patterns in the shipped appsettings.json, not copies of them, so a change to the real
/// file is what these tests catch. A pattern decides whether the hotkey searches at once or shows the
/// confirmation page first.
/// </summary>
public class AutoSearchRegexTests
{
    private static readonly SearchEngine[] Engines = new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
        .Build()
        .GetSection("ApplicationSettings:SearchEngines")
        .Get<SearchEngine[]>()!;

    private static bool Matches(string engine, string text) =>
        Regex.IsMatch(text, Engines.Single(e => e.Name == engine).AutoSearchRegex!);

    [Fact]
    public void EveryEngine_HasAPattern()
    {
        Assert.All(Engines, engine => Assert.False(string.IsNullOrEmpty(engine.AutoSearchRegex), engine.Name));
    }

    [Theory]
    [InlineData("Oxford", "house")]
    [InlineData("Oxford", "give up")]
    [InlineData("Free Dictionary", "house")]
    [InlineData("Free Dictionary", "give up")]
    [InlineData("Free Dictionary", "mother-in-law")]
    [InlineData("Free Dictionary", "o'clock")]
    [InlineData("Free Dictionary", "don’t")]                            // curly apostrophe, as web pages write it
    [InlineData("Wiktionary English", "it’s")]
    [InlineData("Wiktionary German", "geht’s")]
    [InlineData("Multitran", "give up")]
    [InlineData("Urban Dictionary", "spill the tea")]
    [InlineData("Wiktionary English", "house")]
    [InlineData("Wiktionary Spanish", "niño")]
    [InlineData("Wiktionary Spanish", "darse cuenta")]
    [InlineData("Wiktionary German", "Straße")]
    [InlineData("Google Translate", "Haus")]
    [InlineData("Google Translate", "Das Haus ist groß.")]
    [InlineData("Google Translate", "Где находится вокзал?")]
    [InlineData("Google Translate", "¿Dónde está la estación?")]
    [InlineData("Google Translate", "I have 3 cats.")]
    [InlineData("Google Translate", "It costs 1,000.50 euros")]
    [InlineData("Google Translate", "Meet me at 10:30.")]
    [InlineData("Google Translate", "«Bonjour», dit-il.")]
    [InlineData("Google Translate", "Don’t worry, be happy!")]
    [InlineData("Google Translate", "He said: \"go home\" (now).")]
    [InlineData("Google Translate", "你好，世界。")]
    public void Pattern_AcceptsAWordPhraseOrSentence(string engine, string text)
    {
        Assert.True(Matches(engine, text));
    }

    [Theory]
    [InlineData("Free Dictionary", "niño")]                             // English letters only
    [InlineData("Free Dictionary", "one two three four five")]          // more than four words
    [InlineData("Wiktionary German", "Das Haus ist groß.")]             // punctuation: a sentence
    [InlineData("Wiktionary German", "C3PO")]
    [InlineData("Google Translate", "first line\nsecond line")]
    [InlineData("Google Translate", "")]
    [InlineData("Google Translate", "It costs $5")]                     // $ is a symbol, not punctuation
    [InlineData("Google Translate", "50%")]
    public void Pattern_RejectsOtherText(string engine, string text)
    {
        Assert.False(Matches(engine, text));
    }

    /// <summary>
    /// A copied password, link or address must not go to Google Translate without the confirmation page.
    /// </summary>
    [Theory]
    [InlineData("hunter2")]                     // letter touching a digit
    [InlineData("Summer2024!")]
    [InlineData("Summer-2024!")]                // ... even through punctuation
    [InlineData("Tr0ub4dor&3")]
    [InlineData("P@ssw0rd!")]
    [InlineData("x7#kQ9$mL2")]
    [InlineData("aB3$dE6^")]
    [InlineData("https://example.com/page")]    // excluded characters: / @ \
    [InlineData("someone@example.com")]
    [InlineData(@"C:\Users\me")]
    public void GoogleTranslate_RejectsPasswordsLinksAndAddresses(string text)
    {
        Assert.False(Matches("Google Translate", text));
    }

    [Fact]
    public void GoogleTranslate_RejectsMoreThan200Characters()
    {
        Assert.True(Matches("Google Translate", new string('a', 200)));
        Assert.False(Matches("Google Translate", new string('a', 201)));
    }
}
