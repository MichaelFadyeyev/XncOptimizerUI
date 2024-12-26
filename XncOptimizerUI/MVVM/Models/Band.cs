using System.Runtime.CompilerServices;

namespace XncOptimizerUI.MVVM.Models
{
    public class Band
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Width { get; set; }
        public decimal Thickness { get; set; }
        public string InternalSymbol { get; set; } = string.Empty;
        public string ExternalSymbol { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;

    }
}
