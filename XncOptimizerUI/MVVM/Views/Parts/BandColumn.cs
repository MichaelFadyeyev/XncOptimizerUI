using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using XncOptimizerUI.Helpers;

namespace XncOptimizerUI.MVVM.Views.Parts
{
    /// <summary>
    /// Parts grid edge-band column. Displays the band as plain text "&lt;internal symbol&gt; &lt;name&gt;"
    /// (<c>BandCaptionTextStyle</c>, resolved by <see cref="BandCaptionConverter"/> from the row's
    /// band id and the grid's <c>DataContext.Catalog.BandOptions</c>); in edit mode only, a band
    /// select (<c>BandSelectComboBoxStyle</c>) bound two-way to that id - a ComboBox per cell made
    /// grid refreshes lag.
    /// </summary>
    public class BandColumn : DataGridTemplateColumn
    {
        private string _bandingIdPath = string.Empty;

        #region Properties
        /// <summary>The row's band id property, e.g. <c>TopBandingId</c>.</summary>
        public string BandingIdPath
        {
            get => _bandingIdPath;
            set
            {
                _bandingIdPath = value;
                CellTemplate = CreateCaptionTemplate(value);
                CellEditingTemplate = CreateSelectTemplate(value);
            }
        }
        #endregion

        #region Methods
        private static DataTemplate CreateCaptionTemplate(string bandingIdPath)
        {
            var caption = new MultiBinding { Converter = new BandCaptionConverter() };
            caption.Bindings.Add(new Binding(bandingIdPath));
            caption.Bindings.Add(new Binding("DataContext.Catalog.BandOptions")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGrid), 1)
            });

            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetResourceReference(FrameworkElement.StyleProperty, "BandCaptionTextStyle");
            text.SetBinding(TextBlock.TextProperty, caption);

            return new DataTemplate { VisualTree = text };
        }

        private static DataTemplate CreateSelectTemplate(string bandingIdPath)
        {
            var select = new FrameworkElementFactory(typeof(ComboBox));
            select.SetResourceReference(FrameworkElement.StyleProperty, "BandSelectComboBoxStyle");
            select.SetBinding(Selector.SelectedValueProperty, new Binding(bandingIdPath)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

            return new DataTemplate { VisualTree = select };
        }
        #endregion
    }
}
