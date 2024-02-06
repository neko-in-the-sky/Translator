using System;
using System.Globalization;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Toolkit.Uwp.Notifications;
using Serilog;
using Translator.Blocklist;
using Translator.Configuration;

namespace Translator
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly IHost _host;

        public App()
        {
            try
            {
                Directory.SetCurrentDirectory(AppContext.BaseDirectory);
                
                var builder = Host.CreateApplicationBuilder();

                builder.Services
                    .AddSingleton<HotkeyManager>()
                    .AddSingleton<PageBuilder>()
                    .AddSingleton<NotificationStateChecker>()
                    .AddSingleton<BlocklistManager>()
                    .AddSingleton<JsSelector>()
                    .AddSingleton<MainWindowViewModel>()
                    .AddSingleton<PopupVisualManager>()
                    .AddSingleton<MainWindow>();

                Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(builder.Configuration)
                    .CreateLogger();
                builder.Logging.ClearProviders();
                builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));

                builder.Services.Configure<ApplicationSettings>(
                    builder.Configuration.GetSection(key: nameof(ApplicationSettings)));

                _host = builder.Build();
                
                Application.Current.DispatcherUnhandledException += (_, args) =>
                {
                    Log.Logger.Error(args.Exception, "Unhandled exception");
                };

                CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(
                    _host.Services.GetRequiredService<IOptions<ApplicationSettings>>().Value.Culture);
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.ToString());
                Application.Current.Shutdown(-1);
            }
        }

        private async void App_OnStartup(object sender, StartupEventArgs e)
        {
            await _host.StartAsync();
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private async void App_OnExit(object sender, ExitEventArgs e)
        {
            using (_host)
            {
                await _host.StopAsync();
                ToastNotificationManagerCompat.History.Clear();
            }
        }
    }
}
