namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>A program element shown in its own table: a bore, a groove or a mill head.</summary>
    /// <remarks>Declared in the order of the tables (logs list kinds in this order).</remarks>
    public enum XncElementKind
    {
        /// <summary>A bore on the face or an edge (<c>bf</c>/<c>bt</c>/<c>bb</c>/<c>bl</c>/<c>br</c>).</summary>
        Bore,

        /// <summary>A groove <c>&lt;gr&gt;</c>.</summary>
        Groove,

        /// <summary>The <c>&lt;ms&gt;</c> head of a milling contour.</summary>
        Contour,

        /// <summary>A <c>&lt;me&gt;</c> milling ellipse.</summary>
        Ellipse,

        /// <summary>A <c>&lt;mr&gt;</c> milling rectangle.</summary>
        Rectangle
    }

    /// <summary>
    /// One element of the program of XNC operation <paramref name="OperationId"/>: the
    /// <paramref name="Index"/>-th element of <paramref name="Kind"/> in document order (among every
    /// bore tag for <see cref="XncElementKind.Bore"/>, as <see cref="XncBore.Index"/>; among the elements
    /// of the same tag otherwise, as <see cref="XncElementSource.Index"/>).
    /// </summary>
    public readonly record struct XncElementRef(int OperationId, XncElementKind Kind, int Index);

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
            XncElementKind.Bore => "Bore",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

        /// <summary>
        /// Attributes its table edits through <c>UpdateMachiningElement</c>. A contour only edits its
        /// entry depth: moving the entry point would detach it from the arc segments that start there.
        /// A bore has none here: its attributes depend on its surface and are edited through
        /// <c>UpdateBore</c> (see <c>BoreAttributes</c>).
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
            XncElementKind.Bore => [],
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }
}
