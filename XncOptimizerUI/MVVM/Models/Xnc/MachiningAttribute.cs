namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>
    /// A numeric program element attribute edited in a program table: a bore's <c>x</c>/<c>y</c>/<c>z</c>/<c>dp</c>,
    /// a groove's <c>x1</c>/<c>y1</c>/<c>x2</c>/<c>y2</c>/<c>dp</c>/<c>t</c>, a mill's <c>x</c>/<c>y</c>/<c>l</c>/<c>w</c>/<c>a</c>/<c>r</c>/<c>dp</c>.
    /// </summary>
    public enum MachiningAttribute
    {
        X,
        Y,
        Z,
        X1,
        Y1,
        X2,
        Y2,
        Depth,
        GrooveWidth,
        Length,
        Width,
        Angle,
        CornerRadius
    }

    /// <summary>XML names and value rules of the <see cref="MachiningAttribute"/>s.</summary>
    public static class MachiningAttributes
    {
        public static string XmlName(this MachiningAttribute attribute) => attribute switch
        {
            MachiningAttribute.X => "x",
            MachiningAttribute.Y => "y",
            MachiningAttribute.Z => "z",
            MachiningAttribute.X1 => "x1",
            MachiningAttribute.Y1 => "y1",
            MachiningAttribute.X2 => "x2",
            MachiningAttribute.Y2 => "y2",
            MachiningAttribute.Depth => "dp",
            MachiningAttribute.GrooveWidth => "t",
            MachiningAttribute.Length => "l",
            MachiningAttribute.Width => "w",
            MachiningAttribute.Angle => "a",
            MachiningAttribute.CornerRadius => "r",
            _ => throw new ArgumentOutOfRangeException(nameof(attribute), attribute, null)
        };

        /// <summary>
        /// Why <paramref name="value"/> is out of range for <paramref name="attribute"/>, or <c>null</c>
        /// when it is acceptable: depth and sizes must be positive, a corner radius not negative;
        /// coordinates and angles may be any number.
        /// </summary>
        public static string? CheckRange(this MachiningAttribute attribute, double value) => attribute switch
        {
            MachiningAttribute.Depth when value <= 0d => "Depth must be greater than 0",
            MachiningAttribute.GrooveWidth or MachiningAttribute.Width when value <= 0d => "Width must be greater than 0",
            MachiningAttribute.Length when value <= 0d => "Length must be greater than 0",
            MachiningAttribute.CornerRadius when value < 0d => "Corner radius must not be negative",
            _ => null
        };
    }

    /// <summary>
    /// Maps bore attributes to the coordinates shown in the bores table. The table shows a bore's
    /// position in the part plane: a face bore as (x, y), a Top/Bottom edge bore as (x, z), a
    /// Left/Right edge bore as (z, y) - the edge coordinate pinned by the drilled edge itself is not
    /// an attribute and never editable.
    /// </summary>
    public static class BoreAttributes
    {
        /// <summary>Attribute shown in the table's X column for a bore drilled into <paramref name="surface"/>.</summary>
        public static MachiningAttribute HorizontalAttribute(BoreSurface surface) =>
            surface is BoreSurface.Left or BoreSurface.Right ? MachiningAttribute.Z : MachiningAttribute.X;

        /// <summary>Attribute shown in the table's Y column for a bore drilled into <paramref name="surface"/>.</summary>
        public static MachiningAttribute VerticalAttribute(BoreSurface surface) =>
            surface is BoreSurface.Top or BoreSurface.Bottom ? MachiningAttribute.Z : MachiningAttribute.Y;

        /// <summary>Whether a bore drilled into <paramref name="surface"/> carries the editable <paramref name="attribute"/>.</summary>
        public static bool AppliesTo(this MachiningAttribute attribute, BoreSurface surface) =>
            attribute == MachiningAttribute.Depth
            || attribute == HorizontalAttribute(surface)
            || attribute == VerticalAttribute(surface);

        /// <summary>Surface encoded by a bore element name (<c>bf</c>/<c>bt</c>/<c>bb</c>/<c>bl</c>/<c>br</c>), or <c>null</c> for any other element.</summary>
        public static BoreSurface? SurfaceOf(string elementName) => elementName switch
        {
            "bf" => BoreSurface.Face,
            "bt" => BoreSurface.Top,
            "bb" => BoreSurface.Bottom,
            "bl" => BoreSurface.Left,
            "br" => BoreSurface.Right,
            _ => null
        };
    }
}
