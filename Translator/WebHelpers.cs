using System;

namespace Translator;

public static class WebHelpers
{
    public static string GetDomain(Uri uri)
    {
        var host = uri.Host;
        var hostParts = host.Split('.');
        var domain = hostParts.Length >= 2
            ? string.Join(".", hostParts[hostParts.Length - 2], hostParts[hostParts.Length - 1])
            : host;
        return domain;
    }
}