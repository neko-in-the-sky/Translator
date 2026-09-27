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
            UserSettings = new UserSettings
            {
                DefaultSearchEngine = defaultSearchEngine,
                AllowedFullscreenApps = []
            },
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

    [Fact]
    public void SearchCommands_NoneIsActiveAtFirst()
    {
        var viewModel = CreateViewModel("Oxford", "Oxford", "Multitran");

        Assert.All(viewModel.SearchCommands, command => Assert.False(command.IsActive));
    }

    [Fact]
    public void SearchCommand_Execute_MakesOnlyThatEngineActive()
    {
        var viewModel = CreateViewModel("Oxford", "Oxford", "Multitran", "Deepl");
        viewModel.SearchCommands[0].Command.Execute(null);

        viewModel.SearchCommands[1].Command.Execute(null);

        Assert.Equal(
            [false, true, false],
            viewModel.SearchCommands.Select(command => command.IsActive));
    }

    [Fact]
    public void DefaultSearchCommand_ExecuteFromHotkey_MakesTheDefaultEngineActive()
    {
        var viewModel = CreateViewModel("Multitran", "Oxford", "Multitran");
        viewModel.SearchCommands[0].Command.Execute(null);

        viewModel.DefaultSearchCommand.Command.Execute(true);

        Assert.True(viewModel.DefaultSearchCommand.IsActive);
        Assert.False(viewModel.SearchCommands[0].IsActive);
    }

    [Fact]
    public void ClearQueryCommand_EmptiesTheQueryAndNotifies()
    {
        var viewModel = CreateViewModel("Oxford", "Oxford");
        viewModel.QueryText = "cat";
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.ClearQueryCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.QueryText);
        Assert.Equal([nameof(MainWindowViewModel.QueryText)], changed);
    }
}
