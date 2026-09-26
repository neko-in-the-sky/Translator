using System;
using System.IO;
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
    /// shipped values. Anything the file leaves out keeps its shipped value.
    /// </summary>
    public static void Apply(IConfiguration userConfiguration, UserSettings userSettings, ILogger logger)
    {
        userConfiguration.Bind(userSettings);
    }
}
