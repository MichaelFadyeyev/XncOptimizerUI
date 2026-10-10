using System.Windows.Controls;
using XncOptimizerUI.MVVM.ViewModels;
using XncOptimizerUI.MVVM.Views.PartPrograms;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// Drives the real Variables <see cref="DataGrid"/> on <c>TestData/td-bore-depth-variable.project</c>
    /// (<c>&lt;var throughBoreDepth double dz+2.00&gt;</c> on dz=18, used by the bore's <c>dp</c>):
    /// text cells save like the bores table; the Type cell is a selector built in edit mode that
    /// opens on load and commits when its list closes (like the Parts grid band cells).
    /// </summary>
    [TestFixture]
    public class VariablesGridEditingUiTests : ProgramGridUiTestBase
    {
        private const int NameColumn = 2;
        private const int TypeColumn = 3;
        private const int ExprColumn = 4;

        [Test]
        public void EditingExpr_ClickOutside_SavesAndRecalculatesBore()
        {
            var vm = OpenProject("td-bore-depth-variable.project");
            var (window, grid, outside) = Show(new SelectedPartVariablesGrid(), vm, "VariablesDataGrid");
            var row = vm.SelectedPartVariables.Single();

            try
            {
                TypeIntoCell(grid, row, ExprColumn, "dz+4");
                Leave(LeaveBy.ClickOutside, grid, outside);

                Assert.Multiple(() =>
                {
                    Assert.That((row.Expr, row.Value), Is.EqualTo(("dz+4", "22")));
                    Assert.That(vm.SelectedPartBores.Single().Depth, Is.EqualTo("22"));
                    Assert.That(vm.Log, Does.Contain("Variable #1 expr = \"dz+4\" saved"));
                    Assert.That(FindDescendant<TextBox>(grid), Is.Null, "cell left edit mode");
                });
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void EditingName_Enter_RenamesReferences()
        {
            var vm = OpenProject("td-bore-depth-variable.project");
            var (window, grid, outside) = Show(new SelectedPartVariablesGrid(), vm, "VariablesDataGrid");
            var row = vm.SelectedPartVariables.Single();

            try
            {
                TypeIntoCell(grid, row, NameColumn, "holeDepth");
                Leave(LeaveBy.Enter, grid, outside);

                Assert.Multiple(() =>
                {
                    Assert.That(row.Name, Is.EqualTo("holeDepth"));
                    Assert.That(vm.SelectedXncPrograms.Single().Bores.Single().DepthText, Is.EqualTo("holeDepth"));
                    Assert.That(vm.SelectedPartPrograms, Does.Contain("holeDepth double = dz+2.00"));
                });
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void SelectingType_ClosingList_Commits()
        {
            var vm = OpenProject("td-bore-depth-variable.project");
            var (window, grid, _) = Show(new SelectedPartVariablesGrid(), vm, "VariablesDataGrid");
            var row = vm.SelectedPartVariables.Single();

            try
            {
                var selector = OpenTypeSelector(grid, row);
                selector.SelectedItem = "int";
                selector.IsDropDownOpen = false;
                Pump();

                Assert.Multiple(() =>
                {
                    Assert.That(row.Type, Is.EqualTo("int"));
                    Assert.That(vm.Log, Does.Contain("Variable #1 type = \"int\" saved"));
                    Assert.That(FindDescendant<ComboBox>(grid), Is.Null, "cell left edit mode");
                });
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void SelectingType_ExprDoesNotFit_StaysRedThenRevertsOnClickOutside()
        {
            var vm = OpenProject("td-bore-depth-variable.project");
            var (window, grid, outside) = Show(new SelectedPartVariablesGrid(), vm, "VariablesDataGrid");
            var row = vm.SelectedPartVariables.Single();

            try
            {
                var selector = OpenTypeSelector(grid, row);
                selector.SelectedItem = "bool";
                selector.IsDropDownOpen = false;
                Pump();

                var keptOpen = FindDescendant<ComboBox>(grid) is not null;
                var wasRed = row.HasErrors;

                Leave(LeaveBy.ClickOutside, grid, outside);

                Assert.Multiple(() =>
                {
                    Assert.That(keptOpen, Is.True, "refused commit keeps the selector");
                    Assert.That(wasRed, Is.True);
                    Assert.That((row.Type, row.TypeInput, row.HasErrors), Is.EqualTo(("double", "double", false)));
                    Assert.That(vm.Log, Does.Not.Contain("Variable #"));
                });
            }
            finally
            {
                window.Close();
            }
        }

        private static ComboBox OpenTypeSelector(DataGrid grid, VariableRowVM row)
        {
            BeginEdit(grid, row, TypeColumn);

            var selector = FindDescendant<ComboBox>(grid)!;
            Assert.That(selector.IsDropDownOpen, Is.True, "selector opens its list on load");

            return selector;
        }
    }
}
