using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Translator.Configuration;

namespace Translator;

/// <summary>
/// Builds the script that hides the parts of a page listed in its engine's <see cref="SearchEngine.Hide"/>
/// selectors: <c>js/page_filter.js</c> with the selectors of every engine filled in.
/// </summary>
public class PageFilterScript
{
    public const string FiltersPlaceholder = "/* filters */ {}";

    public PageFilterScript(IOptions<ApplicationSettings> applicationSettings)
        : this(applicationSettings.Value.SearchEngines, File.ReadAllText(Path.Combine("js", "page_filter.js")))
    {
    }

    /// <summary>
    /// The template is a parameter so tests can use a minimal one; the app always uses the shipped file.
    /// </summary>
    public PageFilterScript(IEnumerable<SearchEngine> searchEngines, string template)
    {
        if (!template.Contains(FiltersPlaceholder, StringComparison.Ordinal))
        {
            throw new ArgumentException($"The page filter script has no '{FiltersPlaceholder}' placeholder.",
                nameof(template));
        }

        // The page can only tell engines apart by host, so engines on the same host (the Wiktionary
        // languages) share one filter: their selectors are merged and their names joined.
        var byHost = new Dictionary<string, (List<string> Engines, List<string> Hide)>();
        foreach (var searchEngine in searchEngines)
        {
            var hide = searchEngine.Hide?.Where(selector => !string.IsNullOrWhiteSpace(selector)).ToArray();
            if (hide is not { Length: > 0 })
            {
                continue;
            }

            var host = WebHelpers.GetHost(new Uri(searchEngine.UrlTemplate));
            if (!byHost.TryGetValue(host, out var filter))
            {
                byHost[host] = filter = (new List<string>(), new List<string>());
            }

            filter.Engines.Add(searchEngine.Name);
            filter.Hide.AddRange(hide.Where(selector => !filter.Hide.Contains(selector)));
        }

        var filters = byHost.ToDictionary(
            pair => pair.Key,
            pair => new PageFilter(string.Join(", ", pair.Value.Engines), pair.Value.Hide.ToArray()));

        // System.Text.Json escapes quotes, backslashes and characters such as '<', so the result
        // is always a valid JavaScript object literal.
        var json = JsonSerializer.Serialize(filters, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Text = template.Replace(FiltersPlaceholder, json, StringComparison.Ordinal);
    }

    public string Text { get; }

    private record PageFilter(string Engine, string[] Hide);
}
