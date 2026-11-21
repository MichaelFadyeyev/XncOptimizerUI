using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.MVVM.ViewModels.Machining;
using XncOptimizerUI.MVVM.Views.PartPrograms;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// Drives the real bores <see cref="DataGrid"/> on <c>TestData/td-bores-displaying.project</c>:
    /// its selected rows and the rows' <see cref="ISelectableRow.IsSelected"/> (the part
    /// preview's selection) follow each other both ways (<c>DataGridRowSelectionBehavior</c>); and the
    /// bores table's Delete button in the real <see cref="SelectedPartProgramsTabs"/>.
    /// </summary>
    [TestFixture]
    public class MachiningGridSelectionUiTests : ProgramGridUiTestBase
    {
        [Test]
        public void SelectingRowsInTheGrid_SelectsExactlyThem()
        {
            var vm = OpenProject("td-bores-displaying.project");
            var (window, grid, _) = Show(new SelectedPartBoresGrid(), vm, "BoresDataGrid");
            var rows = vm.Machining.Bores;

            try
            {
                grid.SelectedItems.Add(rows[0]);
                grid.SelectedItems.Add(rows[1]);
                Pump();
                var both = rows.Select(row => row.IsSelected).ToList();

                grid.SelectedItem = rows[1];
                Pump();

                Assert.Multiple(() =>
                {
                    Assert.That(both.Take(2), Is.All.True);
                    Assert.That(rows.Where(row => row.IsSelected), Is.EqualTo(new[] { rows[1] }));
                });
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void SelectingFromThePreview_SelectsTheRowsInTheGrid()
        {
            var vm = OpenProject("td-bores-displaying.project");
            var (window, grid, _) = Show(new SelectedPartBoresGrid(), vm, "BoresDataGrid");
            var rows = vm.Machining.Bores;

            try
            {
                vm.Preview.SelectElement(rows[1].Model, ElementSelectionMode.Replace);
                Pump();
                var single = grid.SelectedItems.Cast<object>().ToList();

                vm.Preview.SelectElement(rows[0].Model, ElementSelectionMode.Toggle);
                Pump();
                var added = grid.SelectedItems.Cast<object>().ToList();

                vm.Preview.ClearSelection();
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
                    Assert.That(rows.Where(row => row.IsSelected), Is.EqualTo(new[] { rows[1] }));
                    Assert.That(grid.SelectedItems, Is.EqualTo(new object[] { rows[1] }));
                });
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void DeleteButton_DeletesTheSelectedBores_OtherTablesKeepTheirSelectedRows()
        {
            var vm = OpenProject("td-gr-ml-conversion.project");
            var tabs = new SelectedPartProgramsTabs();
            var (window, _) = Show(tabs, vm);
            var boresGrid = InnerGrid(tabs, "SelectedPartBoresGrid", "BoresDataGrid");
            var groovesGrid = InnerGrid(tabs, "SelectedPartGroovesGrid", "GroovesDataGrid");
            var deleteBores = (Button)tabs.FindName("DeleteSelectedBoresButton");
            var boresBefore = vm.Machining.Bores.Count;
            Assume.That(boresBefore, Is.GreaterThanOrEqualTo(2));
            Assume.That(vm.Machining.Grooves, Has.Count.GreaterThanOrEqualTo(2));

            try
            {
                var enabledWithoutSelection = deleteBores.IsEnabled;
                boresGrid.SelectedItems.Add(vm.Machining.Bores[0]);
                groovesGrid.SelectedItems.Add(vm.Machining.Grooves[1]);
                Pump();

                ((IInvokeProvider)new ButtonAutomationPeer(deleteBores).GetPattern(PatternInterface.Invoke)).Invoke();
                Pump();

                var grooves = vm.Machining.Grooves;
                Assert.Multiple(() =>
                {
                    Assert.That(enabledWithoutSelection, Is.False);
                    Assert.That(vm.Machining.Bores, Has.Count.EqualTo(boresBefore - 1));
                    Assert.That(boresGrid.SelectedItems, Is.Empty);
                    Assert.That(vm.Machining.Bores.Any(row => row.IsSelected), Is.False);
                    Assert.That(deleteBores.IsEnabled, Is.False);
                    Assert.That(groovesGrid.ItemsSource, Is.SameAs(grooves), "rows rebuilt");
                    Assert.That(groovesGrid.SelectedItems, Is.EqualTo(new object[] { grooves[1] }));
                    Assert.That(vm.Machining.IsSelected(grooves[1].Model), Is.True);
                });
            }
            finally
            {
                window.Close();
            }
        }

        /// <summary>The <c>DataGrid</c> <paramref name="gridName"/> of the table control <paramref name="controlName"/> in <paramref name="tabs"/>.</summary>
        private static DataGrid InnerGrid(SelectedPartProgramsTabs tabs, string controlName, string gridName) =>
            (DataGrid)((UserControl)tabs.FindName(controlName)).FindName(gridName);
    }
}
