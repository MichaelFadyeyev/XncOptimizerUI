using XncOptimizerUI.Contracts;
using XncOptimizerUI.Services.Project;
using XncOptimizerUI.Services.Xnc.Batch;

namespace XncOptimizerUI.Test
{
    /// <summary>The real project services, wired as in <c>App.xaml.cs</c>, for tests on fixture files.</summary>
    internal static class TestProject
    {
        public static ProjectServices Create(IConfigService config, TimeProvider clock)
        {
            var document = new ProjectDocument(clock);
            var reader = new ProjectReader(document);

            return new ProjectServices(
                document,
                reader,
                new ProjectEditor(document, reader),
                new ProjectRestructuring(document, config),
                new XncBatchOperations(document, config));
        }

        /// <summary>Appends the result's log text to <paramref name="log"/> and returns whether it changed the project.</summary>
        public static bool Into(this OperationResult result, ref string log)
        {
            log += result.LogText;

            return result.Changed;
        }
    }
}
