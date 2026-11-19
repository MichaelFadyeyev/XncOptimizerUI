using System.Windows.Controls;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.MVVM.ViewModels.Machining;
using XncOptimizerUI.MVVM.Views.PartPrograms;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// Drives the real bores <see cref="DataGrid"/> on <c>TestData/td-bores-displaying.project</c>:
    /// its selected rows and the rows' <see cref="IHighlightableRow.IsHighlighted"/> (the part
    /// preview's selection) follow each other both ways (<c>DataGridHighlightBehavior</c>).
    /// </summary>
    [TestFixture]
    public class MachiningGridHighlightUiTests : ProgramGridUiTestBase
    {
        [Test]
        public void SelectingRowsInTheGrid_HighlightsExactlyThem()
        {
            var vm = OpenProject("td-bores-displaying.project");
            var (window, grid, _) = Show(new SelectedPartBoresGrid(), vm, "BoresDataGrid");
            var rows = vm.Machining.Bores;

            try
            {
                grid.SelectedItems.Add(rows[0]);
                grid.SelectedItems.Add(rows[1]);
                Pump();
                var both = rows.Select(row => row.IsHighlighted).ToList();

                grid.SelectedItem = rows[1];
                Pump();

                Assert.Multiple(() =>
                {
                    Assert.That(both.Take(2), Is.All.True);
                    Assert.That(rows.Where(row => row.IsHighlighted), Is.EqualTo(new[] { rows[1] }));
                });
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void HighlightingFromThePreview_SelectsTheRowsInTheGrid()
        {
            var vm = OpenProject("td-bores-displaying.project");
            var (window, grid, _) = Show(new SelectedPartBoresGrid(), vm, "BoresDataGrid");
            var rows = vm.Machining.Bores;

            try
            {
                vm.Preview.HighlightElement(rows[1].Model, HighlightMode.Replace);
                Pump();
                var single = grid.SelectedItems.Cast<object>().ToList();

                vm.Preview.HighlightElement(rows[0].Model, HighlightMode.Toggle);
                Pump();
                var added = grid.SelectedItems.Cast<object>().ToList();

                vm.Preview.ClearHighlight();
                Pump();

                Assert.Multiple(() =>
                {
                    Assert.That(single, Is.EqualTo(new object[] { rows[1] }));
                    Assert.That(added, Is.EquivalentTo(new object[] { rows[0], rows[1] }));
                    Assert.That(grid.SelectedItems, Is.Empty);
                });
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void RebuildingTheRowsOfTheSamePart_KeepsTheirSelectionInTheGrid()
        {
            var vm = OpenProject("td-bores-displaying.project");
            var (window, grid, _) = Show(new SelectedPartBoresGrid(), vm, "BoresDataGrid");

            try
            {
                grid.SelectedItem = vm.Machining.Bores[1];
                Pump();

                vm.Preview.Rotation = PartTurn.Deg90;
                Pump();

                var rows = vm.Machining.Bores;
                Assert.Multiple(() =>
                {
                    Assert.That(grid.ItemsSource, Is.SameAs(rows), "rows rebuilt");
                    Assert.That(rows.Where(row => row.IsHighlighted), Is.EqualTo(new[] { rows[1] }));
                    Assert.That(grid.SelectedItems, Is.EqualTo(new object[] { rows[1] }));
                });
            }
            finally
            {
                window.Close();
            }
        }
    }
}
