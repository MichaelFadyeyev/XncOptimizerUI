using System.Globalization;
using XncOptimizerUI.Helpers;
using XncOptimizerUI.MVVM.ViewModels;

namespace XncOptimizerUI.Test
{
    [TestFixture]
    public class BandCaptionConverterTests
    {
        private static readonly BandOptionVM[] Options =
        [
            BandOptionVM.None,
            new(1, "S1", "Band one"),
            new(2, "S2", "Band two"),
        ];

        private static object Convert(params object?[] values) =>
            new BandCaptionConverter().Convert(values, typeof(string), null, CultureInfo.InvariantCulture);

        [Test]
        public void KnownId_ReturnsExternalSymbolAndName()
        {
            Assert.That(Convert(2, Options), Is.EqualTo("S2 Band two"));
        }

        [Test]
        public void NullId_ReturnsEmpty()
        {
            Assert.That(Convert(null, Options), Is.Empty);
        }

        [Test]
        public void UnknownId_ReturnsEmpty()
        {
            Assert.That(Convert(99, Options), Is.Empty);
        }

        [Test]
        public void OptionsNotYetBound_ReturnsEmpty()
        {
            // While the cell's DataGrid ancestor isn't resolved, WPF passes DependencyProperty.UnsetValue.
            Assert.That(Convert(1, System.Windows.DependencyProperty.UnsetValue), Is.Empty);
        }
    }
}
