using XncOptimizerUI.MVVM.ViewModels.Parts;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="PartsFilterVM"/>: name and size-range filtering, range input normalization and bound equalizing.
    /// </summary>
    [TestFixture]
    public class PartsFilterVMTests : AppViewModelTestBase
    {
        [Test]
        public void LengthMin_ExcludesShorterParts()
        {
            SeedTwoParts(); // Полиця 600x400, Бокова 800x300

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "700";

            Assert.That(vm.PartsList.Parts.Select(p => p.Name), Is.EqualTo(new[] { "Бокова" }));
        }

        [Test]
        public void LengthRange_KeepsPartsInsideBounds()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "550";
            vm.Filter.LengthMax = "650";

            Assert.That(vm.PartsList.Parts.Select(p => p.Name), Is.EqualTo(new[] { "Полиця" }));
        }

        [Test]
        public void WidthMax_ExcludesWiderParts()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.WidthMax = "350";

            Assert.That(vm.PartsList.Parts.Select(p => p.Name), Is.EqualTo(new[] { "Бокова" }));
        }

        [Test]
        public void RangeAndName_CombineAsAnd()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.FilterName = "Бокова";
            vm.Filter.LengthMin = "100";
            vm.Filter.LengthMax = "900";
            Assert.That(vm.PartsList.Parts, Has.Count.EqualTo(1));

            vm.Filter.LengthMax = "500"; // Бокова is 800 long -> now excluded
            Assert.That(vm.PartsList.Parts, Is.Empty);
        }

        [Test]
        public void ClearFilters_ResetsRangeFields()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "550";
            vm.Filter.LengthMax = "650";
            vm.Filter.WidthMin = "100";
            vm.Filter.WidthMax = "450";
            Assert.That(vm.PartsList.Parts, Has.Count.EqualTo(1));

            vm.Filter.ClearFiltersCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.PartsList.Parts, Has.Count.EqualTo(2));
                Assert.That(vm.Filter.LengthMin, Is.Empty);
                Assert.That(vm.Filter.LengthMax, Is.Empty);
                Assert.That(vm.Filter.WidthMin, Is.Empty);
                Assert.That(vm.Filter.WidthMax, Is.Empty);
            });
        }

        [Test]
        public void InvalidDecimalString_IsIgnored()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "abc";

            Assert.That(vm.PartsList.Parts, Has.Count.EqualTo(2), "unparseable bound must not filter anything");
        }

        [Test]
        public void DotSeparator_ParsedAsDecimal()
        {
            SeedTwoParts(); // Полиця 600 long, Бокова 800 long

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMax = "700.5";

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMax, Is.EqualTo("700.5"), "typed text is kept verbatim");
                Assert.That(vm.PartsList.Parts.Select(p => p.Name), Is.EqualTo(new[] { "Полиця" }),
                    "700.5 must parse as a decimal, so only the 600-long part passes");
            });
        }

        [Test]
        public void CommaSeparator_IsInvalid_NotConverted()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMax = "700,5";

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMax, Is.EqualTo("700,5"), "comma is left verbatim, never rewritten to a dot");
                Assert.That(vm.PartsList.Parts, Has.Count.EqualTo(2), "\"700,5\" does not parse, so it filters nothing");
            });
        }

        [Test]
        public void HalfTypedDecimal_IsNotReformatted()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "18."; // mid-typing "18.5"

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMin, Is.EqualTo("18."), "trailing separator must survive the setter so the next digit lands; truncation is commit-time only");
                Assert.That(vm.PartsList.Parts, Has.Count.EqualTo(2), "\"18.\" parses as 18, both parts are longer");
            });
        }

        [Test]
        public void CommaSeparator_SurvivesCanonicalizationUnchanged()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "18,5";
            vm.Filter.NormalizeRangeBounds(); // canonicalization must not touch the comma either

            Assert.That(vm.Filter.LengthMin, Is.EqualTo("18,5"));
        }

        [Test]
        public void TrailingSeparator_TruncatedOnNormalize()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "18.";
            Assert.That(vm.Filter.LengthMin, Is.EqualTo("18."), "not yet - still mid-edit");

            vm.Filter.NormalizeRangeBounds();
            Assert.That(vm.Filter.LengthMin, Is.EqualTo("18"));
        }

        [Test]
        public void TrailingFractionalZeros_StrippedOnNormalize()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "0.500";
            vm.Filter.WidthMax = "350.00"; // -> "350", trailing "." also gone
            vm.Filter.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMin, Is.EqualTo("0.5"));
                Assert.That(vm.Filter.WidthMax, Is.EqualTo("350"));
            });
        }

        [Test]
        public void SurroundingSpaces_TrimmedOnNormalize()
        {
            SeedTwoParts(); // Полиця 600 long, Бокова 800 long

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMax = " 700 ";
            vm.Filter.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMax, Is.EqualTo("700"));
                Assert.That(vm.PartsList.Parts.Select(p => p.Name), Is.EqualTo(new[] { "Полиця" }));
            });
        }

        [Test]
        public void SpacesAndTrailingDot_CleanedOnNormalize()
        {
            SeedTwoParts(); // widths 400 and 300

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.WidthMax = " 350. ";
            vm.Filter.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.WidthMax, Is.EqualTo("350"));
                Assert.That(vm.PartsList.Parts.Select(p => p.Name), Is.EqualTo(new[] { "Бокова" }));
            });
        }

        [Test]
        public void LeadingDot_FormattedWithLeadingZero()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = ".5";
            vm.Filter.NormalizeRangeBounds();

            Assert.That(vm.Filter.LengthMin, Is.EqualTo("0.5"));
        }

        [Test]
        public void NegativeLeadingDot_FormattedWithLeadingZero()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "-.5";
            vm.Filter.NormalizeRangeBounds();

            Assert.That(vm.Filter.LengthMin, Is.EqualTo("-0.5"));
        }

        [Test]
        public void RedundantLeadingZeros_Stripped()
        {
            SeedTwoParts(); // Полиця 600 long, Бокова 800 long

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMax = "0700";
            vm.Filter.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMax, Is.EqualTo("700"));
                Assert.That(vm.PartsList.Parts.Select(p => p.Name), Is.EqualTo(new[] { "Полиця" }));
            });
        }

        [Test]
        public void LeadingZeros_KeepSingleZeroBeforePoint()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            // one bound per pair, sibling blank -> canonicalization only, no equalize snap
            vm.Filter.LengthMin = "005";
            vm.Filter.WidthMax = "00.5";
            vm.Filter.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMin, Is.EqualTo("5"));
                Assert.That(vm.Filter.WidthMax, Is.EqualTo("0.5"));
            });
        }

        [Test]
        public void AllZeros_CollapseToSingleZero()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "000";
            vm.Filter.NormalizeRangeBounds();

            Assert.That(vm.Filter.LengthMin, Is.EqualTo("0"));
        }

        [Test]
        public void LengthMin_AboveLengthMax_RaisesMaxToMatch()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMax = "500";
            vm.Filter.LengthMin = "700";
            vm.Filter.NormalizeRangeBounds(); // debounce timer would do this after typing pauses

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMin, Is.EqualTo("700"));
                Assert.That(vm.Filter.LengthMax, Is.EqualTo("700"));
            });
        }

        [Test]
        public void LengthMax_BelowLengthMin_LowersMinToMatch()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "500";
            vm.Filter.LengthMax = "300";
            vm.Filter.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMin, Is.EqualTo("300"));
                Assert.That(vm.Filter.LengthMax, Is.EqualTo("300"));
            });
        }

        [Test]
        public void WidthMin_AboveWidthMax_RaisesMaxToMatch()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.WidthMax = "200";
            vm.Filter.WidthMin = "450";
            vm.Filter.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.WidthMin, Is.EqualTo("450"));
                Assert.That(vm.Filter.WidthMax, Is.EqualTo("450"));
            });
        }

        [Test]
        public void Equalize_SkippedWhenSiblingBlank()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "700"; // LengthMax left blank -> no snap
            vm.Filter.NormalizeRangeBounds();

            Assert.That(vm.Filter.LengthMax, Is.Empty);
        }

        [Test]
        public void Equalize_ThenFilterUsesSnappedBounds()
        {
            SeedTwoParts(); // Полиця 600 long, Бокова 800 long

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMax = "500";
            vm.Filter.LengthMin = "700"; // both snap to 700 -> nothing is exactly 700 long
            vm.Filter.NormalizeRangeBounds();

            Assert.That(vm.PartsList.Parts, Is.Empty);
        }

        [Test]
        public void Equalize_MaxTypedBelowMin_DigitByDigit_SnapsToFullValue()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "100";
            vm.Filter.LengthMax = "5";  // first digit while typing "50"
            vm.Filter.LengthMax = "50"; // full value
            vm.Filter.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMin, Is.EqualTo("50"), "min must snap to the full max, not the first digit");
                Assert.That(vm.Filter.LengthMax, Is.EqualTo("50"));
            });
        }

        [Test]
        public void Equalize_MinTypedAboveMax_DigitByDigit_SnapsToFullValue()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMax = "40";
            vm.Filter.LengthMin = "9";
            vm.Filter.LengthMin = "90";
            vm.Filter.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMin, Is.EqualTo("90"));
                Assert.That(vm.Filter.LengthMax, Is.EqualTo("90"));
            });
        }

        [Test]
        public void NormalizeRangeBounds_LeavesValidRangeUntouched()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "100";
            vm.Filter.LengthMax = "500";
            vm.Filter.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMin, Is.EqualTo("100"));
                Assert.That(vm.Filter.LengthMax, Is.EqualTo("500"));
            });
        }

        [Test]
        public void ClearFilters_CancelsPendingEqualize()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "700";
            vm.Filter.LengthMax = "500"; // inverted, snap still pending (timer not fired)

            vm.Filter.ClearFiltersCommand.Execute(null);
            vm.Filter.NormalizeRangeBounds(); // pending edge was cleared -> no-op

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMin, Is.Empty);
                Assert.That(vm.Filter.LengthMax, Is.Empty);
                Assert.That(vm.Filter.WidthMin, Is.Empty);
                Assert.That(vm.Filter.WidthMax, Is.Empty);
                Assert.That(vm.PartsList.Parts, Has.Count.EqualTo(2));
            });
        }
    }
}
