using System;

namespace Translator;

public static class WebHelpers
{
    /// <summary>
    /// The host without a leading "www.", so that "www.example.com" and "example.com" give the same
    /// key, while other subdomains, such as "translate.google.com", are kept apart from "consent.google.com".
    /// </summary>
    public static string GetHost(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();
        return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
    }
}
