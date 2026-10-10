namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>A <c>&lt;var&gt;</c> declaration of an XNC program, as authored.</summary>
    public class XncVariable
    {
        /// <summary>Position among the <c>&lt;var&gt;</c> elements of its program, in document order.</summary>
        public int Index { get; init; }

        /// <summary>The <c>name</c> attribute (identifier, case-insensitive).</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>The <c>type</c> attribute; an absent or unknown type is read as <see cref="XncVariableType.Double"/>.</summary>
        public XncVariableType Type { get; init; } = XncVariableType.Double;

        /// <summary>The <c>expr</c> attribute text.</summary>
        public string Expr { get; init; } = string.Empty;

        /// <summary>The <c>comment</c> attribute; <c>null</c> when absent.</summary>
        public string? Comment { get; init; }

        /// <summary>Resolved value of a numeric (<c>int</c>/<c>double</c>) variable; <c>null</c> for <c>string</c>/<c>bool</c>.</summary>
        public double? Value { get; init; }
    }

    /// <summary>The <c>type</c> of a <c>&lt;var&gt;</c>.</summary>
    public enum XncVariableType
    {
        Int,
        Double,
        String,
        Bool
    }

    /// <summary>An editable <c>&lt;var&gt;</c> attribute.</summary>
    public enum XncVariableAttribute
    {
        Name,
        Type,
        Expr,
        Comment
    }

    public static class XncVariableTypes
    {
        /// <summary>Every type, in the order the type selector lists them.</summary>
        public static IReadOnlyList<XncVariableType> All { get; } =
            [XncVariableType.Int, XncVariableType.Double, XncVariableType.String, XncVariableType.Bool];

        public static string XmlName(this XncVariableType type) => type switch
        {
            XncVariableType.Int => "int",
            XncVariableType.Double => "double",
            XncVariableType.String => "string",
            XncVariableType.Bool => "bool",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        /// <summary>Type named by a <c>type</c> attribute; absent or unknown text is <see cref="XncVariableType.Double"/>.</summary>
        public static XncVariableType Parse(string? text) =>
            All.FirstOrDefault(type => string.Equals(type.XmlName(), text?.Trim(), StringComparison.OrdinalIgnoreCase),
                XncVariableType.Double);

        /// <summary>Type named exactly (<c>int</c>/<c>double</c>/<c>string</c>/<c>bool</c>, surrounding spaces ignored).</summary>
        public static bool TryParseExact(string? text, out XncVariableType type)
        {
            var match = All.Where(t => t.XmlName() == text?.Trim()).ToList();
            type = match.FirstOrDefault();

            return match.Count == 1;
        }

        /// <summary>Whether the variable holds a number usable in arithmetic expressions.</summary>
        public static bool IsNumeric(this XncVariableType type) => type is XncVariableType.Int or XncVariableType.Double;

        public static string XmlName(this XncVariableAttribute attribute) => attribute switch
        {
            XncVariableAttribute.Name => "name",
            XncVariableAttribute.Type => "type",
            XncVariableAttribute.Expr => "expr",
            XncVariableAttribute.Comment => "comment",
            _ => throw new ArgumentOutOfRangeException(nameof(attribute), attribute, null)
        };
    }
}
