using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Translator;

/// <summary>
/// What the app remembers between runs, as opposed to what the user sets: the last engine used.
/// Kept in its own file because usersettings.json is the user's, and the app never writes to it.
/// Nothing here is essential, so a file that can't be read or written only logs a warning.
/// </summary>
public class AppState
{
    private const string LastSearchEngineKey = "LastSearchEngine";

    private readonly string _path;
    private readonly ILogger<AppState> _logger;

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Translator", "state.json");

    public AppState(ILogger<AppState> logger) : this(DefaultPath, logger)
    {
    }

    /// <summary>
    /// The path is a parameter so tests can use a temporary file; the app always uses <see cref="DefaultPath"/>.
    /// </summary>
    public AppState(string path, ILogger<AppState> logger)
    {
        _path = path;
        _logger = logger;
        LastSearchEngine = Load();
    }

    /// <summary>
    /// The name of the engine last picked, or null if none is remembered.
    /// </summary>
    public string LastSearchEngine { get; private set; }

    public void SetLastSearchEngine(string name)
    {
        if (name == LastSearchEngine)
        {
            return;
        }

        LastSearchEngine = name;
        Save();
    }

    private string Load()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(_path));
            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty(LastSearchEngineKey, out var value)
                   && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Could not read the app state file {Path}; nothing is remembered", _path);
            return null;
        }
    }

    private void Save()
    {
        // Written to a temporary file that then replaces the real one, so a crash mid-write can't
        // leave a broken file behind.
        var temporaryPath = _path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(temporaryPath,
                JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    [LastSearchEngineKey] = LastSearchEngine
                }));
            File.Move(temporaryPath, _path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Could not save the app state file {Path}", _path);
        }
    }
}
