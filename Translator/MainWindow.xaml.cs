using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Translator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly HotkeyManager _hotkeyManager;
        private readonly WindowLocationManager _windowLocationManager;
        private readonly MultitranLoader _multitranLoader;
        private CancellationTokenSource _loadingCts;

        public MainWindow()
        {
            InitializeComponent();
            _hotkeyManager = new HotkeyManager(this, TranslateFromClipboard);
            _windowLocationManager = new WindowLocationManager(this);
            _multitranLoader = new MultitranLoader();
            _loadingCts = new CancellationTokenSource();

            Top = 100000;
            Left = 100000;
            Loaded += (s, e) =>
            {
                HideWindow();
                Top = 0;
                Left = 0;
            };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _hotkeyManager.RegisterHotKey();
        }

        protected override void OnClosed(EventArgs e)
        {
            _hotkeyManager.UnregisterHotKey();
            base.OnClosed(e);
        }

        private void TranslateFromClipboard()
        {
            (Left, Top) = _windowLocationManager.GetLeftAndTop();

            var text = Clipboard.GetText();
            QueryTextBox.Text = text;

            LoadingIndicator.Visibility = Visibility.Visible;
            WebBrowser.Visibility = Visibility.Collapsed;

            ShowWindow();

            TranslateFromText(text);

            LoadingIndicator.Visibility = Visibility.Collapsed;
            WebBrowser.Visibility = Visibility.Visible;
        }

        private void TranslateFromText(string text)
        {
            _loadingCts = new CancellationTokenSource();
            var dispatcher = Application.Current.Dispatcher;
            Task.Run(async () =>
            {
                if (!_loadingCts.IsCancellationRequested)
                {

                }

                var page = await _multitranLoader.LoadAsync(text, _loadingCts.Token);
                Action a = () =>
                {
                    if (!_loadingCts.IsCancellationRequested)
                    {
                        NavigateToString(page);
                    }
                };
                await dispatcher.BeginInvoke(a, null);
            });
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
            try
            {
                WebBrowser.Navigate(url);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to navigate to URL {url}.{Environment.NewLine}{e}");
            }
        }

        private void NavigateToString(string page)
        {
            try
            {
                WebBrowser.NavigateToString(page);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to navigate to page.{Environment.NewLine}{e}");
            }
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

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            var text = QueryTextBox.Text;
            TranslateFromText(text);
        }

        private void MenuItemTranslate_Click(object sender, RoutedEventArgs e)
        {
            TranslateFromClipboard();
        }
    }
}