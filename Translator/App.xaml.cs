using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
            var builder = Host.CreateApplicationBuilder();
            builder.Services.AddSingleton<MainWindow>();
            builder.Logging
                .ClearProviders()
                .AddSimpleConsole();

            builder.Services.Configure<ApplicationSettings>(
                builder.Configuration.GetSection(key: nameof(ApplicationSettings)));
            
            _host = builder.Build();
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
            }
        }
    }
}
