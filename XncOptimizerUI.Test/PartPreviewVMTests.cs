using NSubstitute;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Test.Fakes;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="MVVM.ViewModels.Preview.PartPreviewVM"/>: display size, turn and rotation of the selected part.
    /// </summary>
    [TestFixture]
    public class PartPreviewVMTests : AppViewModelTestBase
    {
        [Test]
        public void SelectingPart_WithPrograms_TakesDisplayDimsAndTurnFromFirstProgram()
        {
            SeedTwoParts(); // Полиця is 600 x 400
            ProjectService.XncPrograms =
            [
                new XncProgram { Side = true, Dx = 400, Dy = 800, Dz = 19, Turn = 1 },
                new XncProgram { Side = false, Dx = 111, Dy = 222, Dz = 19, Turn = 3 }
            ];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null); // auto-selects the first part

            Assert.Multiple(() =>
            {
                Assert.That(vm.Preview.DisplayLength, Is.EqualTo(400)); // first program's dx, not Part.Length
                Assert.That(vm.Preview.DisplayWidth, Is.EqualTo(800));  // first program's dy
                Assert.That(vm.Preview.Turn, Is.EqualTo(1));
                Assert.That(vm.Preview.TurnText, Is.EqualTo("90°"));
            });
        }

        [Test]
        public void SelectingPart_WithNoPrograms_FallsBackToPartDimensionsAndZeroTurn()
        {
            SeedTwoParts(); // Полиця is 600 x 400
            ProjectService.XncPrograms = [];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Preview.DisplayLength, Is.EqualTo(600));
                Assert.That(vm.Preview.DisplayWidth, Is.EqualTo(400));
                Assert.That(vm.Preview.Turn, Is.Null);       // no XNC program -> no turn code
                Assert.That(vm.Preview.TurnText, Is.EqualTo("0°")); // preview still reads 0°
            });
        }

        [Test]
        public void SelectingPart_SyncsRotationButtonsWithoutRotating()
        {
            SeedTwoParts();
            ProjectService.XncPrograms = [new XncProgram { Side = true, Dx = 400, Dy = 800, Dz = 19, Turn = 2 }];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Preview.Rotation, Is.EqualTo(PartTurn.Deg180));
                Assert.That(vm.Preview.CanRotate, Is.True);
                Assert.That(ProjectService.Calls, Does.Not.Contain(nameof(FakeProjectService.RotatePart)));
            });
        }

        [Test]
        public void SelectingPart_WithNoPrograms_DisablesRotation()
        {
            SeedTwoParts();
            ProjectService.XncPrograms = [];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Preview.CanRotate, Is.False);
                Assert.That(vm.Preview.Rotation, Is.EqualTo(PartTurn.Deg0));
            });
        }

        [Test]
        public void ChangingRotation_RotatesSavesAndRefreshesPreview()
        {
            SeedTwoParts();
            ProjectService.XncPrograms = [new XncProgram { Side = true, Dx = 400, Dy = 800, Dz = 19, Turn = 0 }];
            ProjectService.XncProgramsAfterRotate = [new XncProgram { Side = true, Dx = 800, Dy = 400, Dz = 19, Turn = 1 }];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            var savesBefore = ProjectService.SaveCount;

            vm.Preview.Rotation = PartTurn.Deg90;

            Assert.Multiple(() =>
            {
                Assert.That(ProjectService.LastRotatePartId, Is.EqualTo(vm.PartsList.SelectedPart!.Id));
                Assert.That(ProjectService.LastRotateTargetTurn, Is.EqualTo(1));
                Assert.That(ProjectService.SaveCount, Is.EqualTo(savesBefore + 1));
                Assert.That(vm.Preview.DisplayLength, Is.EqualTo(800));
                Assert.That(vm.Preview.DisplayWidth, Is.EqualTo(400));
                Assert.That(vm.Preview.TurnText, Is.EqualTo("90°"));
                Assert.That(vm.Preview.Rotation, Is.EqualTo(PartTurn.Deg90));
                Assert.That(vm.Log.Text, Does.Contain("rotated to 90°, saved"));
            });
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void ChangingRotation_PassesTclFlipFromOption(bool neverFlip, bool expectedFlip)
        {
            SeedTwoParts();
            ProjectService.XncPrograms = [new XncProgram { Side = true, Dx = 400, Dy = 800, Dz = 19, Turn = 0 }];
            Config.NeverFlipTclOnTurn.Returns(neverFlip);

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.Preview.Rotation = PartTurn.Deg90;

            Assert.Multiple(() =>
            {
                Assert.That(vm.Preview.NeverFlipTclOnTurn, Is.EqualTo(neverFlip));
                Assert.That(ProjectService.LastRotateFlipEdgeGrooveTcl, Is.EqualTo(expectedFlip));
            });
        }

        [Test]
        public void ChangingNeverFlipTclOnTurn_PersistsToOptions()
        {
            var vm = CreateViewModel();

            vm.Preview.NeverFlipTclOnTurn = true;

            Config.Received(1).UpdateNeverFlipTclOnTurn(true);
        }

        [Test]
        public void ChangingRotation_WhenRotationFails_RestoresButtonsAndSavesNothing()
        {
            SeedTwoParts();
            ProjectService.XncPrograms = [new XncProgram { Side = true, Dx = 400, Dy = 800, Dz = 19, Turn = 0 }];
            ProjectService.RotatePartResult = false;

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            var savesBefore = ProjectService.SaveCount;

            vm.Preview.Rotation = PartTurn.Deg270;

            Assert.Multiple(() =>
            {
                Assert.That(vm.Preview.Rotation, Is.EqualTo(PartTurn.Deg0));
                Assert.That(ProjectService.SaveCount, Is.EqualTo(savesBefore));
            });
        }

        [Test]
        public void SelectingPart_ToNull_ClearsDisplayDimsAndTurn()
        {
            SeedTwoParts();
            ProjectService.XncPrograms = [new XncProgram { Side = true, Dx = 400, Dy = 800, Dz = 19, Turn = 2 }];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.SelectedPart = null;

            Assert.Multiple(() =>
            {
                Assert.That(vm.Preview.DisplayLength, Is.Zero);
                Assert.That(vm.Preview.DisplayWidth, Is.Zero);
                Assert.That(vm.Preview.Turn, Is.Null);
                Assert.That(vm.Preview.TurnText, Is.Empty);
            });
        }
    }
}
