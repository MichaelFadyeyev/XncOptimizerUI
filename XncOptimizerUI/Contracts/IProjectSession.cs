namespace XncOptimizerUI.Contracts
{
    /// <summary>The project file currently open; every other project service works on it.</summary>
    public interface IProjectSession
    {
        /// <summary>Path the project was opened from, or the file the last operation saved to; empty when none is open.</summary>
        string FullPath { get; }

        /// <summary>
        /// Web name of the non-UTF-8 encoding the open file was read in (e.g. <c>windows-1251</c>);
        /// <see langword="null"/> when it was read as UTF-8. Saving always writes UTF-8.
        /// </summary>
        string? ConvertedFromEncoding { get; }

        void Open(string path);

        void Close();

        /// <summary>Saves over <see cref="FullPath"/> (in-place edits; see <see cref="IProjectEditor"/>).</summary>
        void Save();

        /// <summary>
        /// Saves to <paramref name="path"/>, which becomes <see cref="FullPath"/> (and the base name of
        /// suffixed outputs); when writing fails the session stays on the previous file.
        /// </summary>
        void SaveAs(string path);
    }
}
