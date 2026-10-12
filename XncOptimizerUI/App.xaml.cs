using System.Reflection;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.ViewModels;
using XncOptimizerUI.MVVM.ViewModels.Machining;
using XncOptimizerUI.MVVM.ViewModels.Panels;
using XncOptimizerUI.MVVM.ViewModels.Parts;
using XncOptimizerUI.MVVM.ViewModels.Preview;
using XncOptimizerUI.MVVM.Views;
using XncOptimizerUI.Services;
using XncOptimizerUI.Services.Project;
using XncOptimizerUI.Services.Xnc.Batch;

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

            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<IConfigService, ConfigService>();
            services.AddSingleton<IDialogService, DialogService>();
            AddProjectServices(services);
            services.AddSingleton(new ApplicationInfo(Assembly.GetExecutingAssembly().GetName().Name ?? string.Empty));
            AddViewModels(services);
            services.AddSingleton<MainWindow>();

            return services;
        }

        /// <summary>Every project service works on the one shared <see cref="ProjectDocument"/>.</summary>
        internal static void AddProjectServices(IServiceCollection services)
        {
            services.AddSingleton<ProjectDocument>();
            services.AddSingleton<IProjectSession>(sp => sp.GetRequiredService<ProjectDocument>());
            services.AddSingleton<ProjectReader>();
            services.AddSingleton<IProjectReader>(sp => sp.GetRequiredService<ProjectReader>());
            services.AddSingleton<IProjectEditor, ProjectEditor>();
            services.AddSingleton<IProjectRestructuring, ProjectRestructuring>();
            services.AddSingleton<IXncBatchOperations, XncBatchOperations>();
            services.AddSingleton<ProjectServices>();
        }

        /// <summary>
        /// The main window's view models: one instance of each, sharing the log, the open
        /// project and the parts list.
        /// </summary>
        internal static void AddViewModels(IServiceCollection services)
        {
            services.AddSingleton<ActivityLog>();
            services.AddSingleton<PartEditSaver>();
            services.AddSingleton<ProjectCatalogVM>();
            services.AddSingleton<PartsListVM>();
            services.AddSingleton<PartsFilterVM>();
            services.AddSingleton<ProjectWorkspace>();
            services.AddSingleton<SelectedPartMachiningVM>();
            services.AddSingleton<PartPreviewVM>();
            services.AddSingleton<BatchCommandRunner>();
            services.AddSingleton<LabelsVM>();
            services.AddSingleton<SourcePartVM>();
            services.AddSingleton<PreparationPanelVM>();
            services.AddSingleton<CleanOutsidePanelVM>();
            services.AddSingleton<SortToolsPanelVM>();
            services.AddSingleton<GrooveMillPanelVM>();
            services.AddSingleton<BoreMillPanelVM>();
            services.AddSingleton<MillOffsetPanelVM>();
            services.AddSingleton<MillOrderPanelVM>();
            services.AddSingleton<AppViewModel>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _services?.Dispose();

            base.OnExit(e);
        }
    }
}
