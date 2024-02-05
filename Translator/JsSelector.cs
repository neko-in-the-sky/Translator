using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Options;
using Translator.Configuration;

namespace Translator;

public class JsSelector
{
    private readonly Dictionary<string, string> _map = new();

    public JsSelector(IOptions<ApplicationSettings> applicationSettings)
    {
        foreach (var searchEngine in applicationSettings.Value.SearchEngines)
        {
            if (!string.IsNullOrEmpty(searchEngine.JsFileName))
            {
                var domain = WebHelpers.GetDomain(new Uri(searchEngine.UrlTemplate));
                _map[domain] = searchEngine.JsFileName;
            }
        }
    }
    
    public string SelectJs(string url)
    {
        var domain = WebHelpers.GetDomain(new Uri(url));
        if (_map.TryGetValue(domain, out var js))
        {
            return File.ReadAllText(Path.Combine("js", js));
        }

        return null;
    }
}