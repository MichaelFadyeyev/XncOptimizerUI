using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using XncOptimizerUI.MVVM.ViewModels;
using XncOptimizerUI.MVVM.Views;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// Shows the whole <see cref="MainWindow"/> (off-screen) on an opened project and visits every
    /// tab, so each panel, grid and control realizes its bindings; the base class fails the test
    /// on any WPF binding error - the only symptom of a mistyped path or a wrong DataContext.
    /// </summary>
    [TestFixture]
    public class MainWindowUiTests : ProgramGridUiTestBase
    {
        [TestCase("td-bores-displaying.project")]
        [TestCase("td-grooving-mixed.project")]
        [TestCase("td-mill-offset.project")]
        [TestCase("td-mill-ellipse.project")]
        [TestCase("td-milling-pocket.project")]
        [TestCase("td-bore-depth-variable.project")]
        public void EveryTab_BindsWithoutErrors(string fixture)
        {
            var vm = OpenProject(fixture);
            var window = ShowMainWindow(vm);

            try
            {
                VisitEveryTab(window);

                Assert.That(vm.PartsList.SelectedPart, Is.Not.Null);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void ClosedProject_BindsWithoutErrors()
        {
            var vm = OpenProject("td-bores-displaying.project");
            vm.CloseFileCommand.Execute(null);
            var window = ShowMainWindow(vm);

            try
            {
                VisitEveryTab(window);

                Assert.That(vm.PartsList.Parts, Is.Empty);
            }
            finally
            {
                window.Close();
            }
        }

        private static MainWindow ShowMainWindow(AppViewModel vm)
        {
            var window = new MainWindow(vm) { Left = -4000, ShowInTaskbar = false };
            window.Show();
            Pump();

            return window;
        }

        /// <summary>Selects each item of every tab control, nested ones included (a tab realizes its content only when selected).</summary>
        private static void VisitEveryTab(DependencyObject root)
        {
            foreach (var tabs in Descendants<TabControl>(root).ToList())
            {
                for (var i = 0; i < tabs.Items.Count; i++)
                {
                    tabs.SelectedIndex = i;
                    Pump();

                    VisitEveryTab(tabs);
                }

                tabs.SelectedIndex = 0;
                Pump();
            }
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);

                if (child is T match)
                {
                    yield return match;
                    continue;
                }

                foreach (var found in Descendants<T>(child))
                {
                    yield return found;
                }
            }
        }
    }
}
