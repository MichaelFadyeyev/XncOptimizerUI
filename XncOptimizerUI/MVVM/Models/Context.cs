using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace XncOptimizerUI.MVVM.Models
{
    public class Context
    {
        public string Log { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string SearchText { get; set; } = string.Empty;
        public XDocument? Document { get; set; }
    }
}
