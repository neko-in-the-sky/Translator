using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Translator.Configuration;

/// <summary>
/// The per-user settings file. Releases replace appsettings.json in the install folder, so a user's
/// changes live here instead and are overlaid onto <see cref="UserSettings"/> at startup.
/// </summary>
public static class UserSettingsFile
{
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Translator", "usersettings.json");

    /// <summary>
    /// Reads the file into a configuration of its own. It is deliberately never added to the host's
    /// configuration, so nothing outside <see cref="UserSettings"/> can be overridden. Throws on
    /// malformed JSON, with the file's path in the message.
    /// </summary>
    public static IConfiguration Load(string path) => new ConfigurationBuilder()
        .AddJsonFile(path, optional: true, reloadOnChange: false)
        .Build();

    /// <summary>
    /// Overlays the user's values onto <paramref name="userSettings"/>, which already holds the
    /// shipped values. Anything the file leaves out keeps its shipped value, and a list the file
    /// sets replaces the shipped list completely.
    /// </summary>
    public static void Apply(IConfiguration userConfiguration, UserSettings userSettings, ILogger logger)
    {
        var userKeys = userConfiguration.GetChildren()
            .Select(section => section.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var properties = typeof(UserSettings).GetProperties();

        // Bind appends to an existing array instead of replacing it, so empty every list the user
        // sets first. The key set comes from GetChildren rather than GetSection().Exists(), because
        // the JSON provider stores [] as a null value, which Exists() reports as absent.
        foreach (var property in properties)
        {
            if (property.PropertyType.IsArray && userKeys.Contains(property.Name))
            {
                property.SetValue(userSettings, Array.CreateInstance(property.PropertyType.GetElementType()!, 0));
            }
        }

        userConfiguration.Bind(userSettings);

        // A warning, not an error: a setting removed from UserSettings in a later release must not
        // stop the app starting after the very update this file exists to survive.
        var ignored = userKeys
            .Where(key => !properties.Any(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (ignored.Length > 0)
        {
            logger.LogWarning(
                "Ignored {Keys} in the user settings file: only {Allowed} can be overridden there",
                ignored, properties.Select(p => p.Name));
        }
    }
}
