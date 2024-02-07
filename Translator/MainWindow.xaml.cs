using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Translator.Blocklist;

namespace Translator;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _mainWindowViewModel;
    private readonly BlocklistManager _blocklistManager;
    private readonly JsSelector _jsSelector;
    private readonly ILogger<MainWindow> _logger;
    private readonly PopupSizeLocationProvider _popupSizeLocationProvider;

    public MainWindow(MainWindowViewModel mainWindowViewModel, PopupSizeLocationProvider popupSizeLocationProvider,
        BlocklistManager blocklistManager, JsSelector jsSelector, ILogger<MainWindow> logger)
    {
        _mainWindowViewModel = mainWindowViewModel;
        _popupSizeLocationProvider = popupSizeLocationProvider;

        _mainWindowViewModel.NavigationRequested += (args) =>
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
                    NavigateToBlankPage();
                    NavigateToUrl(args.Url);
                }
                else if (!string.IsNullOrEmpty(args.Page))
                {
                    NavigateToPage(args.Page);
                }
            });
        };

        _blocklistManager = blocklistManager;
        _jsSelector = jsSelector;
        _logger = logger;

        InitializeComponent();
        InitializeWebView2();

        DataContext = _mainWindowViewModel;

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

            WebBrowser.CoreWebView2.DOMContentLoaded += async (_, _) =>
            {
                var js = _jsSelector.SelectJs(WebBrowser.Source.AbsoluteUri);
                if (js != null)
                {
                    await WebBrowser.ExecuteScriptAsync(js);
                }
            };
        };
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
        Focus();
    }

    private void HideWindow()
    {
        if (IsVisible)
        {
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
        _mainWindowViewModel.TranslateFromClipboard();
    }
}