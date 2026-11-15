using System.Collections;
using System.Windows.Controls;
using XncOptimizerUI.MVVM.ViewModels;
using XncOptimizerUI.MVVM.ViewModels.Machining;
using XncOptimizerUI.MVVM.Views.PartPrograms;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// Drives the real grooves and mill <see cref="DataGrid"/>s like <see cref="BoresGridEditingUiTests"/>:
    /// an edited numeric cell is saved however the user leaves it - Enter, a click elsewhere in the
    /// window, or keyboard focus moving to another control - and an invalid draft is reverted.
    /// Fixtures: <c>TestData/td-programs.project</c> (grooves, contours, rectangle) and
    /// <c>TestData/td-mill-ellipse.project</c> (ellipse).
    /// </summary>
    [TestFixture]
    public class GroovesAndMillsGridEditingUiTests : ProgramGridUiTestBase
    {
        /// <summary>One table: how to show it and where its rows and the edited cell are.</summary>
        public sealed record Table(
            string Name,
            string Fixture,
            Func<UserControl> CreateControl,
            string GridName,
            Func<AppViewModel, IList> Rows,
            int Column,
            Func<object, ExpressionCellVM> Cell,
            string LogPrefix)
        {
            public override string ToString() => Name;
        }

        private static readonly Table Grooves = new(
            "Grooves end Y", "td-programs.project", () => new SelectedPartGroovesGrid(), "GroovesDataGrid",
            vm => vm.Machining.Grooves, 6, row => ((GrooveRowVM)row).EndY, "Groove #1 y2");

        private static readonly Table Contours = new(
            "Contours depth", "td-programs.project", () => new MillingContoursGrid(), "MillingContoursDataGrid",
            vm => vm.Machining.MillingContours, 7, row => ((MillingContourRowVM)row).Depth, "Contour #1 dp");

        private static readonly Table Rectangles = new(
            "Rectangles length", "td-programs.project", () => new MillingRectanglesGrid(), "MillingRectanglesDataGrid",
            vm => vm.Machining.MillingRectangles, 6, row => ((MillingRectangleRowVM)row).Length, "Rectangle #1 l");

        private static readonly Table Ellipses = new(
            "Ellipses angle", "td-mill-ellipse.project", () => new MillingEllipsesGrid(), "MillingEllipsesDataGrid",
            vm => vm.Machining.MillingEllipses, 8, row => ((MillingEllipseRowVM)row).Angle, "Ellipse #1 a");

        private static IEnumerable<TestCaseData> ValidEdits()
        {
            yield return new TestCaseData(Grooves, LeaveBy.Enter, "dy-35");
            yield return new TestCaseData(Contours, LeaveBy.ClickOutside, "dz/2");
            yield return new TestCaseData(Rectangles, LeaveBy.FocusOutside, "120");
            yield return new TestCaseData(Ellipses, LeaveBy.Enter, "30");
        }

        private static IEnumerable<TestCaseData> InvalidEdits()
        {
            yield return new TestCaseData(Grooves, "2*-3");
            yield return new TestCaseData(Contours, "0");
            yield return new TestCaseData(Rectangles, "-5");
            yield return new TestCaseData(Ellipses, "unknownVar");
        }

        [TestCaseSource(nameof(ValidEdits))]
        public void LeavingEditedCell_SavesValidDraft(Table table, LeaveBy leave, string typed)
        {
            var vm = OpenProject(table.Fixture);
            var (window, grid, outside) = Show(table.CreateControl(), vm, table.GridName);
            var row = table.Rows(vm)[0]!;

            try
            {
                TypeIntoCell(grid, row, table.Column, typed);
                Leave(leave, grid, outside);

                Assert.Multiple(() =>
                {
                    Assert.That(table.Cell(row).Input, Is.EqualTo(typed));
                    Assert.That(FindDescendant<TextBox>(grid), Is.Null, "cell left edit mode");
                    Assert.That(vm.Log.Text, Does.Contain($"{table.LogPrefix} = \"{typed}\", saved"));
                    Assert.That(table.Rows(vm)[0], Is.SameAs(row), "row refreshed in place");
                });
            }
            finally
            {
                window.Close();
            }
        }

        [TestCaseSource(nameof(InvalidEdits))]
        public void ClickingOutsideInvalidDraft_RevertsIt(Table table, string typed)
        {
            var vm = OpenProject(table.Fixture);
            var (window, grid, outside) = Show(table.CreateControl(), vm, table.GridName);
            var row = table.Rows(vm)[0]!;
            var committed = table.Cell(row).Committed;

            try
            {
                TypeIntoCell(grid, row, table.Column, typed);
                Assert.That(table.Cell(row).HasErrors, Is.True, "draft flagged invalid");

                Leave(LeaveBy.ClickOutside, grid, outside);

                Assert.Multiple(() =>
                {
                    Assert.That(table.Cell(row).Input, Is.EqualTo(committed));
                    Assert.That(table.Cell(row).HasErrors, Is.False);
                    Assert.That(FindDescendant<TextBox>(grid), Is.Null, "cell left edit mode");
                    Assert.That(vm.Log.Text, Does.Not.Contain(table.LogPrefix));
                });
            }
            finally
            {
                window.Close();
            }
        }
    }
}
