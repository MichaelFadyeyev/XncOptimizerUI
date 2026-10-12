using System.Windows;
using System.Windows.Controls;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// The Parts grid band select (<c>BandSelectComboBoxStyle</c>) finds its choices through the
    /// grid's <c>DataContext</c>, the <see cref="MVVM.ViewModels.AppViewModel"/>; a wrong path
    /// only shows up as a binding error in the output window and an empty drop-down.
    /// </summary>
    [TestFixture]
    public class BandSelectComboBoxStyleUiTests : ProgramGridUiTestBase
    {
        [Test]
        public void BandSelect_ListsTheCatalogBandOptions()
        {
            var vm = OpenProject("td-bores-displaying.project");
            var grid = GridOfOneBandSelect(vm.PartsList.Parts[0]);
            grid.DataContext = vm;

            var window = new Window { Content = grid, Width = 400, Height = 200, Left = -2000, ShowInTaskbar = false };

            try
            {
                window.Show();
                Pump();

                Assert.That(FindDescendant<ComboBox>(grid)!.ItemsSource, Is.SameAs(vm.Catalog.BandOptions));
            }
            finally
            {
                window.Close();
            }
        }

        /// <summary>A grid showing <paramref name="row"/> with one cell: a band select.</summary>
        private static DataGrid GridOfOneBandSelect(object row)
        {
            var comboBox = new FrameworkElementFactory(typeof(ComboBox));
            comboBox.SetValue(FrameworkElement.StyleProperty, Application.Current.FindResource("BandSelectComboBoxStyle"));

            var grid = new DataGrid { AutoGenerateColumns = false, ItemsSource = new[] { row } };
            grid.Columns.Add(new DataGridTemplateColumn { CellTemplate = new DataTemplate { VisualTree = comboBox } });

            return grid;
        }
    }
}
