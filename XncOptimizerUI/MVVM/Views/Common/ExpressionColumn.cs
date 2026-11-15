using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace XncOptimizerUI.MVVM.Views.Common
{
    /// <summary>
    /// Editable numeric column of a program table, bound to one <c>ExpressionCellVM</c> of the row
    /// (<see cref="Cell"/>, e.g. <c>StartX</c>): displays the evaluated value with the
    /// <c>expression = exact value</c> tooltip, and edits the authored text (number or expression)
    /// in a validated editor that turns red on an invalid draft. The row commits the cell
    /// (<c>DataGridCellEditBehavior.CommitToRow</c> on the grid).
    /// </summary>
    public class ExpressionColumn : DataGridTemplateColumn
    {
        private const string HeaderStyleKey = "ExpressionColumnHeaderStyle";
        private const string TextStyleKey = "ExpressionCellTextStyle";
        private const string EditBoxStyleKey = "ExpressionCellEditBoxStyle";

        private string _cell = string.Empty;

        public ExpressionColumn()
        {
            Width = new DataGridLength(1, DataGridLengthUnitType.Star);
            HeaderStyle = Application.Current?.TryFindResource(HeaderStyleKey) as Style;
        }

        /// <summary>Name of the row's <c>ExpressionCellVM</c> property this column shows and edits.</summary>
        public string Cell
        {
            get => _cell;
            set
            {
                _cell = value;
                CellTemplate = CreateDisplayTemplate(value);
                CellEditingTemplate = CreateEditingTemplate(value);
            }
        }

        private static DataTemplate CreateDisplayTemplate(string cell)
        {
            var text = new FrameworkElementFactory(typeof(TextBlock), $"{cell}Text");
            text.SetResourceReference(FrameworkElement.StyleProperty, TextStyleKey);
            text.SetBinding(TextBlock.TextProperty, new Binding($"{cell}.Display") { Mode = BindingMode.OneWay });
            text.SetBinding(FrameworkElement.ToolTipProperty, new Binding($"{cell}.ToolTip") { Mode = BindingMode.OneWay });

            return new DataTemplate { VisualTree = text };
        }

        private static DataTemplate CreateEditingTemplate(string cell)
        {
            var input = new FrameworkElementFactory(typeof(TextBox), $"{cell}Input");
            input.SetResourceReference(FrameworkElement.StyleProperty, EditBoxStyleKey);
            input.SetBinding(TextBox.TextProperty, new Binding($"{cell}.Input")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                ValidatesOnNotifyDataErrors = true
            });

            return new DataTemplate { VisualTree = input };
        }
    }
}
