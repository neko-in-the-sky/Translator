using Translator.Configuration;

namespace Translator.Tests;

public class NavigationButtonViewModelTests
{
    private const string QueryTemplate = "https://example.com/search?q={0}";
    private const string PathTemplate = "https://example.com/define/{0}";

    private static string BuildUrl(string template, string? query)
    {
        NavigationRequestedEventArgs? captured = null;
        var engine = new SearchEngine
        {
            Name = "Test",
            UrlTemplate = template,
            IconFileName = "test.ico"
        };
        var button = new NavigationButtonViewModel(args => captured = args, () => query!, engine);

        button.Command.Execute(null);

        Assert.NotNull(captured);
        return captured!.Url;
    }

    [Theory]
    [InlineData("hello & goodbye", "hello%20%26%20goodbye")]  // & would start a new query parameter
    [InlineData("C#", "C%23")]                                // # would start a fragment
    [InlineData("what?", "what%3F")]                          // ? would start a query string
    [InlineData("a+b", "a%2Bb")]                              // + is read as a space by many servers
    [InlineData("hello world", "hello%20world")]
    [InlineData("dictionary", "dictionary")]                  // ordinary words are untouched
    public void Execute_PercentEncodesTheQuery(string query, string expected)
    {
        Assert.Equal($"https://example.com/search?q={expected}", BuildUrl(QueryTemplate, query));
    }

    [Theory]
    [InlineData("café", "caf%C3%A9")]
    [InlineData("привет", "%D0%BF%D1%80%D0%B8%D0%B2%D0%B5%D1%82")]
    [InlineData("straße", "stra%C3%9Fe")]
    public void Execute_EncodesNonLatinTextAsUtf8(string query, string expected)
    {
        // The Russian, Spanish and German engines exist to be given exactly this.
        Assert.Equal($"https://example.com/search?q={expected}", BuildUrl(QueryTemplate, query));
    }

    [Fact]
    public void Execute_PathTemplate_EncodesSlashesRatherThanAddingSegments()
    {
        Assert.Equal("https://example.com/define/a%2Fb", BuildUrl(PathTemplate, "a/b"));
    }

    [Fact]
    public void Execute_NullQuery_DoesNotThrow()
    {
        // QueryText is null until something sets it, so clicking an engine button before
        // typing or copying anything must not blow up.
        Assert.Equal("https://example.com/search?q=", BuildUrl(QueryTemplate, null));
    }

    [Fact]
    public void Execute_PassesHotkeyFlagThrough()
    {
        NavigationRequestedEventArgs? captured = null;
        var engine = new SearchEngine { Name = "Test", UrlTemplate = QueryTemplate, IconFileName = "test.ico" };
        var button = new NavigationButtonViewModel(args => captured = args, () => "word", engine);

        button.Command.Execute(true);

        Assert.True(captured!.IsFromHotkey);
    }
}
