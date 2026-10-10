using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NSubstitute;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.ViewModels;
using XncOptimizerUI.Services;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// Host for UI tests that drive a real program-table <see cref="DataGrid"/> (off-screen
    /// window, STA) against a real <see cref="GibLabProjectService"/> on a copy of a
    /// <c>TestData</c> fixture: application resources, a per-test temp dir, opening the project,
    /// showing the grid below an "outside" button, entering edit mode and leaving a cell.
    /// </summary>
    [Apartment(ApartmentState.STA)]
    public abstract class ProgramGridUiTestBase
    {
        private string _directory = string.Empty;

        public enum LeaveBy
        {
            Enter,
            ClickOutside,
            FocusOutside
        }

        [OneTimeSetUp]
        public void EnsureApplicationResources()
        {
            if (Application.Current is not null) return;

            var assembly = typeof(App).Assembly.GetName().Name;
            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            Application.Current!.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/{assembly};component/Resources/ControlStyles.xaml")
            });
        }

        [SetUp]
        public void CreateTempDirectory()
        {
            _directory = Path.Combine(Path.GetTempPath(), "XncOptimizerUI.Test", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void DeleteTempDirectory()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        /// <summary>Opens a copy of <paramref name="fixture"/>; the first part is selected.</summary>
        protected AppViewModel OpenProject(string fixture)
        {
            var path = Path.Combine(_directory, fixture);
            File.Copy(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", fixture), path);

            var config = Substitute.For<IConfigService>();
            config.SawWidth.Returns(4m);
            var dialogs = Substitute.For<IDialogService>();
            dialogs.ShowOpenProjectDialog().Returns(path);

            var vm = new AppViewModel(
                new GibLabProjectService(config, TimeProvider.System), config, dialogs, "Test",
                new ObservableCollection<string>(["label"]), "label");
            vm.OpenFileCommand.Execute(null);

            return vm;
        }

        /// <summary>Shows <paramref name="control"/> below an "outside" button; returns its grid named <paramref name="gridName"/>.</summary>
        protected static (Window Window, DataGrid Grid, Button Outside) Show(UserControl control, AppViewModel vm, string gridName)
        {
            control.DataContext = vm;
            var outside = new Button { Content = "outside" };
            var panel = new StackPanel();
            panel.Children.Add(outside);
            panel.Children.Add(control);

            var window = new Window { Content = panel, Width = 900, Height = 400, Left = -2000, ShowInTaskbar = false };
            window.Show();
            window.Activate();
            Pump();

            return (window, (DataGrid)control.FindName(gridName), outside);
        }

        /// <summary>Puts the cell of <paramref name="item"/> in <paramref name="column"/> into edit mode.</summary>
        protected static void BeginEdit(DataGrid grid, object item, int column)
        {
            grid.SelectedItem = item;
            grid.CurrentCell = new DataGridCellInfo(item, grid.Columns[column]);
            grid.Focus();
            Pump();

            Assert.That(grid.BeginEdit(), Is.True);
            Pump();
        }

        protected static void TypeIntoCell(DataGrid grid, object item, int column, string text)
        {
            BeginEdit(grid, item, column);

            FindDescendant<TextBox>(grid)!.Text = text;
            Pump();
        }

        protected static void Leave(LeaveBy leave, DataGrid grid, Button outside)
        {
            switch (leave)
            {
                case LeaveBy.ClickOutside:
                    outside.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                    {
                        RoutedEvent = Mouse.PreviewMouseDownEvent
                    });
                    break;
                case LeaveBy.FocusOutside:
                    Keyboard.Focus(outside);
                    break;
                default:
                    var editor = FindDescendant<TextBox>(grid)!;
                    editor.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(editor)!, 0, Key.Enter)
                    {
                        RoutedEvent = Keyboard.KeyDownEvent
                    });
                    break;
            }

            Pump();
        }

        protected static void Pump() =>
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

        protected static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);

                if (child is T match)
                {
                    return match;
                }

                if (FindDescendant<T>(child) is { } found)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
