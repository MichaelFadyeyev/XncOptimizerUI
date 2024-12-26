namespace XncOptimizerUI.MVVM.Models
{
    public class Part
    {
        public int Id { get; set; }
        public int GoodId { get; set; }
        public int MaterialId { get; set; }
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
        public int? TopBandingId { get; set; } // elt 
        public int? BottomBandingId { get; set; } // elb
        public int? LeftBandingId { get; set; } // ell
        public int? RightBandingId { get; set; } // elr

        //public string? TopBandingMat { get; set; } // EltMat
        //public string? BottomBandingMat { get; set; } // ElbMat
        //public string? LeftBandingMat { get; set; } // EllMat
        //public string? RightBandingMat { get; set; } // ElrMat

        //public string? TopBandingOperation { get; set; } // operation of type "EL"
        //public string? BottomBandingOperation { get; set; } // operation of type "EL"
        //public string? LeftBandingOperation { get; set; } // operation of type "EL"
        //public string? RightBandingOperation { get; set; } // operation of type "EL"


    }
}
