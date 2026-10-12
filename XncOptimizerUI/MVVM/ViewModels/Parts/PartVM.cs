using CommunityToolkit.Mvvm.ComponentModel;
using XncOptimizerUI.MVVM.Models;

namespace XncOptimizerUI.MVVM.ViewModels.Parts
{
    /// <summary>One row of the Parts grid: the <see cref="Models.Part"/> it edits, its row number and its "Sel" checkbox.</summary>
    public partial class PartVM(Part part) : ObservableObject
    {
        #region Properties
        public Part Part { get; } = part;

        public int Id
        {
            get => Part.Id;
            set => SetProperty(Part.Id, value, Part, (p, v) => p.Id = v);
        }

        public int GoodId
        {
            get => Part.GoodId;
            set => SetProperty(Part.GoodId, value, Part, (p, v) => p.GoodId = v);
        }

        public string Name
        {
            get => Part.Name;
            set => SetProperty(Part.Name, value, Part, (p, v) => p.Name = v);
        }

        public int Count
        {
            get => Part.Count;
            set => SetProperty(Part.Count, value, Part, (p, v) => p.Count = v);
        }

        public decimal Length
        {
            get => Part.Length;
            set => SetProperty(Part.Length, value, Part, (p, v) => p.Length = v);
        }

        public decimal Width
        {
            get => Part.Width;
            set => SetProperty(Part.Width, value, Part, (p, v) => p.Width = v);
        }

        public bool ConsiderTexture
        {
            get => Part.ConsiderTexture;
            set => SetProperty(Part.ConsiderTexture, value, Part, (p, v) => p.ConsiderTexture = v);
        }

        public int? TopBandingId
        {
            get => Part.TopBandingId;
            set => SetProperty(Part.TopBandingId, value, Part, (p, v) => p.TopBandingId = v);
        }

        public int? BottomBandingId
        {
            get => Part.BottomBandingId;
            set => SetProperty(Part.BottomBandingId, value, Part, (p, v) => p.BottomBandingId = v);
        }

        public int? LeftBandingId
        {
            get => Part.LeftBandingId;
            set => SetProperty(Part.LeftBandingId, value, Part, (p, v) => p.LeftBandingId = v);
        }

        public int? RightBandingId
        {
            get => Part.RightBandingId;
            set => SetProperty(Part.RightBandingId, value, Part, (p, v) => p.RightBandingId = v);
        }

        public string? TopBandingMat
        {
            get => Part.TopBandingMat;
            set => SetProperty(Part.TopBandingMat, value, Part, (p, v) => p.TopBandingMat = v);
        }

        public string? BottomBandingMat
        {
            get => Part.BottomBandingMat;
            set => SetProperty(Part.BottomBandingMat, value, Part, (p, v) => p.BottomBandingMat = v);
        }

        public string? LeftBandingMat
        {
            get => Part.LeftBandingMat;
            set => SetProperty(Part.LeftBandingMat, value, Part, (p, v) => p.LeftBandingMat = v);
        }

        public string? RightBandingMat
        {
            get => Part.RightBandingMat;
            set => SetProperty(Part.RightBandingMat, value, Part, (p, v) => p.RightBandingMat = v);
        }

        public int SheetId
        {
            get => Part.SheetId;
            set => SetProperty(Part.SheetId, value, Part, (p, v) => p.SheetId = v);
        }
        #endregion

        #region ObservableProperties
        /// <summary>1-based position of the part in the project, shown in the grid's "#" column.</summary>
        [ObservableProperty]
        private int _number;

        /// <summary>The "Sel" checkbox: the batch commands act on the checked parts.</summary>
        [ObservableProperty]
        private bool _isSelected;
        #endregion
    }
}
