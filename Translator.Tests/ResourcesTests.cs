using System.Globalization;
using Translator.Properties;

namespace Translator.Tests;

public class ResourcesTests
{
    [Theory]
    [InlineData("en-US", "Open settings folder")]
    [InlineData("ru-RU", "Открыть папку настроек")]
    public void OpenSettingsFolder_IsLocalised(string culture, string expected)
    {
        // Resources.Designer.cs is regenerated only by Visual Studio, so this also proves the
        // property exists for command-line and CI builds.
        var text = Resources.ResourceManager.GetString(
            nameof(Resources.TrayIcon_MenuItem_OpenSettingsFolder), CultureInfo.GetCultureInfo(culture));

        Assert.Equal(expected, text);
    }

    [Theory]
    [InlineData("en-US", "Open logs folder")]
    [InlineData("ru-RU", "Открыть папку логов")]
    public void OpenLogsFolder_IsLocalised(string culture, string expected)
    {
        var text = Resources.ResourceManager.GetString(
            nameof(Resources.TrayIcon_MenuItem_OpenLogsFolder), CultureInfo.GetCultureInfo(culture));

        Assert.Equal(expected, text);
    }
}
