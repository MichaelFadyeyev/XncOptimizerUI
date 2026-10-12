using Microsoft.Extensions.DependencyInjection;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.ViewModels;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// Builds the main window's view models the way the app does (<see cref="App.AddViewModels"/>),
    /// over the given project services, config, dialogs and clock.
    /// </summary>
    internal static class TestViewModels
    {
        public static AppViewModel Create(
            ProjectServices project,
            IConfigService config,
            IDialogService dialogs,
            TimeProvider? clock = null,
            string applicationName = "TestAssembly")
        {
            var services = new ServiceCollection();

            services.AddSingleton(project);
            services.AddSingleton(config);
            services.AddSingleton(dialogs);
            services.AddSingleton(clock ?? TimeProvider.System);
            services.AddSingleton(new ApplicationInfo(applicationName));
            App.AddViewModels(services);

            return services
                .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true })
                .GetRequiredService<AppViewModel>();
        }
    }
}
