using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Web.WebView2.Core;
using Translator.Configuration;

namespace Translator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ILogger<MainWindow> _logger;
        private readonly HotkeyManager _hotkeyManager;
        private readonly WindowLocationManager _windowLocationManager;
        private readonly HashSet<string> _blacklist = new();
        private CancellationTokenSource _loadingCts = new();
        private readonly PageBuilder _pageBuilder = new();

        public MainWindow(ILogger<MainWindow> logger, IOptions<ApplicationSettings> applicationSettings)
        {
            _logger = logger;
            InitializeComponent();
            _hotkeyManager = new HotkeyManager(this, TranslateFromClipboard);
            _windowLocationManager = new WindowLocationManager(this);

            Top = 100000;
            Left = 100000;
            Loaded += (s, e) =>
            {
                HideWindow();
                Top = 0;
                Left = 0;
            };
            
            InitializeWebView2();

            foreach (var file in Directory.EnumerateFiles("Blocklist", "*.txt"))
            {
                foreach (var line in File.ReadLines(file))
                {
                    if (line.StartsWith('#'))
                        continue;
                    var address = line;
                    if (!file.EndsWith("my.txt"))
                        address = address.Split(' ')[1];
                    _blacklist.Add(address);
                }
            }
        }
        
        private void InitializeWebView2()
        {
            // Ensure CoreWebView2 is initialized, this might be part of your initialization logic
            WebBrowser.CoreWebView2InitializationCompleted += (sender, args) =>
            {
                if (args.IsSuccess)
                {
                    // Add the WebResourceRequested event handler
                    WebBrowser.CoreWebView2.WebResourceRequested += CoreWebView2_WebResourceRequested;
            
                    // Specify the resource context types for which the event handler is invoked
                    // For example, to filter for all requests, use CoreWebView2WebResourceContext.All
                    WebBrowser.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                    
                    WebBrowser.CoreWebView2.DOMContentLoaded += CoreWebView2OnDOMContentLoaded;
                }
                else
                {
                    // Handle the error, initialization failed
                }
            };
        }

        private async void CoreWebView2OnDOMContentLoaded(object sender, CoreWebView2DOMContentLoadedEventArgs e)
        {
            if (WebBrowser.Source.AbsoluteUri.Contains("oxford"))
            {
                await WebBrowser.ExecuteScriptAsync(
                    "document.getElementById(\"searchbar\").remove(); " +
                    "document.getElementById(\"ox-header\").remove(); " +
                    "document.getElementById(\"topslot_container\").remove();");
            }
        }

        private void CoreWebView2_WebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            // Check the request URL and decide whether to block it
            string requestUrl = e.Request.Uri;
            _logger.LogInformation(requestUrl);
            if (ShouldBlockRequest(requestUrl))
            {
                // To block the request, set the Response to a new response with an appropriate status code
                // For example, 404 Not Found or 204 No Content
                _logger.LogInformation("Blocked");
                e.Response = WebBrowser.CoreWebView2.Environment.CreateWebResourceResponse(null, 204, "Not Found", "");
            }
        }

        private bool ShouldBlockRequest(string url)
        {
            // Implement your blocking logic here
            // For example, block all requests to a specific domain
            //return url.Contains(".ru") || url.Contains("amazon") || url.Contains("google-analytics");
            var uri = new Uri(url);
            var host = uri.Host;
            string[] hostParts = host.Split('.');
            string domain = hostParts.Length >= 2
                ? string.Join(".", hostParts[hostParts.Length - 2], hostParts[hostParts.Length - 1])
                : host;
            return _blacklist.Contains(domain);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _hotkeyManager.RegisterHotKey();
        }

        protected override void OnClosed(EventArgs e)
        {
            _hotkeyManager.UnregisterHotKey();
            ToastNotificationManagerCompat.History.Clear();
            base.OnClosed(e);
        }

        private void TranslateFromClipboard()
        {
            if (!NotificationStateChecker.AreNotificationsAllowed())
            {
                _logger.LogInformation("Notifications are disabled");
                return;
            }

            (Left, Top) = _windowLocationManager.GetLeftAndTop();

            var text = Clipboard.GetText().Trim();
            
            QueryTextBox.Text = text;

            LoadingIndicator.Visibility = Visibility.Visible;
            WebBrowser.Visibility = Visibility.Collapsed;

            ShowWindow();

            if (text.All(char.IsLetter))
            {
                //TranslateFromText(text, _oxfordLoader);
                NavigateToLoading();
                NavigateToUrl($"https://www.oxfordlearnersdictionaries.com/search/english/?q={text}");
            }
            else
            {
                NavigateToPage(_pageBuilder.MakeInfoPage("Click a button if you really want to look up for this text."));
            }

            LoadingIndicator.Visibility = Visibility.Collapsed;
            WebBrowser.Visibility = Visibility.Visible;
        }

        private void ShowWindow()
        {
            if (!IsVisible)
            {
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
                _loadingCts.Cancel();
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
                    _logger.LogInformation($"Navigating to {url}");
                    WebBrowser.CoreWebView2.Navigate(url);
                }
                catch (Exception e)
                {
                    _logger.LogError($"Failed to navigate to URL {url}.{Environment.NewLine}{e}");
                }
            });
        }

        private void NavigateToLoading()
        {
            NavigateToPage(_pageBuilder.MakeInfoPage("Loading"));
        }
        
        private void NavigateToPage(string page)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    WebBrowser.NavigateToString(page);
                }
                catch (Exception e)
                {
                    _logger.LogError($"Failed to navigate to page.{Environment.NewLine}{e}");
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
            _loadingCts.Cancel();
            Application.Current.Shutdown();
        }
        
        private void OxfordSearchButton_Click(object sender, RoutedEventArgs e)
        {
            var text = QueryTextBox.Text;
            NavigateToLoading();
            NavigateToUrl($"https://www.oxfordlearnersdictionaries.com/search/english/?q={text}");
        }
        
        private void MultitranSearchButton_Click(object sender, RoutedEventArgs e)
        {
            var text = QueryTextBox.Text;
            NavigateToLoading();
            NavigateToUrl($"https://www.multitran.com/m.exe?l1=1&l2=2&s={text}");
        }

        private void MenuItemTranslate_Click(object sender, RoutedEventArgs e)
        {
            TranslateFromClipboard();
        }

        private async void WebBrowser_OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (WebBrowser.Source.AbsoluteUri.Contains("oxford"))
            {
                await WebBrowser.ExecuteScriptAsync("document.getElementById(\"searchbar\").remove()");
            }
        }

        private void DeeplSearchButton_OnClick(object sender, RoutedEventArgs e)
        {
            var text = QueryTextBox.Text;
            NavigateToLoading();
            NavigateToUrl($"https://www.deepl.com/translator#en/ru/{text}");
        }
    }
}