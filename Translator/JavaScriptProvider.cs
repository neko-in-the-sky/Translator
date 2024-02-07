using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Options;
using Translator.Configuration;

namespace Translator;

/// <summary>
/// Provides JavaScript scripts for modification of loaded web pages.
/// </summary>
public class JavaScriptProvider
{
    private readonly Dictionary<string, string> _domainToJavaScriptMap = new();

    public JavaScriptProvider(IOptions<ApplicationSettings> applicationSettings)
    {
        foreach (var searchEngine in applicationSettings.Value.SearchEngines)
        {
            if (!string.IsNullOrEmpty(searchEngine.JsFileName))
            {
                var domain = WebHelpers.GetDomain(new Uri(searchEngine.UrlTemplate));
                _domainToJavaScriptMap[domain] = File.ReadAllText(Path.Combine("js", searchEngine.JsFileName));
            }
        }
    }

    public string GetPostProcessingJavaScript(string url)
    {
        var domain = WebHelpers.GetDomain(new Uri(url));
        return _domainToJavaScriptMap.TryGetValue(domain, out var javaScript) ? javaScript : null;
    }
}