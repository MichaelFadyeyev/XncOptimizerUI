using System.Windows.Input;
using XncOptimizerUI.MVVM.ViewModels.Machining;
using XncOptimizerUI.MVVM.Views.PartPreview;

namespace XncOptimizerUI.Test
{
    [TestFixture]
    public class PreviewSelectionTests
    {
        [TestCase(ModifierKeys.None, ElementSelectionMode.Replace)]
        [TestCase(ModifierKeys.Alt, ElementSelectionMode.Replace)]
        [TestCase(ModifierKeys.Control, ElementSelectionMode.Toggle)]
        [TestCase(ModifierKeys.Shift, ElementSelectionMode.Add)]
        [TestCase(ModifierKeys.Control | ModifierKeys.Shift, ElementSelectionMode.Toggle)]
        public void ModeFor_CtrlTogglesShiftAddsElseReplaces(ModifierKeys modifiers, ElementSelectionMode expected) =>
            Assert.That(PreviewSelection.ModeFor(modifiers), Is.EqualTo(expected));

        [Test]
        public void Click_PassesTheElementAndTheModeOfTheModifiers()
        {
            var clicks = new List<(object, ElementSelectionMode)>();
            var selection = new PreviewSelection(_ => false, (element, mode) => clicks.Add((element, mode)));

            selection.Click("bore", ModifierKeys.Shift);

            Assert.That(clicks, Is.EqualTo(new[] { ((object)"bore", ElementSelectionMode.Add) }));
        }

        [Test]
        public void Refresh_ReappliesTheCurrentStateOfEveryRegisteredElement()
        {
            var selected = new HashSet<object>();
            var selection = new PreviewSelection(selected.Contains, (_, _) => { });
            var states = new Dictionary<string, bool>();
            selection.Register("a", isSelected => states["a"] = isSelected);
            selection.Register("b", isSelected => states["b"] = isSelected);

            selected.Add("b");
            selection.Refresh();

            Assert.That(states, Is.EqualTo(new Dictionary<string, bool> { ["a"] = false, ["b"] = true }));
        }
    }
}
