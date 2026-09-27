using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Hardcodet.Wpf.TaskbarNotification.Interop;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Translator.Blocklist;
using Translator.Configuration;
using Translator.Logging;

namespace Translator;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// How far below its place the page is parked while loading. Any distance past the window's height works.
    /// </summary>
    private const double ParkedPageOffset = 10000;

    private readonly MainWindowViewModel _mainWindowViewModel;
    private readonly BlocklistManager _blocklistManager;
    private readonly JavaScriptProvider _javaScriptProvider;
    private readonly ILogger<MainWindow> _logger;
    private readonly PopupSizeLocationProvider _popupSizeLocationProvider;
    private readonly IConfiguration _configuration;
    private ulong _currentNavigationId;

    public MainWindow(MainWindowViewModel mainWindowViewModel, PopupSizeLocationProvider popupSizeLocationProvider,
        BlocklistManager blocklistManager, JavaScriptProvider javaScriptProvider, IConfiguration configuration,
        ILogger<MainWindow> logger)
    {
        _mainWindowViewModel = mainWindowViewModel;
        _popupSizeLocationProvider = popupSizeLocationProvider;
        _blocklistManager = blocklistManager;
        _javaScriptProvider = javaScriptProvider;
        _configuration = configuration;
        _logger = logger;

        _mainWindowViewModel.NavigationRequested += OnNavigationRequested;

        // The tray icon library computes its DPI factor lazily, on first use, by creating a window, which pumps
        // messages. If that first use is a tray click, the next click message reads the factor mid-computation and
        // the menu opens in the wrong place. Computing it here, before InitializeComponent creates the tray icon,
        // keeps it out of the message handler.
        _ = SystemInfo.DpiFactorX;

        InitializeComponent();
        InitializeWebView2();

        // Stored in the window's own resources, which take precedence over Popup.xaml's fallback.
        var accentColor = AccentColor.TryGet();
        if (accentColor != null)
        {
            Resources["AccentBrush"] = new SolidColorBrush(accentColor.Value);
        }
        else
        {
            _logger.LogWarning("Unable to read the Windows accent colour; using the default blue");
        }

        DataContext = _mainWindowViewModel;

        SourceInitialized += (_, _) =>
        {
            if (WindowCorners.TryRound(this))
            {
                WindowBorder.BorderThickness = new Thickness(0);
            }
        };

        Top = 100000;
        Left = 100000;
        Loaded += (_, _) =>
        {
            HideWindow();
            _mainWindowViewModel.Init(this);
            Top = 0;
            Left = 0;
        };
    }

    private void OnNavigationRequested(NavigationRequestedEventArgs args)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (args.IsFromHotkey)
            {
                (Left, Top) = _popupSizeLocationProvider.GetLeftAndTop(this);
            }

            ShowWindow();
            if (!string.IsNullOrEmpty(args.Url))
            {
                NavigateToUrl(args.Url);
            }
            else if (!string.IsNullOrEmpty(args.Page))
            {
                NavigateToPage(args.Page);
            }
        });
    }

    private void InitializeWebView2()
    {
        WebBrowser.CoreWebView2InitializationCompleted += (_, initializationCompletedArgs) =>
        {
            if (!initializationCompletedArgs.IsSuccess)
            {
                _logger.LogError("CoreWebView2 initialization failed.");
                return;
            }

            WebBrowser.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            WebBrowser.CoreWebView2.WebResourceRequested += (_, webResourceRequestedArgs) =>
            {
                var requestUrl = webResourceRequestedArgs.Request.Uri;
                _logger.LogInformation("Requested {Url}", requestUrl);
                var blockResponse = _blocklistManager.TryBlock(requestUrl);
                if (blockResponse != null)
                {
                    _logger.LogInformation("Blocked");
                    webResourceRequestedArgs.Response = WebBrowser.CoreWebView2.Environment
                        .CreateWebResourceResponse(null, blockResponse.StatusCode, blockResponse.Reason, "");
                }
            };

            // The page stays hidden from the start of a navigation until the site script has removed the
            // site's own header and ads, so neither a blank page nor the uncleaned layout ever shows.
            WebBrowser.CoreWebView2.NavigationStarting += (_, navigationStartingArgs) =>
            {
                _currentNavigationId = navigationStartingArgs.NavigationId;
                HidePage();
            };

            WebBrowser.CoreWebView2.DOMContentLoaded += async (_, domContentLoadedArgs) =>
            {
                try
                {
                    var js = _javaScriptProvider.GetPostProcessingJavaScript(WebBrowser.Source.AbsoluteUri);
                    if (js != null)
                    {
                        await WebBrowser.ExecuteScriptAsync(js);
                    }
                }
                finally
                {
                    RevealPage(domContentLoadedArgs.NavigationId);
                }
            };

            // Covers navigations that fail or never reach DOMContentLoaded, so the page never stays hidden.
            WebBrowser.CoreWebView2.NavigationCompleted += (_, navigationCompletedArgs) =>
                RevealPage(navigationCompletedArgs.NavigationId);
        };
    }

    /// <summary>
    /// Parks the page below the window, where Windows clips it away, instead of hiding it. A hidden WebView2
    /// stops drawing, and when shown again its first frame is the page it last drew: the previous entry.
    /// A parked one keeps drawing, at the same size, so it's up to date when it comes back.
    /// </summary>
    private void HidePage()
    {
        WebBrowser.Margin = new Thickness(0, ParkedPageOffset, 0, -ParkedPageOffset);
        LoadingBar.Visibility = Visibility.Visible;
    }

    private void RevealPage(ulong navigationId)
    {
        // Switching engines cancels the previous navigation, and its NavigationCompleted can arrive after the
        // new one has started. Only the latest navigation may reveal the page.
        if (navigationId != _currentNavigationId)
        {
            return;
        }

        // HideWindow navigates to about:blank as the window goes away. Revealing that page would make the
        // next pop-up start with the page visible, so nothing is revealed while the window is hidden.
        if (!IsVisible)
        {
            return;
        }

        WebBrowser.Margin = new Thickness(0);
        LoadingBar.Visibility = Visibility.Hidden;
    }

    private void ShowWindow()
    {
        if (!IsVisible)
        {
            (Width, Height) = _popupSizeLocationProvider.GetWidthAndHeight();
            Show();
        }

        Activate();
        Topmost = true;
        Topmost = false;
        QueryTextBox.Focus();
    }

    private void HideWindow()
    {
        if (IsVisible)
        {
            HidePage();
            NavigateToBlankPage();
            Hide();
        }
    }

    private void NavigateToBlankPage()
    {
        NavigateToUrl("about:blank");
    }

    private void NavigateToUrl(string url)
    {
        Application.Current.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                await WebBrowser.EnsureCoreWebView2Async();
                WebBrowser.CoreWebView2.Settings.IsScriptEnabled = true;
                _logger.LogInformation("Navigating to {Url}", url);
                WebBrowser.CoreWebView2.Navigate(url);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to navigate to the URL {Url}", url);
            }
        });
    }

    private void NavigateToPage(string page)
    {
        Application.Current.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                await WebBrowser.EnsureCoreWebView2Async();
                WebBrowser.NavigateToString(page);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to navigate to a page.");
            }
        });
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideWindow();
        }
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        HideWindow();
    }

    private void MenuItemExit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void MenuItemTranslate_Click(object sender, RoutedEventArgs e)
    {
        _mainWindowViewModel.TranslateFromClipboard(fromHotKey: false);
    }

    private void MenuItemOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        string directoryPath = AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(directoryPath))
        {
            Process.Start("explorer.exe", directoryPath);
        }
        else
        {
            _logger.LogWarning("Unable to locate the directory of the current application.");
        }
    }

    private void MenuItemOpenSettingsFolder_Click(object sender, RoutedEventArgs e)
    {
        var directoryPath = Path.GetDirectoryName(UserSettingsFile.DefaultPath)!;
        try
        {
            // The folder is normally created at startup, but the user may have deleted it since.
            Directory.CreateDirectory(directoryPath);
            Process.Start("explorer.exe", directoryPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Unable to open the settings folder {Path}", directoryPath);
        }
    }

    private void MenuItemOpenLogsFolder_Click(object sender, RoutedEventArgs e)
    {
        var directoryPath = LogFolder.Find(_configuration);
        if (directoryPath == null)
        {
            _logger.LogWarning("Unable to open the logs folder: no File sink with a path is configured");
            return;
        }

        try
        {
            // Serilog creates the folder when it opens a log file, but it may not have done so yet.
            Directory.CreateDirectory(directoryPath);
            Process.Start("explorer.exe", directoryPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Unable to open the logs folder {Path}", directoryPath);
        }
    }

    private void QueryTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _mainWindowViewModel.DefaultSearchCommand.Command.Execute(null);
        }
    }
}