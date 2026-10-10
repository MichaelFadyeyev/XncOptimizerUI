using System.Windows.Controls;
using XncOptimizerUI.MVVM.ViewModels;
using XncOptimizerUI.MVVM.Views.PartPrograms;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// Drives the real bores <see cref="DataGrid"/> on <c>TestData/td-bores-displaying.project</c>:
    /// an X / Y / Depth cell edit must be saved however the user leaves the cell - Enter, a click
    /// elsewhere in the window, or keyboard focus moving to another control - and an invalid
    /// draft left that way is reverted rather than kept open.
    /// </summary>
    [TestFixture]
    public class BoresGridEditingUiTests : ProgramGridUiTestBase
    {
        private const int XColumn = 3;
        private const int YColumn = 4;
        private const int DepthColumn = 6;

        [TestCase(LeaveBy.Enter, DepthColumn, "5", "5", "Bore #1 dp = \"5\" saved")]
        [TestCase(LeaveBy.ClickOutside, XColumn, "dx-32", "dx-32", "Bore #1 x = \"dx-32\" saved")]
        [TestCase(LeaveBy.FocusOutside, YColumn, "dy-7", "dy-7", "Bore #1 y = \"dy-7\" saved")]
        [TestCase(LeaveBy.ClickOutside, XColumn, "2*-3", "150", null)]
        public void LeavingEditedCell_SavesValidDraftOrRevertsInvalidOne(
            LeaveBy leave, int column, string typed, string expectedInput, string? expectedLog)
        {
            var vm = OpenProject("td-bores-displaying.project");
            var (window, grid, outside) = Show(new SelectedPartBoresGrid(), vm, "BoresDataGrid");
            var row = vm.SelectedPartBores[0];

            try
            {
                TypeIntoCell(grid, row, column, typed);
                Leave(leave, grid, outside);

                Assert.Multiple(() =>
                {
                    Assert.That(InputOf(row, column), Is.EqualTo(expectedInput));
                    Assert.That(FindDescendant<TextBox>(grid), Is.Null, "cell left edit mode");
                    Assert.That(vm.Log, expectedLog is null ? Does.Not.Contain("Bore #") : Does.Contain(expectedLog));
                });
            }
            finally
            {
                window.Close();
            }
        }

        private static string InputOf(BoreRowVM row, int column) => column switch
        {
            XColumn => row.XInput,
            YColumn => row.YInput,
            _ => row.DepthInput
        };
    }
}
