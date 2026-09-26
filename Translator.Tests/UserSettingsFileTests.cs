// UseWPF projects drop System.IO from the implicit usings, because System.Windows.Shapes.Path
// would collide with System.IO.Path, so it has to be imported explicitly here.
using System.IO;
using Microsoft.Extensions.Configuration;
using Translator.Configuration;

namespace Translator.Tests;

public class UserSettingsFileTests
{
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
}
