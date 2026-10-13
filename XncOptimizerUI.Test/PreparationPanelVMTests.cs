using System.Xml;
using System.Xml.Linq;
using NSubstitute;
using XncOptimizerUI.Contracts;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="MVVM.ViewModels.Panels.PreparationPanelVM"/>: preparing a split along X.
    /// </summary>
    [TestFixture]
    public class PreparationPanelVMTests : AppViewModelTestBase
    {
        /// <summary>
        /// The selected part used to keep its old width: reopening the <c>_spl</c> result released
        /// the selection, which saved the selected part's view model - still holding the source
        /// file's width - into the result file. Part 1 of td.project: w=263, kerf 4.
        /// </summary>
        [Test]
        public void ExecutePrepForSplitAlongX_ResizesTheSelectedPartToo()
        {
            var config = Substitute.For<IConfigService>();
            config.SawWidth.Returns(4.0m);
            config.LabelsToProcess.Returns(["401.01.00.01.СТН-ФРН"]);
            config.GetLastLabelToProcessSelected().Returns("401.01.00.01.СТН-ФРН");

            var dialogs = Substitute.For<IDialogService>();
            dialogs.ShowOpenProjectDialog().Returns(CopyFixture("td.project"));

            var vm = TestViewModels.Create(TestProject.Create(config, TimeProvider.System), config, dialogs);

            vm.OpenFileCommand.Execute(null);
            Assert.That(vm.PartsList.SelectedPart?.Id, Is.EqualTo(1), "opening selects the first part");

            vm.Preparation.ExecutePrepForSplitAlongXCommand.Execute(null);

            var part = XDocument.Load(vm.Workspace.FullPath).Descendants("part").Single(p => (string?)p.Attribute("id") == "1" && p.Attribute("w") is not null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Workspace.FullPath, Does.EndWith("td_spl.project"));
                Assert.That((XmlConvert.ToDecimal(part.Attribute("w")!.Value), XmlConvert.ToDecimal(part.Attribute("dw")!.Value)), Is.EqualTo((530m, 530m)));
                Assert.That(vm.PartsList.Parts.Single(p => p.Id == 1).Width, Is.EqualTo(530m));
            });
        }
    }
}
