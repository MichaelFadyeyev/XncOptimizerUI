using XncOptimizerUI.Core;
using XncOptimizerUI.MVVM.Models;

namespace XncOptimizerUI.MVVM.ViewModels
{
    public class PartVM(Part part) : ObservableObject
    {
        private Part _part = part;
        private int _number;

        public int Number
        {
            get { return _number; }
            set { _number = value; OnPropertyChanged(); }
        }

        public int Id
        {
            get { return _part.Id; }
            set { _part.Id = value; OnPropertyChanged(); }
        }

        public int GoodId
        {
            get { return _part.GoodId; }
            set { _part.GoodId = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get { return _part.Name; }
            set { _part.Name = value; OnPropertyChanged(); }
        }

        public int Count
        {
            get { return _part.Count; }
            set { _part.Count = value; OnPropertyChanged(); }
        }

        public decimal Length
        {
            get { return _part.Length; }
            set { _part.Length = value; OnPropertyChanged(); }
        }

        public decimal Width
        {
            get { return _part.Width; }
            set { _part.Width = value; OnPropertyChanged(); }
        }

        public bool ConsiderTexture
        {
            get { return _part.ConsiderTexture; }
            set { _part.ConsiderTexture = value; OnPropertyChanged(); }
        }

        public int? TopBandingId
        {
            get { return _part.TopBandingId; }
            set { _part.TopBandingId = value; OnPropertyChanged(); }
        }

        public int? BottomBandingId
        {
            get { return _part.BottomBandingId; }
            set { _part.BottomBandingId = value; OnPropertyChanged(); }
        }

        public int? LeftBandingId
        {
            get { return _part.LeftBandingId; }
            set { _part.LeftBandingId = value; OnPropertyChanged(); }
        }

        public int? RightBandingId
        {
            get { return _part.RightBandingId; }
            set { _part.RightBandingId = value; OnPropertyChanged(); }
        }

        public string? TopBandingMat
        {
            get { return _part.TopBandingMat; }
            set { _part.TopBandingMat = value; OnPropertyChanged(); }
        }

        public string? BottomBandingMat
        {
            get { return _part.BottomBandingMat; }
            set { _part.BottomBandingMat = value; OnPropertyChanged(); }
        }
        public string? LeftBandingMat
        {
            get { return _part.LeftBandingMat; }
            set { _part.LeftBandingMat = value; OnPropertyChanged(); }
        }
        public string? RightBandingMat
        {
            get { return _part.RightBandingMat; }
            set { _part.RightBandingMat = value; OnPropertyChanged(); }
        }
    }
}
