using System;
using System.Collections.Generic;
using System.IO;

namespace Translator.Blocklist;

public class BlocklistManager
{
    private readonly HashSet<string> _blocklist = new();

    public BlocklistManager()
    {
        foreach (var file in Directory.EnumerateFiles("Blocklist", "*.txt"))
        {
            foreach (var line in File.ReadLines(file))
            {
                if (line.StartsWith('#'))
                    continue;
                var address = line;
                if (!file.EndsWith("my.txt"))
                    address = address.Split(' ')[1];
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