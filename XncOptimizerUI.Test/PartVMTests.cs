using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.ViewModels.Parts;

namespace XncOptimizerUI.Test
{
    [TestFixture]
    public class PartVMTests
    {
        [Test]
        public void Setter_WritesThroughToThePart_AndRaisesPropertyChanged()
        {
            var part = new Part { Name = "old" };
            var vm = new PartVM(part);
            var raised = new List<string?>();
            vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            vm.Name = "new";

            Assert.Multiple(() =>
            {
                Assert.That(part.Name, Is.EqualTo("new"));
                Assert.That(raised, Is.EqualTo(new[] { nameof(PartVM.Name) }));
            });
        }

        [Test]
        public void Setter_WithTheCurrentValue_RaisesNothing()
        {
            var vm = new PartVM(new Part { ConsiderTexture = true, TopBandingId = 3 }) { IsChecked = true };
            var raised = new List<string?>();
            vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            vm.ConsiderTexture = true;
            vm.TopBandingId = 3;
            vm.IsChecked = true;

            Assert.That(raised, Is.Empty, "an unchanged value must not trigger the immediate part save");
        }
    }
}
