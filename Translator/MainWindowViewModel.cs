using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Translator.Configuration;
using Translator.Properties;

namespace Translator;

public class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly HotkeyManager _hotkeyManager;
    private readonly NotificationStateChecker _notificationStateChecker;
    private readonly PageBuilder _pageBuilder;
    private readonly NavigationButtonViewModel _defaultButton;
    private readonly ILogger<MainWindowViewModel> _logger;
    private string _queryText;

    public MainWindowViewModel(HotkeyManager hotkeyManager, NotificationStateChecker notificationStateChecker,
        PageBuilder pageBuilder, IOptions<ApplicationSettings> applicationSettings, ILogger<MainWindowViewModel> logger)
    {
        _hotkeyManager = hotkeyManager;
        _notificationStateChecker = notificationStateChecker;
        _pageBuilder = pageBuilder;
        _logger = logger;

        SearchCommands = new();
        foreach (var searchEngine in applicationSettings.Value.SearchEngines)
        {
            var command = new NavigationButtonViewModel(RequestNavigation, () => QueryText, searchEngine);
            SearchCommands.Add(command);
            if (searchEngine.Name == applicationSettings.Value.DefaultSearchEngine)
            {
                _defaultButton = command;
            }
        }
    }

    public void Init(Window window)
    {
        _hotkeyManager.Init(window, TranslateFromClipboard);
        _hotkeyManager.RegisterHotKey();
    }

    public string QueryText
    {
        get => _queryText;
        set => SetField(ref _queryText, value);
    }

    public event Action<NavigationRequestedEventArgs> NavigationRequested;

    public void TranslateFromClipboard()
    {
        if (!_notificationStateChecker.AreNotificationsAllowed())
        {
            _logger.LogInformation("Notifications are disabled");
            return;
        }

        var text = GetTextFromClipboard();
        if (text == null)
        {
            return;
        }

        QueryText = text;

        if (_defaultButton.CanAutoSearch(text))
        {
            _defaultButton.Command.Execute(true);
        }
        else
        {
            var page = _pageBuilder.MakeInfoPage(Resources.Notification_SuspiciousText);
            NavigationRequested?.Invoke(new NavigationRequestedEventArgs
            {
                Page = page,
                IsFromHotkey = true
            });
        }
    }

    private string GetTextFromClipboard()
    {
        for (var i = 0; i < 3; i++)
        {
            try
            {
                return Clipboard.GetText().Trim();
            }
            catch (COMException exception)
            {
                _logger.LogError(exception, "Error while getting text from the clipboard");
                Task.Delay(TimeSpan.FromMilliseconds(150));
            }
        }

        return null;
    }

    private void RequestNavigation(NavigationRequestedEventArgs args)
        => NavigationRequested?.Invoke(args);

    public event PropertyChangedEventHandler PropertyChanged;

    public ObservableCollection<NavigationButtonViewModel> SearchCommands { get; }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public void Dispose()
    {
        _hotkeyManager.UnregisterHotKey();
    }
}

public class NavigationRequestedEventArgs : EventArgs
{
    public string Url { get; init; }
    
    public string Page { get; init; }
    
    public bool IsFromHotkey { get; init; }
}

public class NavigationButtonViewModel
{
    private readonly Action<NavigationRequestedEventArgs> _action;
    private readonly Func<string> _query;
    private readonly SearchEngine _searchEngine;
    private readonly Regex _autoSearchRegex;

    public NavigationButtonViewModel(Action<NavigationRequestedEventArgs> action, Func<string> query,
        SearchEngine searchEngine)
    {
        _action = action;
        _query = query;
        _searchEngine = searchEngine;
        if (!string.IsNullOrEmpty(searchEngine.AutoSearchRegex))
        {
            _autoSearchRegex = new Regex(searchEngine.AutoSearchRegex);
        }

        IconFilePath = Path.Combine("icons", searchEngine.IconFileName);
        Command = new NavigateCommand(this);
    }
    
    public ICommand Command { get; }
    
    public string IconFilePath { get; }

    public string ToolTip => _searchEngine.Name;

    public bool CanAutoSearch(string query) => _autoSearchRegex != null && _autoSearchRegex.IsMatch(query);
    
    private class NavigateCommand(NavigationButtonViewModel parent) : ICommand
    {
        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter)
        {
            parent._action?.Invoke(new NavigationRequestedEventArgs
            {
                Url = string.Format(parent._searchEngine.UrlTemplate, parent._query()),
                IsFromHotkey = parameter is true
            });
            
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler CanExecuteChanged;
    }
}