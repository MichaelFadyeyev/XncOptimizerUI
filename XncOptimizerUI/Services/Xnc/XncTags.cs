namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>Element names of an XNC <c>&lt;program&gt;</c> (format: <c>xnc-program-format.md</c>).</summary>
    internal static class XncTags
    {
        public const string Program = "program";
        public const string Tool = "tool";
        public const string Variable = "var";

        public const string FaceBore = "bf";
        public const string TopBore = "bt";
        public const string BottomBore = "bb";
        public const string LeftBore = "bl";
        public const string RightBore = "br";

        public const string Groove = "gr";

        public const string ContourStart = "ms";
        public const string Line = "ml";
        public const string CentreArc = "mac";
        public const string RadiusArc = "ma";
        public const string Rectangle = "mr";
        public const string Ellipse = "me";

        /// <summary>Any drilled hole: on the face or on one of the four edges.</summary>
        public static bool IsBore(string tag) => tag is FaceBore or TopBore or BottomBore or LeftBore or RightBore;

        /// <summary>A segment continuing the <c>&lt;ms&gt;</c> contour it follows.</summary>
        public static bool IsContourSegment(string tag) => tag is Line or CentreArc or RadiusArc;

        /// <summary>The head of a mill: a contour start, a rectangle or an ellipse.</summary>
        public static bool IsMillHead(string tag) => tag is ContourStart or Rectangle or Ellipse;
    }
}
