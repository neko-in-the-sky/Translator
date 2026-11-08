using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Translator;

/// <summary>
/// Logs the reports that <c>js/page_filter.js</c> posts when a selector is invalid or matches nothing,
/// each one once per run.
/// </summary>
public class PageFilterReports
{
    private const int MaxFieldLength = 200;

    private readonly ILogger<PageFilterReports> _logger;
    private readonly HashSet<(string Engine, string Selector, string Reason)> _logged = new();

    public PageFilterReports(ILogger<PageFilterReports> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Handles a web message. Any page can post one, so anything that isn't a page filter report is
    /// ignored, and the fields are truncated.
    /// </summary>
    /// <returns>Whether a warning was logged.</returns>
    public bool Handle(string messageJson, string pageUrl)
    {
        string engine, selector, reason;
        try
        {
            using var message = JsonDocument.Parse(messageJson);
            var root = message.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || GetString(root, "type") != "page-filter"
                || (engine = GetString(root, "engine")) == null
                || (selector = GetString(root, "selector")) == null
                || (reason = GetString(root, "reason")) is not ("no-match" or "invalid-selector"))
            {
                return false;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        engine = Truncate(engine);
        selector = Truncate(selector);
        if (!_logged.Add((engine, selector, reason)))
        {
            return false;
        }

        _logger.LogWarning("Page filter for {Engine}: the selector {Selector} is {Problem} on {Url}",
            engine, selector, reason == "no-match" ? "matching nothing" : "not valid CSS", Truncate(pageUrl));
        return true;
    }

    private static string GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string Truncate(string value) =>
        value == null || value.Length <= MaxFieldLength ? value : value[..MaxFieldLength] + "…";
}
