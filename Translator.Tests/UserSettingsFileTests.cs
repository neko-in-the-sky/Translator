// UseWPF projects drop System.IO from the implicit usings, because System.Windows.Shapes.Path
// would collide with System.IO.Path, so it has to be imported explicitly here.
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Translator.Configuration;

namespace Translator.Tests;

/// <summary>
/// xUnit creates one instance per test, so each test gets its own directory for the user file.
/// </summary>
public class UserSettingsFileTests : IDisposable
{
    private readonly string _directory;
    private readonly string _path;

    public UserSettingsFileTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "translator-usersettings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "usersettings.json");
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    /// <summary>
    /// Mirrors the UserSettings section of the shipped appsettings.json.
    /// </summary>
    private static UserSettings Shipped() => new()
    {
        DefaultSearchEngine = "Oxford",
        Culture = "en-US",
        AllowedFullscreenApps = ["firefox", "foxit", "explorer", "translator"],
        Popup = new PopupSettings { DefaultWidth = 650, DefaultHeight = 500, VerticalOffsetFromCursor = 25 }
    };

    private UserSettings ApplyUserFile(string json)
    {
        File.WriteAllText(_path, json);
        return ApplyTo(_path);
    }

    private static UserSettings ApplyTo(string path)
    {
        var settings = Shipped();
        UserSettingsFile.Apply(UserSettingsFile.Load(path), settings, NullLogger.Instance);
        return settings;
    }

    [Fact]
    public void ShippedAppSettings_BindsEveryUserSetting()
    {
        // The shipped file reaches this project's output through the project reference. A typo
        // while restructuring it would otherwise only show up at runtime as a null setting.
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
            .Build();

        var settings = configuration.GetSection(nameof(ApplicationSettings)).Get<ApplicationSettings>()!;

        Assert.NotEmpty(settings.SearchEngines);
        Assert.NotNull(settings.UserSettings);
        Assert.False(string.IsNullOrEmpty(settings.UserSettings.DefaultSearchEngine));
        Assert.False(string.IsNullOrEmpty(settings.UserSettings.Culture));
        Assert.NotEmpty(settings.UserSettings.AllowedFullscreenApps);
        Assert.NotNull(settings.UserSettings.Popup);
        Assert.True(settings.UserSettings.Popup.DefaultWidth > 0);
    }

