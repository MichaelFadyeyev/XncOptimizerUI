using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.ViewModels;
using XncOptimizerUI.MVVM.ViewModels.Panels;
using XncOptimizerUI.MVVM.ViewModels.Parts;
using XncOptimizerUI.Services.Project;

namespace XncOptimizerUI.Test
{
    [TestFixture]
    public class ServiceRegistrationTests
    {
        private static ServiceProvider BuildProvider()
        {
            var services = new ServiceCollection();
            services.AddSingleton(Substitute.For<IConfigService>());
            services.AddSingleton(TimeProvider.System);
            App.AddProjectServices(services);

            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        }

        [Test]
        public void ProjectServices_ResolveAndShareOneDocument()
        {
            using var provider = BuildProvider();

            var project = provider.GetRequiredService<ProjectServices>();
            var document = provider.GetRequiredService<ProjectDocument>();

            Assert.Multiple(() =>
            {
                Assert.That(project.Session, Is.SameAs(document), "the session is the shared document");
                Assert.That(project.Reader, Is.SameAs(provider.GetRequiredService<ProjectReader>()));
                Assert.That(project.Editor, Is.Not.Null);
                Assert.That(project.Restructuring, Is.Not.Null);
                Assert.That(project.Batch, Is.Not.Null);
            });
        }

        [Test]
        public void ViewModels_ResolveAndShareTheLogAndTheParts()
        {
            var services = new ServiceCollection();
            services.AddSingleton(Substitute.For<IConfigService>());
            services.AddSingleton(Substitute.For<IDialogService>());
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton(new ApplicationInfo("Test"));
            App.AddProjectServices(services);
            App.AddViewModels(services);

            using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

            var vm = provider.GetRequiredService<AppViewModel>();

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log, Is.SameAs(provider.GetRequiredService<ActivityLog>()));
                Assert.That(vm.PartsList, Is.SameAs(provider.GetRequiredService<PartsListVM>()));
                Assert.That(vm.Workspace, Is.SameAs(provider.GetRequiredService<ProjectWorkspace>()));
                Assert.That(vm.Preparation.Labels, Is.SameAs(provider.GetRequiredService<LabelsVM>()));
            });
        }
    }
}
