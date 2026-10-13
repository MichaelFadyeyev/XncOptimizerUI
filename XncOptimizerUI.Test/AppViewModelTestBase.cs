using NSubstitute;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.ViewModels;
using XncOptimizerUI.Test.Fakes;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// The main window's view models built as the app builds them, over a project-service fake.
    /// These were impossible before DI: constructing AppViewModel triggered
    /// ConfigService's static constructor (real disk I/O), and the clipboard and
    /// MessageBox commands either needed an STA message pump or blocked the test
    /// host indefinitely.
    /// </summary>
    public abstract class AppViewModelTestBase : TempFixtureTestBase
    {
        protected FakeProjectService ProjectService { get; private set; } = null!;

        protected IConfigService Config { get; private set; } = null!;

        protected IDialogService Dialogs { get; private set; } = null!;

        [SetUp]
        public void SetUp()
        {
            ProjectService = new FakeProjectService();
            Dialogs = Substitute.For<IDialogService>();

            Config = Substitute.For<IConfigService>();
            Config.LabelsToProcess.Returns(["поріз.2х40"]);
            Config.GetLastLabelToProcessSelected().Returns("поріз.2х40");
        }

        protected AppViewModel CreateViewModel(TimeProvider? clock = null) =>
            TestViewModels.Create(ProjectService.Services, Config, Dialogs, clock);

        /// <summary>
        /// A failed operation ran once and was not followed by reopening or saving the project; the
        /// selected part is read again, as it is selected again.
        /// </summary>
        protected void AssertNotReloadedAfter(string operation)
        {
            var after = ProjectService.Calls.SkipWhile(call => call != operation).Skip(1).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(ProjectService.Calls.Count(call => call == operation), Is.EqualTo(1), $"{operation} runs once");
                Assert.That(after, Has.None.EqualTo(nameof(FakeProjectService.Open)).And.None.EqualTo(nameof(FakeProjectService.Save)),
                    "a failed operation must not reload or save the project");
            });
        }

        /// <summary>
        /// Two parts on one sheet, banded top and bottom, reachable through
        /// OpenFileCommand once the open dialog is stubbed to return a path.
        /// </summary>
        protected void SeedTwoParts()
        {
            Dialogs.ShowOpenProjectDialog().Returns(@"C:\project.project");

            // Two sheets so a test can mark exactly one of them as a split sheet.
            ProjectService.Sheets =
            [
                new Sheet { Id = 10, Name = "ДСП 18мм" },
                new Sheet { Id = 11, Name = "ДСП 16мм" }
            ];

            ProjectService.Bands =
            [
                new Band { Id = 1, ExternalSymbol = "S1", InternalSymbol = "i1" },
                new Band { Id = 2, ExternalSymbol = "S2", InternalSymbol = "i2" }
            ];

            ProjectService.Parts =
            [
                new Part
                {
                    Id = 100, Name = "Полиця", Count = 2, Length = 600, Width = 400,
                    SheetId = 10, TopBandingId = 1, BottomBandingId = 2
                },
                new Part
                {
                    Id = 101, Name = "Бокова", Count = 4, Length = 800, Width = 300,
                    SheetId = 11
                }
            ];
        }
    }
}