    [Fact]
    public void DefaultPath_IsUserSettingsJsonInTheTranslatorAppDataFolder()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        Assert.Equal(Path.Combine(appData, "Translator", "usersettings.json"), UserSettingsFile.DefaultPath);
    }

    [Fact]
    public void Apply_NoUserFile_KeepsShippedSettings()
    {
        var settings = ApplyTo(_path);

        Assert.Equivalent(Shipped(), settings, strict: true);
    }

    [Fact]
    public void Apply_NoUserFolder_KeepsShippedSettings()
    {
        // On first run %APPDATA%\Translator may not exist at all, not just the file.
        var settings = ApplyTo(Path.Combine(_directory, "missing", "usersettings.json"));

        Assert.Equivalent(Shipped(), settings, strict: true);
    }

    [Fact]
    public void Apply_UserValues_OverrideShippedValues()
    {
        var settings = ApplyUserFile("""{ "Culture": "ru-RU", "DefaultSearchEngine": "Multitran" }""");

        Assert.Equal("ru-RU", settings.Culture);
        Assert.Equal("Multitran", settings.DefaultSearchEngine);
    }

    [Fact]
    public void Apply_PartialPopup_OverridesOnlyThatField()
    {
        var settings = ApplyUserFile("""{ "Popup": { "DefaultWidth": 800 } }""");

        Assert.Equal(800, settings.Popup.DefaultWidth);
        Assert.Equal(500, settings.Popup.DefaultHeight);
        Assert.Equal(25, settings.Popup.VerticalOffsetFromCursor);
    }

    [Fact]
    public void Apply_ShorterUserList_ReplacesTheShippedList()
    {
        // Plain configuration layering merges arrays by index, which would keep "explorer" and
        // "translator" from the shipped list here.
        var settings = ApplyUserFile("""{ "AllowedFullscreenApps": ["chrome"] }""");

        Assert.Equal(["chrome"], settings.AllowedFullscreenApps);
    }

    [Fact]
    public void Apply_LongerUserList_ReplacesTheShippedList()
    {
        // ConfigurationBinder.Bind appends to an existing array rather than replacing it.
        var settings = ApplyUserFile("""{ "AllowedFullscreenApps": ["a", "b", "c", "d", "e"] }""");

        Assert.Equal(["a", "b", "c", "d", "e"], settings.AllowedFullscreenApps);
    }

    [Fact]
    public void Apply_EmptyUserList_ClearsTheShippedList()
    {
        // The JSON provider stores [] as a key with a null value and no children, so the section
        // looks absent to GetSection().Exists(). An empty list must still mean "no apps".
        var settings = ApplyUserFile("""{ "AllowedFullscreenApps": [] }""");

        Assert.Empty(settings.AllowedFullscreenApps);
    }

    [Fact]
    public void Apply_NoUserList_KeepsTheShippedList()
    {
        var settings = ApplyUserFile("""{ "Culture": "ru-RU" }""");

        Assert.Equal(["firefox", "foxit", "explorer", "translator"], settings.AllowedFullscreenApps);
    }

    [Fact]
    public void Apply_KeysOutsideUserSettings_AreIgnored()
    {
        var settings = ApplyUserFile("""
            {
              "SearchEngines": [ { "Name": "Mine" } ],
              "Serilog": { "MinimumLevel": { "Default": "Verbose" } },
              "ApplicationSettings": { "UserSettings": { "Culture": "ru-RU" } }
            }
            """);

        Assert.Equivalent(Shipped(), settings, strict: true);
    }

    [Fact]
    public void Apply_KeysOutsideUserSettings_AreNamedInOneWarning()
    {
        File.WriteAllText(_path, """{ "SearchEngines": [], "Serilog": {}, "ApplicationSettings": {}, "Culture": "ru-RU" }""");
        var logger = new CapturingLogger();

        UserSettingsFile.Apply(UserSettingsFile.Load(_path), Shipped(), logger);

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        var ignored = (IEnumerable<string>)warning.Values["Keys"]!;
        Assert.Equivalent(new[] { "SearchEngines", "Serilog", "ApplicationSettings" }, ignored, strict: true);
    }

    [Fact]
    public void Apply_OnlyUserSettingsKeys_LogsNoWarning()
    {
        // Matching is case-insensitive, like the binder's.
        File.WriteAllText(_path, """{ "culture": "ru-RU", "Popup": { "DefaultWidth": 800 }, "AllowedFullscreenApps": [] }""");
        var logger = new CapturingLogger();

        UserSettingsFile.Apply(UserSettingsFile.Load(_path), Shipped(), logger);

        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public void Load_MalformedJson_ThrowsNamingTheFile()
    {
        // App shows this message in its startup error dialog, so it must point the user at the file.
        File.WriteAllText(_path, """{ "Culture": "ru-RU" """);

        var exception = Record.Exception(() => UserSettingsFile.Load(_path));

        Assert.NotNull(exception);
        Assert.Contains(_path, exception.Message);
    }

    [Fact]
    public void Load_CommentsAndTrailingCommas_AreAccepted()
    {
        var settings = ApplyUserFile("""
            // My settings
            {
              /* Russian UI */
              "Culture": "ru-RU",
            }
            """);

        Assert.Equal("ru-RU", settings.Culture);
    }

    /// <summary>
    /// Keeps each entry's structured values (the {Placeholders} of the message template), so tests
    /// can assert on exactly what was logged rather than on the wording.
    /// </summary>
    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, Dictionary<string, object?> Values)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var values = state as IEnumerable<KeyValuePair<string, object?>> ?? [];
            Entries.Add((logLevel, values.ToDictionary(kv => kv.Key, kv => kv.Value)));
        }
    }
}
