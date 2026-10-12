using System.Xml.Linq;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models.Xnc;
using static XncOptimizerUI.Services.Xnc.XncSymbols;

namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>
    /// Single-attribute edits of a decoded XNC program made from the program tables: bore
    /// coordinates/depth and <c>&lt;var&gt;</c> name/type/expr/comment. An unacceptable value
    /// throws <see cref="XncProgramFormatException"/> with the reason, before anything is changed.
    /// </summary>
    internal static class XncProgramEdits
    {
        /// <summary>
        /// Writes <paramref name="expression"/> (trimmed, as typed) to the bore's attribute; an
        /// explicit <c>z</c> replaces the <c>m="true"</c> middle pin.
        /// </summary>
        public static void SetBoreAttribute(XElement program, int boreIndex, BoreAttribute attribute, string expression)
        {
            var bore = program.Elements().Where(e => XncTags.IsBore(e.Name.LocalName)).ElementAtOrDefault(boreIndex)
                ?? throw new XncProgramFormatException("bore not found in the program.");
            var surface = BoreAttributes.SurfaceOf(bore.Name.LocalName)!.Value;

            if (!attribute.AppliesTo(surface))
            {
                throw new XncProgramFormatException($"<{bore.Name.LocalName}> has no editable '{attribute.XmlName()}' attribute.");
            }

            if (!BoreExpression.TryEvaluate(expression, attribute, SeedProgramSymbols(program), out _, out var error))
            {
                throw new XncProgramFormatException($"invalid {attribute.XmlName()} \"{expression}\": {error}.");
            }

            bore.SetAttributeValue(attribute.XmlName(), expression.Trim());

            if (attribute == BoreAttribute.Z)
            {
                bore.SetAttributeValue("m", null);
            }
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

        #region Methods

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
