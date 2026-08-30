using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.ViewModels;
using XncOptimizerUI.MVVM.Views;
using XncOptimizerUI.Services;

namespace XncOptimizerUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider? _services;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                _services = ConfigureServices()
                    .BuildServiceProvider(new ServiceProviderOptions
                    {
                        ValidateOnBuild = true,
                        ValidateScopes = true
                    });

                var window = _services.GetRequiredService<MainWindow>();
                MainWindow = window;
                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{ex.Message}: {ex.InnerException?.Message}\n{ex.StackTrace}", "Startup error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private static IServiceCollection ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton<IConfigService, ConfigService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IProjectService, GibLabProjectService>();
            services.AddSingleton<AppViewModel>();
            services.AddSingleton<MainWindow>();

            return services;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _services?.Dispose();

            base.OnExit(e);
        }
    }
}
