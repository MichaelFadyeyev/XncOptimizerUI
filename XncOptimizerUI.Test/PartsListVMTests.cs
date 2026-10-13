using XncOptimizerUI.MVVM.ViewModels.Parts;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="PartsListVM"/>: checking parts and the checked count.
    /// </summary>
    [TestFixture]
    public class PartsListVMTests : AppViewModelTestBase
    {
        [Test]
        public void CheckAll_ChecksEveryVisiblePart_AndCheckedCountReflectsIt()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.PartsList.CheckAllCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.PartsList.Parts.Select(p => p.IsSelected), Is.All.True);
                Assert.That(vm.PartsList.CheckedCount, Is.EqualTo(2));
            });
        }

        [Test]
        public void UncheckAll_UnchecksEveryVisiblePart()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.CheckAllCommand.Execute(null);

            vm.PartsList.UncheckAllCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.PartsList.Parts.Select(p => p.IsSelected), Is.All.False);
                Assert.That(vm.PartsList.CheckedCount, Is.EqualTo(0));
            });
        }

        [Test]
        public void CheckedCount_UpdatesOnIndividualToggle_AndRaisesPropertyChanged()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            var raised = 0;
            vm.PartsList.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PartsListVM.CheckedCount)) raised++;
            };

            vm.PartsList.Parts[0].IsSelected = true;
            Assert.Multiple(() =>
            {
                Assert.That(vm.PartsList.CheckedCount, Is.EqualTo(1));
                Assert.That(raised, Is.GreaterThanOrEqualTo(1));
            });

            vm.PartsList.Parts[0].IsSelected = false;
            Assert.That(vm.PartsList.CheckedCount, Is.EqualTo(0));
        }

        [Test]
        public void CheckAll_WithActiveFilter_OnlyChecksFilteredParts()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.FilterName = "Полиця"; // setter runs FilterParts -> only part 100 stays visible
            Assert.That(vm.PartsList.Parts, Has.Count.EqualTo(1));

            vm.PartsList.CheckAllCommand.Execute(null);
            Assert.That(vm.PartsList.CheckedCount, Is.EqualTo(1), "the filtered-out part must not be checked");

            vm.Filter.ClearFiltersCommand.Execute(null);
            var reappeared = vm.PartsList.Parts.Single(p => p.Name == "Бокова");
            Assert.That(reappeared.IsSelected, Is.False);
        }
    }
}
