using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XncOptimizerUI.MVVM.ViewModels;
using XncOptimizerUI.MVVM.Views.Parts;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// The Parts tab views on a real project: <see cref="BandColumn"/> (caption text, band select
    /// in edit mode) in <see cref="PartsGrid"/>, and the decimal range inputs of
    /// <see cref="PartsFilterBar"/> (<c>DecimalBinding</c>, <c>CaretBehavior</c>).
    /// </summary>
    [TestFixture]
    public class PartsViewsUiTests : ProgramGridUiTestBase
    {
        private const string BandedFixture = "td-bore-depth-variable.project";

        /// <summary>14 parts: more rows than a 200 px high grid shows.</summary>
        private const string ScrollFixture = "td-mill-offset.project";

        [Test]
        public void BandColumn_ShowsTheCaptionOfThePartsBand()
        {
            var vm = OpenProject(BandedFixture);
            var band = vm.Catalog.BandOptions.First(o => o.Id != null);
            var part = vm.PartsList.Parts[0];
            part.TopBandingId = band.Id;

            ShowAndRun(new PartsGrid(), vm, control =>
            {
                var text = FindDescendant<TextBlock>(CellOf(control, "PartsTopBandColumn", part))!;

                Assert.That(text.Text, Is.EqualTo(band.Caption));
            });
        }

        [Test]
        public void BandColumn_EditMode_SelectsTheBandIntoThePart()
        {
            var vm = OpenProject(BandedFixture);
            var band = vm.Catalog.BandOptions.First(o => o.Id != null);
            var part = vm.PartsList.Parts[0];
            part.LeftBandingId = null;

            ShowAndRun(new PartsGrid(), vm, control =>
            {
                var grid = (DataGrid)control.FindName("PartsDataGrid");
                BeginEdit(grid, part, grid.Columns.IndexOf((DataGridColumn)control.FindName("PartsLeftBandColumn")));

                var select = FindDescendant<ComboBox>(CellOf(control, "PartsLeftBandColumn", part))!;
                select.SelectedValue = band.Id;

                Assert.Multiple(() =>
                {
                    Assert.That(select.ItemsSource, Is.SameAs(vm.Catalog.BandOptions));
                    Assert.That(part.LeftBandingId, Is.EqualTo(band.Id));
                });
            });
        }

        [Test]
        public void RangeInput_RejectsANonNumber()
        {
            var vm = OpenProject(BandedFixture);

            ShowAndRun(new PartsFilterBar(), vm, control =>
            {
                var input = (TextBox)control.FindName("LengthMinTextBox");
                input.Text = "12x";
                Pump();

                Assert.Multiple(() =>
                {
                    Assert.That(Validation.GetHasError(input), Is.True);
                    Assert.That(vm.Filter.LengthMin, Is.Empty);
                });
            });
        }

        [Test]
        public void RangeInput_RewrittenWhileTyping_KeepsTheCaretAtTheEnd()
        {
            var vm = OpenProject(BandedFixture);

            ShowAndRun(new PartsFilterBar(), vm, control =>
            {
                var input = (TextBox)control.FindName("WidthMaxTextBox");
                Keyboard.Focus(input);
                input.Text = ".5";
                Pump();
                vm.Filter.NormalizeRangeBounds(); // what the debounce timer runs after typing stops
                Pump();

                Assert.Multiple(() =>
                {
                    Assert.That(vm.Filter.WidthMax, Is.EqualTo("0.5"));
                    Assert.That(input.Text, Is.EqualTo("0.5"));
                    Assert.That(input.CaretIndex, Is.EqualTo(3));
                });
            });
        }

        [Test]
        public void DeleteButton_EnabledWhileAPartIsChecked()
        {
            var vm = OpenProject(BandedFixture);

            ShowAndRun(new PartsFilterBar(), vm, control =>
            {
                var button = (Button)control.FindName("DeletePartsButton");
                var disabledBefore = !button.IsEnabled;

                vm.PartsList.Parts[0].IsChecked = true;
                Pump();

                Assert.Multiple(() =>
                {
                    Assert.That(disabledBefore, Is.True);
                    Assert.That(button.IsEnabled, Is.True);
                });
            });
        }

        [Test]
        public void DeleteChecked_NextPartVisible_KeepsTheScrollPosition()
        {
            var vm = OpenProject(ScrollFixture);

            ShowAndRun(new PartsGrid(), vm, control =>
            {
                var scroller = ScrollerOf(control);
                scroller.ScrollToVerticalOffset(3);
                Pump();
                var offsetBefore = scroller.VerticalOffset;
                var shown = vm.PartsList.Parts;
                var next = shown[5];
                shown[4].IsChecked = true;

                vm.PartsList.DeleteCheckedCommand.Execute(null);
                Pump();

                Assert.Multiple(() =>
                {
                    Assert.That(offsetBefore, Is.EqualTo(3));
                    Assert.That(scroller.VerticalOffset, Is.EqualTo(offsetBefore));
                    Assert.That(vm.PartsList.SelectedPart, Is.SameAs(next));
                    Assert.That(((DataGrid)control.FindName("PartsDataGrid")).SelectedItem, Is.SameAs(next));
                });
            }, height: 200);
        }

        [Test]
        public void DeleteChecked_NextPartOffScreen_ScrollsItIntoView()
        {
            var vm = OpenProject(ScrollFixture);

            ShowAndRun(new PartsGrid(), vm, control =>
            {
                var scroller = ScrollerOf(control);
                var shown = vm.PartsList.Parts;
                var next = shown[11];
                shown[10].IsChecked = true;

                vm.PartsList.DeleteCheckedCommand.Execute(null);
                Pump();

                var nextIndex = shown.IndexOf(next);

                Assert.Multiple(() =>
                {
                    Assert.That(vm.PartsList.SelectedPart, Is.SameAs(next));
                    Assert.That(scroller.VerticalOffset, Is.GreaterThan(0));
                    Assert.That(nextIndex, Is.InRange(scroller.VerticalOffset, scroller.VerticalOffset + scroller.ViewportHeight - 1),
                        "the next part's row is within the viewport (item-based scrolling)");
                });
            }, height: 200);
        }

        private static ScrollViewer ScrollerOf(UserControl control) =>
            FindDescendant<ScrollViewer>((DataGrid)control.FindName("PartsDataGrid"))!;

        /// <summary>Shows <paramref name="control"/> bound to <paramref name="vm"/> in an off-screen window while <paramref name="act"/> runs.</summary>
        private static void ShowAndRun(UserControl control, AppViewModel vm, Action<UserControl> act, double height = 400)
        {
            control.DataContext = vm;
            var window = new Window { Content = control, Width = 1000, Height = height, Left = -4000, ShowInTaskbar = false };

            try
            {
                window.Show();
                window.Activate();
                Pump();

                act(control);
            }
            finally
            {
                window.Close();
            }
        }

        private static FrameworkElement CellOf(UserControl control, string columnName, object item) =>
            ((DataGridColumn)control.FindName(columnName)).GetCellContent(item)!;
    }
}
