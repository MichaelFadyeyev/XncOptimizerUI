using System.Collections.ObjectModel;

namespace XncOptimizerUI.MVVM.Models
{
    public class Context
    {
        public string Log { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string SearchText { get; set; } = string.Empty;
        public ObservableCollection<Part> Parts { get; set; } = [];
}
}
