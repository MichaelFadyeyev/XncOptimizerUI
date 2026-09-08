using System.Globalization;
using System.Windows.Controls;
using XncOptimizerUI.Helpers;

namespace XncOptimizerUI.MVVM.Validators
{
    public class DecimalValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var text = value as string;

            if (string.IsNullOrWhiteSpace(text))
                return ValidationResult.ValidResult;

            return DecimalInput.TryParse(text, out _)
                ? ValidationResult.ValidResult
                : new ValidationResult(false, "Invalid decimal number");
        }
    }
}
