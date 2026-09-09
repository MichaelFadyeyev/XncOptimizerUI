
using XncOptimizerUI.Core;
using XncOptimizerUI.MVVM.Models;

namespace XncOptimizerUI.MVVM.ViewModels
{
    public class ProductVM(Product product) : ObservableObject
    {
        Product _product = product;

        public int Id
        {
            get { return _product.Id; }
            set { _product.Id = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get { return _product.Name; }
            set { _product.Name = value; OnPropertyChanged(); }
        }

        public string Code
        {
            get { return _product.Code; }
            set { _product.Code = value; OnPropertyChanged(); }
        }

        public int Count
        {
            get { return _product.Count; }
            set { _product.Count = value; OnPropertyChanged(); }
        }
    }
}
