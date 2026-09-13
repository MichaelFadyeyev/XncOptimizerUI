using System;
using CommunityToolkit.Mvvm.ComponentModel;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.MVVM.ViewModels
{
    /// <summary>Display-ready row for a selected part's grooves.</summary>
    public partial class GrooveRowVM : ObservableObject
    {
        private readonly Action<GrooveRowVM, bool> _onSelectionChanged;

        public GrooveRowVM(
            int number,
            string side,
            string startX,
            string startY,
            string endX,
            string endY,
            string depth,
            string width,
            string toolToCenterLine,
            XncGrooving groove,
            Action<GrooveRowVM, bool> onSelectionChanged)
        {
            Number = number;
            Side = side;
            StartX = startX;
            StartY = startY;
            EndX = endX;
            EndY = endY;
            Depth = depth;
            Width = width;
            ToolToCenterLine = toolToCenterLine;
            Groove = groove;
            _onSelectionChanged = onSelectionChanged;
        }

        public int Number { get; }
        public string Side { get; }
        public string StartX { get; }
        public string StartY { get; }
        public string EndX { get; }
        public string EndY { get; }
        public string Depth { get; }
        public string Width { get; }
        public string ToolToCenterLine { get; }
        public XncGrooving Groove { get; }

        [ObservableProperty]
        private bool _isSelected;

        partial void OnIsSelectedChanged(bool value) => _onSelectionChanged(this, value);
    }
}
