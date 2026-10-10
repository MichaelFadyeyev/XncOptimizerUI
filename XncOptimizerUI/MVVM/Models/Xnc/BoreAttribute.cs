namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>An editable bore element attribute: <c>x</c>, <c>y</c>, <c>z</c> or <c>dp</c>.</summary>
    public enum BoreAttribute
    {
        X,
        Y,
        Z,
        Depth
    }

    /// <summary>
    /// Maps bore attributes to their XML names and to the coordinates shown in the bores table.
    /// The table shows a bore's position in the part plane: a face bore as (x, y), a Top/Bottom
    /// edge bore as (x, z), a Left/Right edge bore as (z, y) - the edge coordinate pinned by the
    /// drilled edge itself is not an attribute and never editable.
    /// </summary>
    public static class BoreAttributes
    {
        public static string XmlName(this BoreAttribute attribute) => attribute switch
        {
            BoreAttribute.X => "x",
            BoreAttribute.Y => "y",
            BoreAttribute.Z => "z",
            BoreAttribute.Depth => "dp",
            _ => throw new ArgumentOutOfRangeException(nameof(attribute), attribute, null)
        };

        /// <summary>Attribute shown in the table's X column for a bore drilled into <paramref name="surface"/>.</summary>
        public static BoreAttribute HorizontalAttribute(BoreSurface surface) =>
            surface is BoreSurface.Left or BoreSurface.Right ? BoreAttribute.Z : BoreAttribute.X;

        /// <summary>Attribute shown in the table's Y column for a bore drilled into <paramref name="surface"/>.</summary>
        public static BoreAttribute VerticalAttribute(BoreSurface surface) =>
            surface is BoreSurface.Top or BoreSurface.Bottom ? BoreAttribute.Z : BoreAttribute.Y;

        /// <summary>Whether a bore drilled into <paramref name="surface"/> carries <paramref name="attribute"/>.</summary>
        public static bool AppliesTo(this BoreAttribute attribute, BoreSurface surface) =>
            attribute == BoreAttribute.Depth
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
