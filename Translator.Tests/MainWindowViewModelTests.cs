// UseWPF projects drop System.IO from the implicit usings, because System.Windows.Shapes.Path
// would collide with System.IO.Path, so it has to be imported explicitly here.
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Translator.Configuration;

namespace Translator.Tests;

/// <summary>
/// xUnit creates one instance per test, so each test gets its own state file.
/// </summary>
public class MainWindowViewModelTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "translator-viewmodel-" + Guid.NewGuid().ToString("N"));

    private string StatePath => Path.Combine(_directory, "state.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private MainWindowViewModel CreateViewModel(string defaultSearchEngine, params string[] engineNames)
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
            new AppState(StatePath, NullLogger<AppState>.Instance),
            settings,
            NullLogger<MainWindowViewModel>.Instance);
    }

    [Fact]
    public void CurrentSearchCommand_IsTheConfiguredEngine()
    {
        var viewModel = CreateViewModel("Second", "First", "Second");

        Assert.Equal("Second", viewModel.CurrentSearchCommand.ToolTip);
    }

    [Fact]
    public void CurrentSearchCommand_UnknownName_FallsBackToTheFirstEngine()
    {
        // A typo in DefaultSearchEngine used to leave the default null, so the hotkey
        // crashed with a NullReferenceException on every press.
        var viewModel = CreateViewModel("Oxfrod", "Oxford", "Cambridge");

        Assert.Equal("Oxford", viewModel.CurrentSearchCommand.ToolTip);
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
    public void CurrentSearchCommand_ExecuteFromHotkeyBeforeAnyClick_MakesTheDefaultEngineActive()
    {
        var viewModel = CreateViewModel("Multitran", "Oxford", "Multitran");

        viewModel.CurrentSearchCommand.Command.Execute(true);

        Assert.Equal([false, true], viewModel.SearchCommands.Select(command => command.IsActive));
    }

    [Fact]
    public void SearchCommand_Execute_MakesThatEngineCurrent()
    {
        var viewModel = CreateViewModel("Oxford", "Oxford", "Multitran");
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.SearchCommands[1].Command.Execute(null);

        Assert.Equal("Multitran", viewModel.CurrentSearchCommand.ToolTip);
        Assert.Contains(nameof(MainWindowViewModel.CurrentSearchCommand), changed);
    }

    [Fact]
    public void CurrentSearchCommand_AfterRestart_IsTheLastEnginePicked()
    {
        CreateViewModel("Oxford", "Oxford", "Multitran").SearchCommands[1].Command.Execute(null);

        var restarted = CreateViewModel("Oxford", "Oxford", "Multitran");

        Assert.Equal("Multitran", restarted.CurrentSearchCommand.ToolTip);
    }

    [Fact]
    public void CurrentSearchCommand_RememberedEngineRemoved_FallsBackToTheDefault()
    {
        CreateViewModel("Oxford", "Oxford", "SpanishDict").SearchCommands[1].Command.Execute(null);

        // An update removed SpanishDict.
        var updated = CreateViewModel("Multitran", "Oxford", "Multitran");

        Assert.Equal("Multitran", updated.CurrentSearchCommand.ToolTip);
    }

    [Fact]
    public void CurrentSearchCommand_ExecuteFromHotkey_KeepsTheEngine()
    {
        var viewModel = CreateViewModel("Oxford", "Oxford", "Multitran");
        viewModel.SearchCommands[1].Command.Execute(null);

        viewModel.CurrentSearchCommand.Command.Execute(true);

        Assert.Equal("Multitran", viewModel.CurrentSearchCommand.ToolTip);
        Assert.Equal("Multitran", CreateViewModel("Oxford", "Oxford", "Multitran").CurrentSearchCommand.ToolTip);
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
