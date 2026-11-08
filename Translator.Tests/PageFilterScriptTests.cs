// UseWPF projects drop System.IO from the implicit usings, because System.Windows.Shapes.Path
// would collide with System.IO.Path, so it has to be imported explicitly here.
using System.IO;
using System.Text.Json;
using Translator.Configuration;

namespace Translator.Tests;

public class PageFilterScriptTests
{
    private static SearchEngine Engine(string name, string urlTemplate, params string[]? hide) =>
        new() { Name = name, UrlTemplate = urlTemplate, Hide = hide };

    /// <summary>
    /// With the placeholder as the whole template, the script's text is just the filters' JSON.
    /// </summary>
    private static JsonElement Filters(params SearchEngine[] engines) =>
        JsonDocument.Parse(new PageFilterScript(engines, PageFilterScript.FiltersPlaceholder).Text).RootElement;

    [Fact]
    public void Text_EngineWithSelectors_IsKeyedByDomain()
    {
        var filters = Filters(Engine("Oxford", "https://www.oxfordlearnersdictionaries.com/search/english/?q={0}",
            "#ox-header", "#searchbar"));

        var filter = filters.GetProperty("oxfordlearnersdictionaries.com");
        Assert.Equal("Oxford", filter.GetProperty("engine").GetString());
        Assert.Equal(["#ox-header", "#searchbar"],
            filter.GetProperty("hide").EnumerateArray().Select(selector => selector.GetString()));
    }

    [Fact]
    public void Text_EngineWithoutSelectors_IsLeftOut()
    {
        var filters = Filters(
            Engine("Multitran", "https://www.multitran.com/m.exe?s={0}", null),
            Engine("DeepL", "https://www.deepl.com/translator#en/ru/{0}"),
            Engine("Blank", "https://blank.example.com/{0}", " ", ""));

        Assert.Empty(filters.EnumerateObject());
    }

    [Fact]
    public void Text_SelectorWithQuotesAndBackslashes_SurvivesUnchanged()
    {
        const string selector = "[id^=\"ad_\"], a[href='x\\y'] </script>";

        var filters = Filters(Engine("Site", "https://example.com/{0}", selector));

        Assert.Equal(selector, filters.GetProperty("example.com").GetProperty("hide")[0].GetString());
    }

    [Fact]
    public void Constructor_TemplateWithoutPlaceholder_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PageFilterScript([], "const filters = {};"));
    }

    [Fact]
    public void ShippedScript_HasThePlaceholderOnce()
    {
        var template = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "js", "page_filter.js"));

        var first = template.IndexOf(PageFilterScript.FiltersPlaceholder, StringComparison.Ordinal);
        Assert.True(first >= 0);
        Assert.Equal(-1, template.IndexOf(PageFilterScript.FiltersPlaceholder, first + 1, StringComparison.Ordinal));
    }
}
