namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>
    /// Where one render of the part preview puts everything, in canvas px: the canvas size
    /// (the viewport, or larger when zoomed in) and the <see cref="FaceLayout"/> of the Face
    /// rectangle and its four side views. The face, a gap and one sheet-thickness band on each
    /// side are fitted into the viewport at one uniform scale, centred, then magnified by the
    /// zoom factor.
    /// </summary>
    internal sealed record PartPreviewLayout(double CanvasWidth, double CanvasHeight, FaceLayout Face)
    {
        /// <summary>
        /// Lays the part out in a <paramref name="viewportWidth"/> x <paramref name="viewportHeight"/>
        /// viewport, or returns <c>null</c> when there is nothing to draw (no part size, or no room
        /// inside the margins).
        /// </summary>
        /// <param name="lengthMm">Part size along X (horizontal).</param>
        /// <param name="widthMm">Part size along Y (vertical, pointing down).</param>
        /// <param name="sheetThicknessMm">Depth of every side view; 0 collapses the bands to <see cref="PartPreviewMetrics.MinBandThickness"/>.</param>
        /// <param name="zoom">Magnification of the fit-to-view scale (1 = fit).</param>
        public static PartPreviewLayout? Fit(
            double viewportWidth,
            double viewportHeight,
            double lengthMm,
            double widthMm,
            double sheetThicknessMm,
            double zoom,
            PartPreviewMetrics metrics)
        {
            if (lengthMm <= 0 || widthMm <= 0 || viewportWidth <= 0 || viewportHeight <= 0)
            {
                return null;
            }

            var availableWidth = viewportWidth - (2 * metrics.Margin);
            var availableHeight = viewportHeight - (2 * metrics.Margin);

            if (availableWidth <= 0 || availableHeight <= 0)
            {
                return null;
            }

            // Face plus one gap and one band on each side, all in mm.
            var perSideMm = metrics.GapMm + sheetThicknessMm;
            var drawingLengthMm = lengthMm + (2 * perSideMm);
            var drawingWidthMm = widthMm + (2 * perSideMm);
            var scale = Math.Min(availableWidth / drawingLengthMm, availableHeight / drawingWidthMm) * zoom;

            if (scale <= 0)
            {
                return null;
            }

            var canvasWidth = Math.Max(viewportWidth, (drawingLengthMm * scale) + (2 * metrics.Margin));
            var canvasHeight = Math.Max(viewportHeight, (drawingWidthMm * scale) + (2 * metrics.Margin));

            var faceWidth = lengthMm * scale;
            var faceHeight = widthMm * scale;
            var gap = metrics.GapMm * scale;
            var bandThickness = Math.Max(sheetThicknessMm * scale, metrics.MinBandThickness);

            // Top-left corner of the face rectangle, the whole group centred in the canvas.
            var slot = gap + bandThickness;
            var originX = ((canvasWidth - (faceWidth + (2 * slot))) / 2) + slot;
            var originY = ((canvasHeight - (faceHeight + (2 * slot))) / 2) + slot;

            return new PartPreviewLayout(
                canvasWidth,
                canvasHeight,
                new FaceLayout(originX, originY, faceWidth, faceHeight, scale, gap, bandThickness));
        }

        /// <summary>The Face rectangle.</summary>
        public PxRect FaceRect => new(Face.OriginX, Face.OriginY, Face.FaceWidth, Face.FaceHeight);

        /// <summary>
        /// The four side views, tagged by side: Top/Bottom as long as the face is wide,
        /// Left/Right as long as it is high, each one band thick and one gap away from the face.
        /// </summary>
        public IReadOnlyList<(string Side, PxRect Rect)> Bands
        {
            get
            {
                var (x, y, w, h, gap, band) = (Face.OriginX, Face.OriginY, Face.FaceWidth, Face.FaceHeight, Face.Gap, Face.BandThickness);

                return
                [
                    ("Top", new PxRect(x, y - gap - band, w, band)),
                    ("Bottom", new PxRect(x, y + h + gap, w, band)),
                    ("Left", new PxRect(x - gap - band, y, band, h)),
                    ("Right", new PxRect(x + w + gap, y, band, h)),
                ];
            }
        }
    }
}
