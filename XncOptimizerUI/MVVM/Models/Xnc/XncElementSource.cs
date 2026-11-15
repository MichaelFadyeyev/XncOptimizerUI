namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>
    /// Where a read program element came from: its position among the elements of the same tag in
    /// its program (document order) and its authored expression attribute texts (e.g. <c>x1="dx-10"</c>),
    /// so an edit can find the element again and an editor can show what was written.
    /// </summary>
    public sealed record XncElementSource(int Index, IReadOnlyDictionary<string, string> Texts)
    {
        /// <summary>No source: an element that was not read from a program.</summary>
        public static XncElementSource None { get; } = new(-1, new Dictionary<string, string>());

        /// <summary>Authored text of attribute <paramref name="xmlName"/>, or <c>null</c> when absent.</summary>
        public string? Text(string xmlName) => Texts.TryGetValue(xmlName, out var text) ? text : null;
    }
}
