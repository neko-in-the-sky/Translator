using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace Translator.Logging;

/// <summary>
/// Finds the folder that the File sink in appsettings.json writes to. The tray item opens this
/// folder, so the path lives only in configuration and there is no second copy that could drift.
/// </summary>
public static class LogFolder
{
    /// <returns>The folder, or null if no File sink with a path is configured.</returns>
    public static string Find(IConfiguration configuration)
    {
        var path = configuration.GetSection("Serilog:WriteTo").GetChildren()
            .Where(sink => string.Equals(sink["Name"], "File", StringComparison.OrdinalIgnoreCase))
            .Select(sink => sink["Args:path"])
            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));

        // Serilog expands %VAR% in the path and resolves a relative one against the current
        // directory, which App.xaml.cs sets to AppContext.BaseDirectory.
        return path == null
            ? null
            : Path.GetDirectoryName(Path.GetFullPath(
                Environment.ExpandEnvironmentVariables(path), AppContext.BaseDirectory));
    }
}
