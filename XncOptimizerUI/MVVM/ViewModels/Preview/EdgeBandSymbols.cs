using XncOptimizerUI.MVVM.ViewModels.Parts;

namespace XncOptimizerUI.MVVM.ViewModels.Preview
{
    /// <summary>
    /// The edge-band symbol shown at each side of the part preview; empty for an unbanded edge.
    /// Built in the part's own frame (<c>elt</c>/<c>elr</c>/<c>elb</c>/<c>ell</c>, as at
    /// <c>turn</c> 0) and moved to the screen sides with <see cref="Turned"/>.
    /// </summary>
    public sealed record EdgeBandSymbols(string Top, string Right, string Bottom, string Left)
    {
        /// <summary>No band on any edge.</summary>
        public static EdgeBandSymbols None { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty);

        /// <summary>The symbols of <paramref name="part"/>'s four edge bands, each looked up by <paramref name="symbolOf"/>.</summary>
        public static EdgeBandSymbols Of(PartVM part, Func<int?, string> symbolOf) => new(
            symbolOf(part.TopBandingId),
            symbolOf(part.RightBandingId),
            symbolOf(part.BottomBandingId),
            symbolOf(part.LeftBandingId));

        /// <summary>
        /// The symbols after the part is turned by XNC <paramref name="turn"/> (clockwise quarter
        /// turns, any integer): each edge moves to the next side clockwise per step
        /// (Top → Right → Bottom → Left), like the edge machining of a rotated program.
        /// </summary>
        public EdgeBandSymbols Turned(int turn) =>
            Enumerable.Range(0, ((turn % 4) + 4) % 4)
                .Aggregate(this, (symbols, _) => symbols.TurnedQuarterClockwise());

        private EdgeBandSymbols TurnedQuarterClockwise() => new(Top: Left, Right: Top, Bottom: Right, Left: Bottom);
    }
}
