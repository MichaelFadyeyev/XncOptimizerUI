using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models;

namespace XncOptimizerUI.Test.Fakes
{
    /// <summary>
    /// Hand-written stand-in for <see cref="IProjectService"/>.
    /// A mocking library cannot express this interface: C# forbids passing a
    /// method call such as Arg.Any&lt;string&gt;() in a <c>ref</c> position, so the
    /// <c>ref string log</c> parameters cannot be matched. Writing it out also lets
    /// the fake append to the log the way the real service does.
    /// </summary>
    public class FakeProjectService : IProjectService
    {
        public List<Part> Parts { get; set; } = [];
        public List<Band> Bands { get; set; } = [];
        public List<Sheet> Sheets { get; set; } = [];

        public string FullPath { get; set; } = string.Empty;

        /// <summary>Text each operation appends to the log when called.</summary>
        public string LogToAppend { get; set; } = string.Empty;

        public bool GroupIdenticalElementsResult { get; set; } = true;
        public bool ReplaceXncProgramsResult { get; set; } = true;
        public bool UpdatePartResult { get; set; } = true;

        public List<string> Calls { get; } = [];

        public int SaveProjectCount { get; private set; }
        public Part? LastUpdatedPart { get; private set; }
        public IList<Part>? LastReplaceTargets { get; private set; }

        public void OpenProject(string path)
        {
            Calls.Add(nameof(OpenProject));
            FullPath = path;
        }

        public void CloseProject() => Calls.Add(nameof(CloseProject));

        public bool GroupIdenticalElements(ref string log)
        {
            Calls.Add(nameof(GroupIdenticalElements));
            log += LogToAppend;

            return GroupIdenticalElementsResult;
        }

        public void PrepForSplitAlongX(ref string log, string[] selectedPartsIds)
        {
            Calls.Add(nameof(PrepForSplitAlongX));
            log += LogToAppend;
        }

        public bool ReplaceXncPrograms(ref string log, Part sourcePart, IList<Part> targetParts)
        {
            Calls.Add(nameof(ReplaceXncPrograms));
            LastReplaceTargets = targetParts;
            log += LogToAppend;

            return ReplaceXncProgramsResult;
        }

        public int GetXncProgramsCount(int partId)
        {
            Calls.Add(nameof(GetXncProgramsCount));

            return 0;
        }

        public void SaveProject()
        {
            Calls.Add(nameof(SaveProject));
            SaveProjectCount++;
        }

        public bool UpdatePart(ref string log, Part part)
        {
            Calls.Add(nameof(UpdatePart));
            LastUpdatedPart = part;

            return UpdatePartResult;
        }

        public List<Part> ReadParts()
        {
            Calls.Add(nameof(ReadParts));

            return Parts;
        }

        public List<Band> ReadBands()
        {
            Calls.Add(nameof(ReadBands));

            return Bands;
        }

        public List<Sheet> ReadSheets()
        {
            Calls.Add(nameof(ReadSheets));

            return Sheets;
        }
    }
}
