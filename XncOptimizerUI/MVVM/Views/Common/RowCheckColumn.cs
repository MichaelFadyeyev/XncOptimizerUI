using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace XncOptimizerUI.MVVM.Views.Common
{
    /// <summary>
    /// "Check" column: a centered check box bound two-way to the row's <c>IsSelected</c>, toggled
    /// with a single click (unlike <see cref="DataGridCheckBoxColumn"/>, which selects the cell
    /// first).
    /// </summary>
    public class RowCheckColumn : DataGridTemplateColumn
    {
        public RowCheckColumn()
        {
            Header = "Check";
            Width = DataGridLength.Auto;
            CellTemplate = CreateCheckBoxTemplate();
        }

        private static DataTemplate CreateCheckBoxTemplate()
        {
            var checkBox = new FrameworkElementFactory(typeof(CheckBox));
            checkBox.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            checkBox.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsSelected")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

            return new DataTemplate { VisualTree = checkBox };
        }
    }
}
