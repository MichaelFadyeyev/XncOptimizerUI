using System.Xml.Linq;
using XncOptimizerUI.Services.Xnc;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// Exercises <see cref="XncToolDeclarationSorter"/>: tool declarations move to the program
    /// start, ordered Bore - Cut - Mill (other names last), then by diameter ascending.
    /// </summary>
    [TestFixture]
    public class XncToolDeclarationSorterTests
    {
        private static XElement Program(string body) =>
            XElement.Parse($"""<program dx="600" dy="300" dz="18">{body}</program>""");

        private static string[] Tags(XElement program) =>
            program.Elements().Select(e => e.Attribute("name") is { } name ? $"{e.Name.LocalName}:{name.Value}" : e.Name.LocalName).ToArray();

        [Test]
        public void SortInProgram_MovesToolsToStart_OrderedByKindThenDiameter()
        {
            var program = Program("""
                <tool name="Mill6" d="6"/>
                <tool name="Bore10" d="10"/>
                <bf x="10" y="10" dp="12" name="Bore10"/>
                <tool name="Cut3.2" d="3.2"/>
                <gr x1="0" y1="50" x2="dx" y2="50" dp="4" t="4" c="0" p="0" name="Cut3.2"/>
                <tool name="Bore8" d="8"/>
                <bf x="30" y="10" dp="12" name="Bore8"/>
                <tool name="Mill20" d="20"/>
                <var name="depth" type="double" expr="dz"/>
                <ms x="0" y="100" dp="depth" c="0" name="Mill20"/>
                <ml x="dx" y="100"/>
                <mr x="100" y="100" dp="8" l="100" w="20" c="3" name="Mill6"/>
                """);

            var result = XncToolDeclarationSorter.SortInProgram(program);

            Assert.Multiple(() =>
            {
                Assert.That(result.Sorted, Is.True);
                Assert.That(result.Skipped, Is.False);
                Assert.That(Tags(program), Is.EqualTo(new[]
                {
                    "tool:Bore8", "tool:Bore10", "tool:Cut3.2", "tool:Mill6", "tool:Mill20",
                    "bf:Bore10", "gr:Cut3.2", "bf:Bore8", "var:depth", "ms:Mill20", "ml", "mr:Mill6",
                }));
            });
        }

        [Test]
        public void SortInProgram_OrdersKindByNamePrefixRegardlessOfUse_OtherNamesLast()
        {
            var program = Program("""
                <tool name="Drill5" d="5"/>
                <tool name="mill4" d="4"/>
                <tool name="Bore10" d="10"/>
                <ms x="0" y="100" dp="4" c="0" name="Bore10"/>
                <ml x="dx" y="100"/>
                """);

            XncToolDeclarationSorter.SortInProgram(program);

            Assert.That(Tags(program).Take(3), Is.EqualTo(new[] { "tool:Bore10", "tool:mill4", "tool:Drill5" }),
                "a Bore-named tool used for milling still ranks as a bore; the prefix match ignores case");
        }

        [Test]
        public void SortInProgram_CollapsesRepeatedIdenticalDeclarations()
        {
            var program = Program("""
                <tool name="Bore8" d="8"/>
                <bf x="10" y="10" dp="12" name="Bore8"/>
                <tool name="Bore8" d="8"/>
                <bf x="30" y="10" dp="12" name="Bore8"/>
                """);

            var result = XncToolDeclarationSorter.SortInProgram(program);

            Assert.Multiple(() =>
            {
                Assert.That(result.Sorted, Is.True);
                Assert.That(Tags(program), Is.EqualTo(new[] { "tool:Bore8", "bf:Bore8", "bf:Bore8" }));
            });
        }

        [Test]
        public void SortInProgram_AlreadySorted_ReportsUnchanged()
        {
            var program = Program("""
                <tool name="Bore8" d="8"/>
                <tool name="Mill6" d="6"/>
                <var name="depth" type="double" expr="dz"/>
                <bf x="10" y="10" dp="12" name="Bore8"/>
                """);
            var before = program.ToString();

            var result = XncToolDeclarationSorter.SortInProgram(program);

            Assert.Multiple(() =>
            {
                Assert.That(result.Sorted, Is.False);
                Assert.That(result.Skipped, Is.False);
                Assert.That(program.ToString(), Is.EqualTo(before));
            });
        }

        [Test]
        public void SortInProgram_WithoutTools_ReportsUnchanged()
        {
            var result = XncToolDeclarationSorter.SortInProgram(Program("""<var name="depth" type="double" expr="dz"/>"""));

            Assert.Multiple(() =>
            {
                Assert.That(result.Sorted, Is.False);
                Assert.That(result.Skipped, Is.False);
            });
        }

        [Test]
        public void SortInProgram_SameNameWithDifferentDiameters_SkipsProgram()
        {
            var program = Program("""
                <tool name="Mill6" d="6"/>
                <tool name="Bore8" d="8"/>
                <bf x="10" y="10" dp="12" name="Bore8"/>
                <tool name="bore8" d="10"/>
                <bf x="30" y="10" dp="12" name="Bore8"/>
                """);
            var before = program.ToString();

            var result = XncToolDeclarationSorter.SortInProgram(program);

            Assert.Multiple(() =>
            {
                Assert.That(result.Sorted, Is.False);
                Assert.That(result.Problems, Is.EqualTo(new[] { "tool \"Bore8\" declared with different diameters" }));
                Assert.That(program.ToString(), Is.EqualTo(before), "a skipped program is left untouched");
            });
        }

        [Test]
        public void SortInProgram_NonNumericDiameter_SkipsProgram()
        {
            var program = Program("""
                <bf x="10" y="10" dp="12" name="Bore8"/>
                <tool name="Bore8" d="dz"/>
                """);

            var result = XncToolDeclarationSorter.SortInProgram(program);

            Assert.Multiple(() =>
            {
                Assert.That(result.Sorted, Is.False);
                Assert.That(result.Problems, Is.EqualTo(new[] { "tool \"Bore8\" has a non-numeric diameter \"dz\"" }));
            });
        }

        [Test]
        public void SortInProgram_ToolInsideMillingContour_SkipsProgram()
        {
            var program = Program("""
                <tool name="Mill6" d="6"/>
                <ms x="0" y="100" dp="4" c="0" name="Mill6"/>
                <ml x="100" y="100"/>
                <tool name="Bore8" d="8"/>
                <tool name="Bore5" d="5"/>
                <ml x="100" y="200"/>
                """);
            var before = program.ToString();

            var result = XncToolDeclarationSorter.SortInProgram(program);

            Assert.Multiple(() =>
            {
                Assert.That(result.Sorted, Is.False);
                Assert.That(result.Problems, Is.EqualTo(new[]
                {
                    "tool \"Bore8\" declared inside a milling contour",
                    "tool \"Bore5\" declared inside a milling contour",
                }));
                Assert.That(program.ToString(), Is.EqualTo(before));
            });
        }

        [Test]
        public void SortInProgram_ToolBetweenContourAndNextMill_IsMoved()
        {
            var program = Program("""
                <tool name="Mill6" d="6"/>
                <ms x="0" y="100" dp="4" c="0" name="Mill6"/>
                <ml x="100" y="100"/>
                <tool name="Mill10" d="10"/>
                <ms x="0" y="200" dp="4" c="0" name="Mill10"/>
                <ml x="100" y="200"/>
                """);

            var result = XncToolDeclarationSorter.SortInProgram(program);

            Assert.Multiple(() =>
            {
                Assert.That(result.Sorted, Is.True);
                Assert.That(Tags(program), Is.EqualTo(new[] { "tool:Mill6", "tool:Mill10", "ms:Mill6", "ml", "ms:Mill10", "ml" }));
            });
        }
    }
}
