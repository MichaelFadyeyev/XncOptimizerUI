using System.Windows.Controls;

namespace XncOptimizerUI.MVVM.Views.Common
{
    /// <summary>
    /// The header line of a table: its name (<see cref="HeaderedItemsControl.Header"/>) on the left and
    /// the table's action buttons (the items) right-aligned opposite it. Laid out by its implicit style
    /// in <c>Resources/LayoutStyles.xaml</c>, which also styles the buttons.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;common:TableHeader x:Name="BoresHeader" Header="Bores"&gt;
    ///     &lt;Button x:Name="DeleteBoresButton" Content="Delete" Command="{Binding DeleteSelectedBoresCommand}" /&gt;
    /// &lt;/common:TableHeader&gt;
    /// </code>
    /// </example>
    public sealed class TableHeader : HeaderedItemsControl
    {
        public TableHeader()
        {
            Focusable = false;
            IsTabStop = false;
        }
    }
}
