using System;
using System.Collections.Generic;
using System.IO;

namespace Translator.Blocklist;

public class BlocklistManager
{
    private const string DefaultDirectory = "Blocklist";

    private readonly HashSet<string> _blocklist = new();

    public BlocklistManager() : this(DefaultDirectory)
    {
    }

    /// <summary>
    /// Loads every *.txt file in <paramref name="directory"/>. The parameter exists so tests can
    /// point at a fixture directory; the app always uses the default.
    /// </summary>
    public BlocklistManager(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.txt"))
        {
            foreach (var line in File.ReadLines(file))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                    continue;

                // The hosts-format lists use "0.0.0.0 example.com" while my.txt holds bare
                // domains. Taking the last whitespace-separated token handles both, and no
                // longer throws on the blank lines the upstream lists now contain.
                var address = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)[^1];
                _blocklist.Add(address);
            }
        }
    }

    public BlockResponse TryBlock(string url)
    {
        var uri = new Uri(url);
        var domain = WebHelpers.GetDomain(uri);
        return _blocklist.Contains(domain) ? new BlockResponse(404, "Not found") : null;
    }
}