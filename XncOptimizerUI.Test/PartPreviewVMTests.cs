using NSubstitute;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models.Xnc;

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
            Project.XncPrograms =
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
            Project.XncPrograms = [];

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
            Project.XncPrograms = [new XncProgram { Side = true, Dx = 400, Dy = 800, Dz = 19, Turn = 2 }];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Preview.Rotation, Is.EqualTo(PartTurn.Deg180));
                Assert.That(vm.Preview.CanRotate, Is.True);
                Assert.That(Project.Calls, Does.Not.Contain(nameof(IProjectEditor.RotatePart)));
            });
        }

        [Test]
        public void SelectingPart_WithNoPrograms_DisablesRotation()
        {
            SeedTwoParts();
            Project.XncPrograms = [];

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
            Project.XncPrograms = [new XncProgram { Side = true, Dx = 400, Dy = 800, Dz = 19, Turn = 0 }];
            Project.Editor.RotatePart(default, default, default).ReturnsForAnyArgs(Project.ChangingProgramsTo([new XncProgram { Side = true, Dx = 800, Dy = 400, Dz = 19, Turn = 1 }]));

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            var savesBefore = Project.SaveCount;

            vm.Preview.Rotation = PartTurn.Deg90;

            Project.Editor.Received(1).RotatePart(vm.PartsList.SelectedPart!.Id, 1, Arg.Any<bool>());
            Assert.Multiple(() =>
            {
                Assert.That(Project.SaveCount, Is.EqualTo(savesBefore + 1));
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
            Project.XncPrograms = [new XncProgram { Side = true, Dx = 400, Dy = 800, Dz = 19, Turn = 0 }];
            Config.NeverFlipTclOnTurn.Returns(neverFlip);

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.Preview.Rotation = PartTurn.Deg90;

            Assert.That(vm.Preview.NeverFlipTclOnTurn, Is.EqualTo(neverFlip));
            Project.Editor.Received(1).RotatePart(Arg.Any<int>(), Arg.Any<int>(), expectedFlip);
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
            Project.XncPrograms = [new XncProgram { Side = true, Dx = 400, Dy = 800, Dz = 19, Turn = 0 }];
            Project.Editor.RotatePart(default, default, default).ReturnsForAnyArgs(_ => Project.Result(changed: false));

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            var savesBefore = Project.SaveCount;

            vm.Preview.Rotation = PartTurn.Deg270;

            Assert.Multiple(() =>
            {
                Assert.That(vm.Preview.Rotation, Is.EqualTo(PartTurn.Deg0));
                Assert.That(Project.SaveCount, Is.EqualTo(savesBefore));
            });
        }

        [Test]
        public void SelectingPart_ToNull_ClearsDisplayDimsAndTurn()
        {
            SeedTwoParts();
            Project.XncPrograms = [new XncProgram { Side = true, Dx = 400, Dy = 800, Dz = 19, Turn = 2 }];

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
