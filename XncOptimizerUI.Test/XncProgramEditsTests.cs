using System.Xml.Linq;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="Services.Xnc.XncProgramEdits"/> through <see cref="IProjectEditor.UpdateBore"/>,
    /// <see cref="IProjectEditor.UpdateMachiningElement"/> and
    /// <see cref="IProjectEditor.UpdateVariable"/>.
    /// </summary>
    [TestFixture]
    public class XncProgramEditsTests : ProjectServicesTestBase
    {
        // --- Bore attribute editing (td-bores-large: part 2, bf bores, dx=1000 dy=600 dz=19;
        //     td-bl-65-bore: part 2, <bl y="65" dp="30" m="true">, dx=500 dy=200 dz=18) ---

        private (ProjectServices Service, string Path, int OperationId) OpenForBoreEdit(string fixture)
        {
            var service = CreateService();
            var path = CopyFixture(fixture);
            service.Session.Open(path);

            return (service, path, service.Reader.ReadXncPrograms(2).Single().OperationId);
        }

        private static XElement SavedBoreElement(string path, int boreIndex) =>
            ReadSavedProgram(path, 2).Elements()
                .Where(e => e.Name.LocalName is "bf" or "bt" or "bb" or "bl" or "br")
                .ElementAt(boreIndex);

        [Test]
        public void UpdateBore_FaceX_KeepsExpressionTextAndReadsItsValue()
        {
            var (service, path, operationId) = OpenForBoreEdit("td-bores-large.project");

            var log = string.Empty;
            var result = service.Editor.UpdateBore(operationId, 0, MachiningAttribute.X, " dx-32 ").Into(ref log);
            service.Session.Save();

            var bore = service.Reader.ReadXncPrograms(2).Single().Bores[0];
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, log);
                Assert.That(SavedBoreElement(path, 0).Attribute("x")!.Value, Is.EqualTo("dx-32"));
                Assert.That(bore.X, Is.EqualTo(968));
                Assert.That(bore.XText, Is.EqualTo("dx-32"));
            });
        }

        [Test]
        public void UpdateBore_Depth_ThroughWhenReachingDz()
        {
            var (service, _, operationId) = OpenForBoreEdit("td-bores-large.project");

            var log = string.Empty;
            Assert.That(service.Editor.UpdateBore(operationId, 0, MachiningAttribute.Depth, "dz").Into(ref log), Is.True, log);

            var bore = service.Reader.ReadXncPrograms(2).Single().Bores[0];
            Assert.That((bore.Depth, bore.Through, bore.DepthText), Is.EqualTo((19d, true, "dz")));
        }

        [Test]
        public void UpdateBore_FaceX_WhenDepthIsCustomVariable_KeepsDepthVariable()
        {
            var service = CreateService();
            var path = CopyFixture("td-bore-depth-variable.project");
            service.Session.Open(path);
            var operationId = service.Reader.ReadXncPrograms(1).Single().OperationId;

            var log = string.Empty;
            var result = service.Editor.UpdateBore(operationId, 0, MachiningAttribute.X, "dx/2-10").Into(ref log);
            service.Session.Save();

            var bore = service.Reader.ReadXncPrograms(1).Single().Bores[0];
            var saved = ReadSavedProgram(path, 1).Element("bf")!;
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, log);
                Assert.That(saved.Attribute("x")!.Value, Is.EqualTo("dx/2-10"));
                Assert.That(saved.Attribute("dp")!.Value, Is.EqualTo("throughBoreDepth"));
                Assert.That((bore.X, bore.Depth), Is.EqualTo((290d, 20d)));
            });
        }

        [Test]
        public void UpdateBore_Depth_AcceptsProgramVariable()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-bore-depth-variable.project"));
            var operationId = service.Reader.ReadXncPrograms(1).Single().OperationId;

            var log = string.Empty;
            Assert.That(service.Editor.UpdateBore(operationId, 0, MachiningAttribute.Depth, "throughBoreDepth-10").Into(ref log), Is.True, log);

            var bore = service.Reader.ReadXncPrograms(1).Single().Bores[0];
            Assert.That((bore.Depth, bore.Through, bore.DepthText), Is.EqualTo((10d, false, "throughBoreDepth-10")));
        }

        [Test]
        public void UpdateBore_RejectsUndeclaredVariable()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-bore-depth-variable.project"));
            var operationId = service.Reader.ReadXncPrograms(1).Single().OperationId;

            var log = string.Empty;
            var result = service.Editor.UpdateBore(operationId, 0, MachiningAttribute.Depth, "blindBoreDepth").Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("Unknown variable 'blindBoreDepth'"));
                Assert.That(service.Reader.ReadXncPrograms(1).Single().Bores[0].DepthText, Is.EqualTo("throughBoreDepth"));
            });
        }

        [Test]
        public void UpdateBore_EdgeZ_ReplacesMiddlePin()
        {
            var (service, path, operationId) = OpenForBoreEdit("td-bl-65-bore.project");

            var log = string.Empty;
            var result = service.Editor.UpdateBore(operationId, 0, MachiningAttribute.Z, "6").Into(ref log);
            service.Session.Save();

            var element = SavedBoreElement(path, 0);
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, log);
                Assert.That(element.Attribute("z")!.Value, Is.EqualTo("6"));
                Assert.That(element.Attribute("m"), Is.Null);
                Assert.That(service.Reader.ReadXncPrograms(2).Single().Bores[0].Z, Is.EqualTo(6));
            });
        }

        [TestCase("2*-3")]
        [TestCase("12,5")]
        [TestCase("dw+1")]
        [TestCase("")]
        public void UpdateBore_InvalidExpression_RejectedAndProgramUnchanged(string expression)
        {
            var (service, _, operationId) = OpenForBoreEdit("td-bores-large.project");
            var before = service.Reader.ReadXncPrograms(2).Single().Bores[0].XText;

            var log = string.Empty;
            var result = service.Editor.UpdateBore(operationId, 0, MachiningAttribute.X, expression).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("invalid x"));
                Assert.That(service.Reader.ReadXncPrograms(2).Single().Bores[0].XText, Is.EqualTo(before));
            });
        }

        [Test]
        public void UpdateBore_NonPositiveDepth_Rejected()
        {
            var (service, _, operationId) = OpenForBoreEdit("td-bores-large.project");

            var log = string.Empty;
            Assert.That(service.Editor.UpdateBore(operationId, 0, MachiningAttribute.Depth, "dz-dz").Into(ref log), Is.False);
            Assert.That(log, Does.Contain("Depth must be greater than 0"));
        }

        [Test]
        public void UpdateBore_AttributeNotOnBoreKind_Rejected()
        {
            var (service, _, operationId) = OpenForBoreEdit("td-bl-65-bore.project");

            var log = string.Empty;
            Assert.That(service.Editor.UpdateBore(operationId, 0, MachiningAttribute.X, "10").Into(ref log), Is.False);
            Assert.That(log, Does.Contain("<bl> has no editable 'x' attribute"));
        }

        [Test]
        public void UpdateBore_UnknownOperationOrBore_Rejected()
        {
            var (service, _, operationId) = OpenForBoreEdit("td-bores-large.project");

            var log = string.Empty;
            Assert.Multiple(() =>
            {
                Assert.That(service.Editor.UpdateBore(999, 0, MachiningAttribute.X, "10").Into(ref log), Is.False);
                Assert.That(service.Editor.UpdateBore(operationId, 99, MachiningAttribute.X, "10").Into(ref log), Is.False);
                Assert.That(log, Does.Contain("XNC operation not found."));
                Assert.That(log, Does.Contain("bore not found in the program."));
            });
        }

        // --- Groove and mill attribute editing (one program with every editable kind) ---

        private const string GroovesAndMillsProgram = """
            <program dx="600" dy="300" dz="18">
              <tool name="Cut4" d="4" />
              <tool name="Mill10" d="10" />
              <var name="pocketDepth" type="double" expr="dz-10" />
              <gr x1="-10" y1="50" x2="dx+10" y2="50" dp="4" t="4" c="0" p="0" name="Cut4" />
              <gr x1="-10" y1="100" x2="dx+10" y2="100" dp="4" t="4" c="0" p="0" name="Cut4" />
              <ms x="20" y="20" dp="5" c="0" name="Mill10" />
              <ml x="200" y="20" />
              <ms x="20" y="40" dp="5" c="0" name="Mill10" />
              <ml x="200" y="40" />
              <me x="300" y="150" l="20" w="10" a="0" dp="pocketDepth" c="3" name="Mill10" />
              <mr x="450" y="150" l="100" w="40" a="0" r="0" dp="8" c="3" name="Mill10" />
            </program>
            """;

        private string SingleProgramPath => Path.Combine(TempDirectory, "single-program.project");

        private static IEnumerable<TestCaseData> ElementEdits()
        {
            yield return new TestCaseData(XncElementKind.Groove, 1, MachiningAttribute.Y2, " dy-35 ", "gr", "dy-35");
            yield return new TestCaseData(XncElementKind.Groove, 0, MachiningAttribute.GrooveWidth, "tool.dia*2", "gr", "tool.dia*2");
            yield return new TestCaseData(XncElementKind.Contour, 1, MachiningAttribute.Depth, "pocketDepth", "ms", "pocketDepth");
            yield return new TestCaseData(XncElementKind.Ellipse, 0, MachiningAttribute.Angle, "-30", "me", "-30");
            yield return new TestCaseData(XncElementKind.Rectangle, 0, MachiningAttribute.CornerRadius, "tool.dia/2", "mr", "tool.dia/2");
            yield return new TestCaseData(XncElementKind.Rectangle, 0, MachiningAttribute.X, "dx-150", "mr", "dx-150");
        }

        [TestCaseSource(nameof(ElementEdits))]
        public void UpdateMachiningElement_WritesTrimmedExpressionToTheIndexedElement(
            XncElementKind kind, int index, MachiningAttribute attribute, string typed, string tag, string expected)
        {
            var service = OpenSingleProgramProject(GroovesAndMillsProgram);
            var before = ReadSavedProgram(SingleProgramPath, 1);

            var log = string.Empty;
            var result = service.Editor.UpdateMachiningElement(1, kind, index, attribute, typed).Into(ref log);
            service.Session.Save();

            var saved = ReadSavedProgram(SingleProgramPath, 1).Elements(tag).ToList();
            var untouched = before.Elements(tag).Where((_, i) => i != index).Select(e => e.ToString());
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, log);
                Assert.That(saved[index].Attribute(attribute.XmlName())!.Value, Is.EqualTo(expected));
                Assert.That(saved.Where((_, i) => i != index).Select(e => e.ToString()), Is.EqualTo(untouched));
            });
        }

        [Test]
        public void UpdateMachiningElement_ReadBackKeepsAuthoredTextAndValue()
        {
            var service = OpenSingleProgramProject(GroovesAndMillsProgram);

            var log = string.Empty;
            Assert.That(service.Editor.UpdateMachiningElement(1, XncElementKind.Groove, 1, MachiningAttribute.X2, "dx-5").Into(ref log), Is.True, log);

            var groove = service.Reader.ReadXncPrograms(1).Single().Groovings[1];
            Assert.Multiple(() =>
            {
                Assert.That(groove.End.X, Is.EqualTo(595));
                Assert.That(groove.Source.Index, Is.EqualTo(1));
                Assert.That(groove.Source.Text("x2"), Is.EqualTo("dx-5"));
            });
        }

        [TestCase(XncElementKind.Groove, MachiningAttribute.Depth, "0", "Depth must be greater than 0")]
        [TestCase(XncElementKind.Ellipse, MachiningAttribute.Width, "-1", "Width must be greater than 0")]
        [TestCase(XncElementKind.Rectangle, MachiningAttribute.Length, "dz-dz", "Length must be greater than 0")]
        [TestCase(XncElementKind.Rectangle, MachiningAttribute.CornerRadius, "-2", "Corner radius must not be negative")]
        [TestCase(XncElementKind.Groove, MachiningAttribute.X1, "blind", "Unknown variable 'blind'")]
        public void UpdateMachiningElement_InvalidValue_RejectedAndUnchanged(
            XncElementKind kind, MachiningAttribute attribute, string typed, string reason)
        {
            var service = OpenSingleProgramProject(GroovesAndMillsProgram);
            var before = ReadSavedProgram(SingleProgramPath, 1).ToString();

            var log = string.Empty;
            var result = service.Editor.UpdateMachiningElement(1, kind, 0, attribute, typed).Into(ref log);
            service.Session.Save();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain(reason));
                Assert.That(ReadSavedProgram(SingleProgramPath, 1).ToString(), Is.EqualTo(before));
            });
        }

        [TestCase(XncElementKind.Contour, MachiningAttribute.X, "<ms> has no editable 'x' attribute")]
        [TestCase(XncElementKind.Groove, MachiningAttribute.Width, "<gr> has no editable 'w' attribute")]
        [TestCase(XncElementKind.Ellipse, MachiningAttribute.CornerRadius, "<me> has no editable 'r' attribute")]
        public void UpdateMachiningElement_AttributeNotEditedByItsTable_Rejected(XncElementKind kind, MachiningAttribute attribute, string reason)
        {
            var service = OpenSingleProgramProject(GroovesAndMillsProgram);

            var log = string.Empty;
            Assert.That(service.Editor.UpdateMachiningElement(1, kind, 0, attribute, "10").Into(ref log), Is.False);
            Assert.That(log, Does.Contain(reason));
        }

        [Test]
        public void UpdateMachiningElement_UnknownOperationOrElement_Rejected()
        {
            var service = OpenSingleProgramProject(GroovesAndMillsProgram);

            var log = string.Empty;
            Assert.Multiple(() =>
            {
                Assert.That(service.Editor.UpdateMachiningElement(999, XncElementKind.Groove, 0, MachiningAttribute.X1, "10").Into(ref log), Is.False);
                Assert.That(service.Editor.UpdateMachiningElement(1, XncElementKind.Ellipse, 1, MachiningAttribute.X, "10").Into(ref log), Is.False);
                Assert.That(log, Does.Contain("XNC operation not found."));
                Assert.That(log, Does.Contain("<me> not found in the program."));
            });
        }

        // --- Variable editing (td-bore-depth-variable: part 1, dx=600 dy=300 dz=18,
        //     <var throughBoreDepth double dz+2.00>, <bf dp="throughBoreDepth">; extended here with
        //     <var half double throughBoreDepth/2> and <var label string "throughBoreDepth mark">) ---

        private (ProjectServices Service, string Path, int OperationId) OpenForVariableEdit()
        {
            var path = CopyFixture("td-bore-depth-variable.project");
            File.WriteAllText(path, File.ReadAllText(path).Replace(
                "&lt;bf ",
                "&lt;var name=&quot;half&quot; type=&quot;double&quot; expr=&quot;throughBoreDepth/2&quot;/&gt;"
                + "&lt;var name=&quot;label&quot; type=&quot;string&quot; expr=&quot;throughBoreDepth mark&quot;/&gt;&lt;bf "));

            var service = CreateService();
            service.Session.Open(path);

            return (service, path, service.Reader.ReadXncPrograms(1).Single().OperationId);
        }

        private static XElement SavedVariable(string path, int index) =>
            ReadSavedProgram(path, 1).Elements("var").ElementAt(index);

        [Test]
        public void ReadXncPrograms_ReadsDeclaredVariablesOfAnyType()
        {
            var (service, _, _) = OpenForVariableEdit();

            var program = service.Reader.ReadXncPrograms(1).Single();

            Assert.Multiple(() =>
            {
                Assert.That(program.DeclaredVariables.Select(v => (v.Index, v.Name, v.Type, v.Expr, v.Value)), Is.EqualTo(new[]
                {
                    (0, "throughBoreDepth", XncVariableType.Double, "dz+2.00", (double?)20),
                    (1, "half", XncVariableType.Double, "throughBoreDepth/2", (double?)10),
                    (2, "label", XncVariableType.String, "throughBoreDepth mark", (double?)null)
                }));
                Assert.That(program.DeclaredVariables[0].Comment, Is.EqualTo("Глубина сквозного отверстия"));
                Assert.That(program.Variables.Keys, Is.EquivalentTo(new[] { "throughBoreDepth", "half" }));
            });
        }

        [Test]
        public void UpdateVariable_Rename_RewritesNumericReferencesOnly()
        {
            var (service, path, operationId) = OpenForVariableEdit();

            var log = string.Empty;
            var result = service.Editor.UpdateVariable(operationId, 0, XncVariableAttribute.Name, " holeDepth ").Into(ref log);
            service.Session.Save();

            var program = ReadSavedProgram(path, 1);
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, log);
                Assert.That(SavedVariable(path, 0).Attribute("name")!.Value, Is.EqualTo("holeDepth"));
                Assert.That(SavedVariable(path, 1).Attribute("expr")!.Value, Is.EqualTo("holeDepth/2"));
                Assert.That(SavedVariable(path, 2).Attribute("expr")!.Value, Is.EqualTo("throughBoreDepth mark"));
                Assert.That(program.Element("bf")!.Attribute("dp")!.Value, Is.EqualTo("holeDepth"));
                Assert.That(service.Reader.ReadXncPrograms(1).Single().Bores.Single().Depth, Is.EqualTo(20));
            });
        }

        [TestCase("half", "already exists")]
        [TestCase("DZ", "built-in name")]
        [TestCase("tool.dia", "built-in name")]
        [TestCase("1depth", "must start with a letter")]
        [TestCase("hole depth", "must start with a letter")]
        [TestCase("", "Name is required")]
        public void UpdateVariable_InvalidName_Rejected(string name, string reason)
        {
            var (service, _, operationId) = OpenForVariableEdit();

            var log = string.Empty;
            Assert.That(service.Editor.UpdateVariable(operationId, 0, XncVariableAttribute.Name, name).Into(ref log), Is.False);
            Assert.That(log, Does.Contain(reason));
        }

        [Test]
        public void UpdateVariable_Expr_RecalculatesDependents()
        {
            var (service, path, operationId) = OpenForVariableEdit();

            var log = string.Empty;
            Assert.That(service.Editor.UpdateVariable(operationId, 0, XncVariableAttribute.Expr, " dz+4 ").Into(ref log), Is.True, log);
            service.Session.Save();

            var program = service.Reader.ReadXncPrograms(1).Single();
            Assert.Multiple(() =>
            {
                Assert.That(SavedVariable(path, 0).Attribute("expr")!.Value, Is.EqualTo("dz+4"));
                Assert.That(program.Variables["half"], Is.EqualTo(11));
                Assert.That(program.Bores.Single().Depth, Is.EqualTo(22));
            });
        }

        [TestCase(0, "half*2", "Unknown variable 'half'")]      // a later var is not visible yet
        [TestCase(0, "dz--1", "sign is allowed only at the beginning")]
        [TestCase(2, "anything goes", null)]                     // string: any text
        public void UpdateVariable_Expr_CheckedForTypeAndDeclarationOrder(int index, string expr, string? reason)
        {
            var (service, _, operationId) = OpenForVariableEdit();

            var log = string.Empty;
            var result = service.Editor.UpdateVariable(operationId, index, XncVariableAttribute.Expr, expr).Into(ref log);

            Assert.That(result, Is.EqualTo(reason is null), log);
            if (reason is not null)
            {
                Assert.That(log, Does.Contain(reason));
            }
        }

        [Test]
        public void UpdateVariable_Type_ToIntRequiresWholeValue()
        {
            var (service, _, operationId) = OpenForVariableEdit();

            var log = string.Empty;
            Assert.Multiple(() =>
            {
                Assert.That(service.Editor.UpdateVariable(operationId, 0, XncVariableAttribute.Type, "int").Into(ref log), Is.True, log);
                Assert.That(service.Editor.UpdateVariable(operationId, 0, XncVariableAttribute.Expr, "dz/4").Into(ref log), Is.False);
                Assert.That(log, Does.Contain("must be whole"));
            });
        }

        [Test]
        public void UpdateVariable_Type_NonNumericRefusedWhileReferenced()
        {
            var (service, _, operationId) = OpenForVariableEdit();

            var log = string.Empty;
            var result = service.Editor.UpdateVariable(operationId, 0, XncVariableAttribute.Type, "string").Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("'throughBoreDepth' is used in 2 expression(s)"));
            });
        }

        [Test]
        public void UpdateVariable_Type_StringToBoolNeedsBoolExpr()
        {
            var (service, _, operationId) = OpenForVariableEdit();

            var log = string.Empty;
            Assert.Multiple(() =>
            {
                Assert.That(service.Editor.UpdateVariable(operationId, 2, XncVariableAttribute.Type, "bool").Into(ref log), Is.False);
                Assert.That(log, Does.Contain("must be true or false"));
                Assert.That(service.Editor.UpdateVariable(operationId, 2, XncVariableAttribute.Expr, "True").Into(ref log), Is.True, log);
                Assert.That(service.Editor.UpdateVariable(operationId, 2, XncVariableAttribute.Type, "bool").Into(ref log), Is.True, log);
                Assert.That(service.Editor.UpdateVariable(operationId, 2, XncVariableAttribute.Type, "float").Into(ref log), Is.False);
                Assert.That(log, Does.Contain("unknown type \"float\""));
            });
        }

        [Test]
        public void UpdateVariable_Comment_SetAndRemoved()
        {
            var (service, path, operationId) = OpenForVariableEdit();

            var log = string.Empty;
            service.Editor.UpdateVariable(operationId, 1, XncVariableAttribute.Comment, "Половина").Into(ref log);
            service.Session.Save();
            var set = SavedVariable(path, 1).Attribute("comment")?.Value;

            service.Editor.UpdateVariable(operationId, 0, XncVariableAttribute.Comment, string.Empty).Into(ref log);
            service.Session.Save();

            Assert.Multiple(() =>
            {
                Assert.That(set, Is.EqualTo("Половина"));
                Assert.That(SavedVariable(path, 0).Attribute("comment"), Is.Null);
            });
        }

        [Test]
        public void UpdateVariable_UnknownVariable_Rejected()
        {
            var (service, _, operationId) = OpenForVariableEdit();

            var log = string.Empty;
            Assert.That(service.Editor.UpdateVariable(operationId, 9, XncVariableAttribute.Comment, "x").Into(ref log), Is.False);
            Assert.That(log, Does.Contain("variable not found in the program."));
        }
    }
}
