namespace XncOptimizerUI.MVVM.Models
{
    public class Part
    {
        public int Id { get; set; }
        public int GoodId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Length { get; set; }
        public decimal Width { get; set; }

        //public decimal Cl { get; set; }
        //public decimal Cw { get; set; }
        //public decimal Dl { get; set; }
        //public decimal Dw { get; set; }
        //public decimal Jl { get; set; }
        //public decimal Jw { get; set; }
        //public int Minuscount { get; set; }
        //public int Usedcount { get; set; }

        public bool ConsiderTexture { get; set; }
        //public string? Elt { get; set; }
        //public string? Elb { get; set; }
        //public string? Ell { get; set; }
        //public string? Elr { get; set; }

        //public string? EltMat { get; set; }
        //public string? ElbMat { get; set; }
        //public string? EllMat { get; set; }
        //public string? ElrMat { get; set; }


    }
}
