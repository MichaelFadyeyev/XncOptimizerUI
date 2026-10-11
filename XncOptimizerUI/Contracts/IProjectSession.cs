namespace XncOptimizerUI.Contracts
{
    /// <summary>The project file currently open; every other project service works on it.</summary>
    public interface IProjectSession
    {
        /// <summary>Path the project was opened from, or the file the last operation saved to; empty when none is open.</summary>
        string FullPath { get; }

        void Open(string path);

        void Close();

        /// <summary>Saves over <see cref="FullPath"/> (in-place edits; see <see cref="IProjectEditor"/>).</summary>
        void Save();
    }
}
