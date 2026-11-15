namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>A program element edited through its own table, other than a bore: a groove or a mill head.</summary>
    public enum XncElementKind
    {
        /// <summary>A <c>&lt;gr&gt;</c> groove.</summary>
        Groove,

        /// <summary>The <c>&lt;ms&gt;</c> head of a milling contour.</summary>
        Contour,

        /// <summary>A <c>&lt;me&gt;</c> milling ellipse.</summary>
        Ellipse,

        /// <summary>A <c>&lt;mr&gt;</c> milling rectangle.</summary>
        Rectangle
    }

    /// <summary>Names and editable attributes of the <see cref="XncElementKind"/>s.</summary>
    public static class XncElementKinds
    {
        /// <summary>Label used in log lines, e.g. <c>Groove #2</c>.</summary>
        public static string DisplayName(this XncElementKind kind) => kind switch
        {
            XncElementKind.Groove => "Groove",
            XncElementKind.Contour => "Contour",
            XncElementKind.Ellipse => "Ellipse",
            XncElementKind.Rectangle => "Rectangle",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

        /// <summary>
        /// Attributes its table edits. A contour only edits its entry depth: moving the entry point
        /// would detach it from the arc segments that start there.
        /// </summary>
        public static IReadOnlyList<MachiningAttribute> EditableAttributes(this XncElementKind kind) => kind switch
        {
            XncElementKind.Groove =>
            [
                MachiningAttribute.X1, MachiningAttribute.Y1, MachiningAttribute.X2, MachiningAttribute.Y2,
                MachiningAttribute.Depth, MachiningAttribute.GrooveWidth
            ],
            XncElementKind.Contour => [MachiningAttribute.Depth],
            XncElementKind.Ellipse =>
            [
                MachiningAttribute.X, MachiningAttribute.Y, MachiningAttribute.Length, MachiningAttribute.Width,
                MachiningAttribute.Angle, MachiningAttribute.Depth
            ],
            XncElementKind.Rectangle =>
            [
                MachiningAttribute.X, MachiningAttribute.Y, MachiningAttribute.Length, MachiningAttribute.Width,
                MachiningAttribute.Angle, MachiningAttribute.CornerRadius, MachiningAttribute.Depth
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }
}
