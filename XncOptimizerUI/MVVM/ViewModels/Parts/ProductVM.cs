using CommunityToolkit.Mvvm.ComponentModel;
using XncOptimizerUI.MVVM.Models;

namespace XncOptimizerUI.MVVM.ViewModels.Parts
{
    /// <summary>One product of the project, for the Products grid.</summary>
    public class ProductVM(Product product) : ObservableObject
    {
        private readonly Product _product = product;

        public int Id
        {
            get => _product.Id;
            set => SetProperty(_product.Id, value, _product, (p, v) => p.Id = v);
        }

        public string Name
        {
            get => _product.Name;
            set => SetProperty(_product.Name, value, _product, (p, v) => p.Name = v);
        }

        public string Code
        {
            get => _product.Code;
            set => SetProperty(_product.Code, value, _product, (p, v) => p.Code = v);
        }

        public int Count
        {
            get => _product.Count;
            set => SetProperty(_product.Count, value, _product, (p, v) => p.Count = v);
        }
    }
}
