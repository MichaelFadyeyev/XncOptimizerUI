
using System.Globalization;

using System.Windows.Controls;

namespace XncOptimizerUI.MVVM.Validators
{
    public class DecimalValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (string.IsNullOrEmpty(value as string))
                return ValidationResult.ValidResult;

            if (decimal.TryParse(value as string, out _))
                return ValidationResult.ValidResult;

            return new ValidationResult(false, "Invalid decimal number");
        }
    }

}
