using System.Xml.Linq;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc.Conversions;
using static XncOptimizerUI.Services.Xnc.XncSymbols;

namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>
    /// Single-attribute edits of a decoded XNC program made from the program tables: bore, groove and
    /// mill coordinates/depth/sizes, <c>&lt;var&gt;</c> name/type/expr/comment, and deleting bores, grooves
    /// and mills. An unacceptable value throws <see cref="XncProgramFormatException"/> with the reason,
    /// before anything is changed.
    /// </summary>
    internal static class XncProgramEdits
    {
        /// <summary>What <see cref="DeleteElements"/> removed: the elements and the tools left unused.</summary>
        internal readonly record struct DeletedElements(int Elements, int Tools);

        /// <summary>
        /// Writes <paramref name="expression"/> (trimmed, as typed) to the bore's attribute; an
        /// explicit <c>z</c> replaces the <c>m="true"</c> middle pin.
        /// </summary>
        public static void SetBoreAttribute(XElement program, int boreIndex, MachiningAttribute attribute, string expression)
        {
            var bore = FindElement(program, XncElementKind.Bore, boreIndex);
            var surface = BoreAttributes.SurfaceOf(bore.Name.LocalName)!.Value;

            if (!attribute.AppliesTo(surface))
            {
                throw new XncProgramFormatException($"<{bore.Name.LocalName}> has no editable '{attribute.XmlName()}' attribute.");
            }

            WriteExpression(program, bore, attribute, expression);

            if (attribute == MachiningAttribute.Z)
            {
                bore.SetAttributeValue("m", null);
            }
        }

        /// <summary>
        /// Writes <paramref name="expression"/> (trimmed, as typed) to <paramref name="attribute"/> of
        /// the <paramref name="index"/>-th element of <paramref name="kind"/> (document order among the
        /// elements of its tag); only the attributes its table edits are accepted.
        /// </summary>
        public static void SetElementAttribute(XElement program, XncElementKind kind, int index, MachiningAttribute attribute, string expression)
        {
            var element = FindElement(program, kind, index);

            if (!kind.EditableAttributes().Contains(attribute))
            {
                throw new XncProgramFormatException($"<{element.Name.LocalName}> has no editable '{attribute.XmlName()}' attribute.");
            }

            WriteExpression(program, element, attribute, expression);
        }

        public static void SetVariableAttribute(XElement program, int variableIndex, XncVariableAttribute attribute, string value)
        {
            var variable = program.Elements(XncTags.Variable).ElementAtOrDefault(variableIndex)
                ?? throw new XncProgramFormatException("variable not found in the program.");

            switch (attribute)
            {
                case XncVariableAttribute.Name: RenameVariable(program, variable, value); break;
                case XncVariableAttribute.Type: ChangeVariableType(program, variable, value); break;
                case XncVariableAttribute.Expr: ChangeVariableExpr(program, variable, value); break;
                default: variable.SetAttributeValue("comment", string.IsNullOrEmpty(value) ? null : value); break;
            }
        }

        /// <summary>
        /// Removes the elements of <paramref name="kind"/> at <paramref name="indices"/> (positions as
        /// <see cref="XncElementRef.Index"/>; a contour takes its segments along), then the tools they
        /// used that no remaining element references. Every index is resolved before anything is
        /// removed, so an unknown one throws and leaves the program unchanged.
        /// </summary>
        public static DeletedElements DeleteElements(XElement program, XncElementKind kind, IEnumerable<int> indices)
        {
            var heads = indices
                .Distinct()
                .Select(index => FindElement(program, kind, index))
                .ToList();

            heads
                .SelectMany(head => ContourSegmentsOf(head).Prepend(head))
                .ToList()
                .ForEach(element => element.Remove());

            var toolsRemoved = ConversionTools.RemoveUnreferenced(program, ConversionTools.NamesUsedBy(heads));

            return new DeletedElements(heads.Count, toolsRemoved);
        }

        #region Methods

        /// <summary>The <paramref name="index"/>-th element of <paramref name="kind"/> in document order (see <see cref="XncElementRef.Index"/>).</summary>
        private static XElement FindElement(XElement program, XncElementKind kind, int index) =>
            program.Elements().Where(element => IsOfKind(element, kind)).ElementAtOrDefault(index)
                ?? throw new XncProgramFormatException($"{(kind == XncElementKind.Bore ? "bore" : $"<{TagOf(kind)}>")} not found in the program.");

        private static bool IsOfKind(XElement element, XncElementKind kind) =>
            kind == XncElementKind.Bore
                ? XncTags.IsBore(element.Name.LocalName)
                : element.Name.LocalName == TagOf(kind);

        /// <summary>The one tag of a groove or mill head kind (a bore has five, see <see cref="XncTags.IsBore"/>).</summary>
        private static string TagOf(XncElementKind kind) => kind switch
        {
            XncElementKind.Groove => XncTags.Groove,
            XncElementKind.Contour => XncTags.ContourStart,
            XncElementKind.Ellipse => XncTags.Ellipse,
            XncElementKind.Rectangle => XncTags.Rectangle,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

        /// <summary>The segments continuing a <c>&lt;ms&gt;</c> contour head; none for any other element.</summary>
        private static IEnumerable<XElement> ContourSegmentsOf(XElement head) =>
            head.Name.LocalName == XncTags.ContourStart
                ? head.ElementsAfterSelf().TakeWhile(XncMillElements.IsContourSegment)
                : [];

        /// <summary>Checks <paramref name="expression"/> against the element's symbols and range, then writes it trimmed.</summary>
        private static void WriteExpression(XElement program, XElement element, MachiningAttribute attribute, string expression)
        {
            if (!MachiningExpression.TryEvaluate(expression, attribute, SeedElementSymbols(program, element), out _, out var error))
            {
                throw new XncProgramFormatException($"invalid {attribute.XmlName()} \"{expression}\": {error}.");
            }

            element.SetAttributeValue(attribute.XmlName(), expression.Trim());
        }


        /// <summary>Renames the variable and every whole-identifier reference to it in the program's expressions.</summary>
        private static void RenameVariable(XElement program, XElement variable, string value)
        {
            var oldName = variable.GetNameValue() ?? string.Empty;
            var newName = value.Trim();
            var otherNames = program.Elements(XncTags.Variable).Where(v => v != variable).Select(v => v.GetNameValue() ?? string.Empty);

            if (XncVariableRules.CheckName(newName, otherNames) is { } error)
            {
                throw new XncProgramFormatException($"invalid name \"{value}\": {error}.");
            }

            ExpressionReferences(program, variable, oldName)
                .ToList()
                .ForEach(attribute => attribute.Value = RenameIdentifier(attribute.Value, oldName, newName));

            variable.SetAttributeValue("name", newName);
        }

        /// <summary>
        /// Sets the type when the current expr is valid for it; a string/bool variable cannot be
        /// referenced from an arithmetic expression, so that change is refused while it is.
        /// </summary>
        private static void ChangeVariableType(XElement program, XElement variable, string value)
        {
            if (!XncVariableTypes.TryParseExact(value, out var type))
            {
                throw new XncProgramFormatException($"unknown type \"{value}\" (use int, double, string or bool).");
            }

            var name = variable.GetNameValue() ?? string.Empty;
            var uses = ExpressionReferences(program, variable, name).Count();

            if (!type.IsNumeric() && uses > 0)
            {
                throw new XncProgramFormatException($"'{name}' is used in {uses} expression(s); a {type.XmlName()} variable cannot be referenced there.");
            }

            RequireValidExpr(program, variable, variable.GetExprValue(), type);
            variable.SetAttributeValue("type", type.XmlName());
        }

        private static void ChangeVariableExpr(XElement program, XElement variable, string value)
        {
            var type = XncVariableTypes.Parse(variable.GetTypeValue());

            RequireValidExpr(program, variable, value, type);
            variable.SetAttributeValue("expr", XncVariableRules.NormalizeExpr(value, type));
        }

        private static void RequireValidExpr(XElement program, XElement variable, string? expr, XncVariableType type)
        {
            if (!XncVariableRules.TryCheckExpr(expr, type, SeedProgramSymbols(program, before: variable), out _, out var error))
            {
                throw new XncProgramFormatException($"invalid {type.XmlName()} expr \"{expr}\": {error}.");
            }
        }

        /// <summary>
        /// Expression attributes of the program (other than <paramref name="variable"/>'s own) that
        /// reference <paramref name="name"/>. A string/bool var's expr is a literal, so it never counts.
        /// </summary>
        private static IEnumerable<XAttribute> ExpressionReferences(XElement program, XElement variable, string name) =>
            program.Elements()
                .Where(element => element != variable)
                .Where(element => element.Name.LocalName != XncTags.Variable || XncVariableTypes.Parse(element.GetTypeValue()).IsNumeric())
                .SelectMany(element => element.Attributes())
                .Where(attribute => ExpressionAttributes.Contains(attribute.Name.LocalName))
                .Where(attribute => ReferencesIdentifier(attribute.Value, name));

        #endregion
    }
}
