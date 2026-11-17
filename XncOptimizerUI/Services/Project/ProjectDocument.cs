using System.IO;
using System.Xml.Linq;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.Extensions;

namespace XncOptimizerUI.Services.Project
{
    /// <summary>
    /// The open GibLab <c>.project</c> file, shared by every project service: the parsed document,
    /// typed queries over its operations and goods, the audit description and saving - in place or
    /// as a new file named after the operation (<see cref="ProjectFileNames"/>).
    /// </summary>
    public sealed class ProjectDocument(TimeProvider timeProvider) : IProjectSession
    {
        private XDocument? _document;
        private XElement? _project;
        private string _directory = string.Empty;
        private string _sourceFileName = string.Empty;

        /// <summary>Path the document was opened from or last saved to; empty when nothing is open.</summary>
        public string FullPath { get; private set; } = string.Empty;

        /// <summary>
        /// Web name of the non-UTF-8 encoding the open file was read in (e.g. <c>windows-1251</c>);
        /// <see langword="null"/> when it was read as UTF-8 or nothing is open. Saving always writes UTF-8.
        /// </summary>
        public string? ConvertedFromEncoding { get; private set; }

        public bool IsOpen => _project != null;

        /// <summary>The <c>&lt;project&gt;</c> root element.</summary>
        /// <exception cref="InvalidOperationException">No project is open.</exception>
        public XElement Project => _project ?? throw new InvalidOperationException("No project is opened.");

        public void Open(string path)
        {
            var (document, encoding) = ProjectFileEncoding.Load(path);

            _project = document.GetProject() ?? throw new InvalidDataException($"""File "{path}" contains wrong data""");
            _document = document;
            ConvertedFromEncoding = ProjectFileEncoding.IsUtf8(encoding) ? null : encoding.WebName;
            PrepareIfCadExport(document);
            FullPath = path;
            _directory = Path.GetDirectoryName(path) ?? string.Empty;
            _sourceFileName = Path.GetFileName(path);
        }

        public void Close()
        {
            _document = null;
            _project = null;
            ConvertedFromEncoding = null;
            FullPath = string.Empty;
            _directory = string.Empty;
            _sourceFileName = string.Empty;
        }

        /// <summary>Saves over the file at <see cref="FullPath"/>.</summary>
        public void Save()
        {
            if (_document == null || string.IsNullOrEmpty(FullPath))
            {
                throw new InvalidOperationException("No project is opened");
            }

            _document.Save(FullPath);
        }

        /// <summary>
        /// Saves as a new file next to the opened one, named with <paramref name="suffix"/>; the new
        /// file becomes <see cref="FullPath"/>. Returns its path.
        /// </summary>
        public string SaveAsSuffixed(string suffix)
        {
            FullPath = Path.Combine(_directory, ProjectFileNames.Suffixed(_sourceFileName, suffix));
            Save();

            return FullPath;
        }

        /// <summary>Appends a timestamped line to the project's <c>description</c> attribute.</summary>
        public void AppendDescription(string message)
        {
            // Format string kept byte-identical: this timestamp is written into the saved project
            // XML, so changing it would change output files.
            var line = $"{timeProvider.GetLocalNow().DateTime:yyyy-MM-dd hh:mm:ss} -> {message}\n";
            var description = Project.Attribute("description");

            if (description == null)
            {
                Project.Add(new XAttribute("description", line));
                return;
            }

            description.Value += line;
        }

        /// <summary>
        /// A file read in another encoding is a CAD export: completed by <see cref="CadExportPreparer"/>
        /// and saved as UTF-8 (<see cref="XDocument.Save(string)"/> writes in the declared encoding).
        /// UTF-8 files are left as read, so they save byte-identical.
        /// </summary>
        private void PrepareIfCadExport(XDocument document)
        {
            if (ConvertedFromEncoding == null)
            {
                return;
            }

            CadExportPreparer.Prepare(Project);
            document.Declaration = new XDeclaration("1.0", "UTF-8", document.Declaration?.Standalone);
        }

        /// <summary>Operations of one <c>typeId</c> (see <see cref="GibLabTypes"/>), in document order.</summary>
        public List<XElement> Operations(string typeId) =>
            Project.GetOperations().Where(o => o.GetTypeIdValue() == typeId).ToList();

        /// <summary>Goods of one <c>typeId</c> (see <see cref="GibLabTypes"/>), in document order.</summary>
        public List<XElement> Goods(string typeId) =>
            Project.GetGoods().Where(g => g.GetTypeIdValue() == typeId).ToList();

        public List<XElement> XncOperations() => Operations(GibLabTypes.XncOperation);

        /// <summary>The XNC operations (one per machined face) of the part with <paramref name="partId"/>.</summary>
        public List<XElement> XncOperationsOf(int partId) =>
            XncOperations().Where(o => o.GetPart()?.GetIdIntValue() == partId).ToList();

        /// <summary>Every <c>&lt;part&gt;</c> of every product good.</summary>
        public List<XElement> ProductParts() =>
            Goods(GibLabTypes.ProductGood).SelectMany(g => g.GetParts()).ToList();
    }
}
