using System.Windows.Media;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc.Geometry;

namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>
    /// Where the Face rectangle and the four side bands of one preview render sit, in canvas px,
    /// plus the mm-to-px <see cref="Scale"/>. Every overlay renderer draws against it.
    /// </summary>
    internal readonly record struct FaceLayout(
        double OriginX,
        double OriginY,
        double FaceWidth,
        double FaceHeight,
        double Scale,
        double Gap,
        double BandThickness)
    {
        /// <summary>Face width in program mm.</summary>
        public double FaceWidthMm => FaceWidth / Scale;

        /// <summary>Face height in program mm.</summary>
        public double FaceHeightMm => FaceHeight / Scale;

        /// <summary>A length in program mm, in px.</summary>
        public double MmToPx(double mm) => mm * Scale;

        /// <summary>Canvas x of program x <paramref name="xMm"/>.</summary>
        public double XPx(double xMm) => OriginX + (xMm * Scale);

        /// <summary>Canvas y of program y <paramref name="yMm"/>.</summary>
        public double YPx(double yMm) => OriginY + (yMm * Scale);

        /// <summary>Canvas position of program point <paramref name="mm"/>.</summary>
        public Vec2 ToPx(Vec2 mm) => new(XPx(mm.X), YPx(mm.Y));

        /// <summary>Canvas coordinate of <paramref name="mm"/> along a band's length: x on a horizontal (Top/Bottom) band, else y.</summary>
        public double AlongPx(bool horizontal, double mm) => horizontal ? XPx(mm) : YPx(mm);

        /// <summary>
        /// The edges of one side band: Top/Bottom when <paramref name="horizontal"/>, else
        /// Left/Right; Top/Left when <paramref name="near"/>.
        /// </summary>
        public BandEdges Band(bool horizontal, bool near)
        {
            var direction = near ? -1d : 1d;
            var nearEdge = horizontal
                ? (near ? OriginY - Gap : OriginY + FaceHeight + Gap)
                : (near ? OriginX - Gap : OriginX + FaceWidth + Gap);

            return new BandEdges(nearEdge, nearEdge + (direction * BandThickness), direction);
        }
    }

    /// <summary>
    /// The across-band edges of one side band in px: <see cref="Near"/> faces the Face rectangle,
    /// <see cref="Far"/> is the outer edge, and <see cref="Direction"/> (-1 for Top/Left, +1 for
    /// Bottom/Right) points from near to far.
    /// </summary>
    internal readonly record struct BandEdges(double Near, double Far, double Direction)
    {
        /// <summary>
        /// The edge a depth into the band is measured from and the direction it grows in: the
        /// inner edge growing outward when <paramref name="fromInner"/>, else the outer edge
        /// growing inward.
        /// </summary>
        public (double Start, double Direction) Anchor(bool fromInner) =>
            fromInner ? (Near, Direction) : (Far, -Direction);

        /// <summary>The across-band px coordinate <paramref name="depthPx"/> into the band from the edge <see cref="Anchor"/> picks.</summary>
        public double At(double depthPx, bool fromInner)
        {
            var (start, direction) = Anchor(fromInner);

            return start + (direction * depthPx);
        }
    }

    /// <summary>An axis-aligned rectangle in canvas px.</summary>
    internal readonly record struct PxRect(double Left, double Top, double Width, double Height);

    /// <summary>A line segment in canvas px.</summary>
    internal readonly record struct PxLine(double X1, double Y1, double X2, double Y2);

    /// <summary>Identifies the bore (and its owning program, for the <c>Side</c> colour) a shape belongs to.</summary>
    internal readonly record struct BoreTag(XncProgram Program, XncBore Bore);

    /// <summary>Identifies the groove (and its owning program, for the <c>Side</c> colour) a shape belongs to.</summary>
    internal readonly record struct GrooveTag(XncProgram Program, XncGrooving Groove);

    /// <summary>Identifies the mill (<see cref="XncMillingContour"/>, <see cref="XncMillingRectangle"/> or <see cref="XncMillingEllipse"/>) and its owning program a shape belongs to.</summary>
    internal readonly record struct MillTag(XncProgram Program, object Mill);

    /// <summary>
    /// Screen-space metrics of the part drawing: the face-to-band gap (mm, scaled with the
    /// part), the margin around the drawing (px) and the thinnest band drawn (px).
    /// </summary>
    internal readonly record struct PartPreviewMetrics(double GapMm, double Margin, double MinBandThickness);

    /// <summary>Styling of the part itself: face, side bands, outline and texture mark.</summary>
    internal readonly record struct PartBrushes(
        Brush Face,
        Brush Edge,
        Brush Outline,
        double OutlineThickness,
        Brush Texture,
        double TextureThickness);

    /// <summary>Styling of the fixed viewport decorations: the axis glyph and the turn label.</summary>
    internal readonly record struct DecorationBrushes(
        Brush AxisX,
        Brush AxisY,
        double AxisThickness,
        Brush TurnLabel,
        double TurnLabelFontSize);

    /// <summary>
    /// Bore/groove styling. <see cref="Thickness2Px"/> is used for circular bore symbols and solid
    /// groove rectangles; <see cref="Thickness1Px"/> for rectangular bore symbols, bore center
    /// lines, and every dashed groove line. <see cref="DashLengthPx"/> is the screen-space dash
    /// length used by dashed groove lines; <see cref="CenterLineDashMm"/> is the model-space dash
    /// length used by a bore's crossed center lines when its diameter exceeds
    /// <see cref="PartPreviewOverlayGeometry.DashedCrossLineDiameterMm"/> (scaled to px per draw, since it
    /// must grow/shrink with zoom).
    /// </summary>
    internal readonly record struct BoreBrushes(
        Brush SideTrue,
        Brush SideFalse,
        Brush Selected,
        Brush ThroughFill,
        double Thickness2Px,
        double Thickness1Px,
        double CenterLineOvershootMm,
        double DashLengthPx,
        double CenterLineDashMm);

    /// <summary>Mill styling; <see cref="Lines"/> styles the tool path and start marker like bores.</summary>
    internal readonly record struct MillBrushes(
        Brush Face,
        Brush ThroughCutOff,
        Brush BlindCutOff,
        Brush CentreLine,
        double CentreLineThickness,
        double StripOpacity,
        BoreBrushes Lines);
}
