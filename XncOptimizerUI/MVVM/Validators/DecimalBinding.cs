using System.Windows;
using System.Windows.Data;

namespace XncOptimizerUI.MVVM.Validators
{
    /// <summary>
    /// Two-way binding of a text input holding a decimal, written on every keystroke and checked
    /// by <see cref="DecimalValidationRule"/> (an empty text is valid):
    /// <c>Text="{validators:DecimalBinding Filter.LengthMin}"</c>.
    /// </summary>
    public class DecimalBinding : Binding
    {
        public DecimalBinding()
        {
            Mode = BindingMode.TwoWay;
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
            ValidatesOnDataErrors = true;
            NotifyOnValidationError = true;
            ValidationRules.Add(new DecimalValidationRule());
        }

        public DecimalBinding(string path) : this()
        {
            Path = new PropertyPath(path);
        }
    }
}
