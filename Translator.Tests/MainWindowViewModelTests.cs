using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Translator.Configuration;

namespace Translator.Tests;

public class MainWindowViewModelTests
{
    private static MainWindowViewModel CreateViewModel(string defaultSearchEngine, params string[] engineNames)
    {
        var settings = Options.Create(new ApplicationSettings
        {
            DefaultSearchEngine = defaultSearchEngine,
            AllowedFullscreenApps = [],
            SearchEngines = engineNames
                .Select(name => new SearchEngine
                {
                    Name = name,
                    UrlTemplate = "https://example.com/search?q={0}",
                    IconFileName = "test.ico"
                })
                .ToArray()
        });

        return new MainWindowViewModel(
            new HotkeyManager(),
            new NotificationStateChecker(settings, NullLogger<NotificationStateChecker>.Instance),
            new PageBuilder(),
            settings,
            NullLogger<MainWindowViewModel>.Instance);
    }

    [Fact]
    public void DefaultSearchCommand_IsTheConfiguredEngine()
    {
        var viewModel = CreateViewModel("Second", "First", "Second");

        Assert.Equal("Second", viewModel.DefaultSearchCommand.ToolTip);
    }

    [Fact]
    public void DefaultSearchCommand_UnknownName_FallsBackToTheFirstEngine()
    {
        // A typo in DefaultSearchEngine used to leave the default null, so the hotkey
        // crashed with a NullReferenceException on every press.
        var viewModel = CreateViewModel("Oxfrod", "Oxford", "Cambridge");

        Assert.Equal("Oxford", viewModel.DefaultSearchCommand.ToolTip);
    }

    [Fact]
    public void Constructor_NoEngines_ThrowsAClearConfigurationError()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => CreateViewModel("Oxford"));

        Assert.Contains("SearchEngines", exception.Message);
    }
}
