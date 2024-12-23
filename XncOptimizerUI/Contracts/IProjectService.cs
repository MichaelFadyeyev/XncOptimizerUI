using System.Collections.ObjectModel;
using XncOptimizerUI.MVVM.Models;

namespace XncOptimizerUI.Contracts
{
    public interface IProjectService
    {
        void OpenProject(string path);
        void GroupIdenticalElements(ref string log);
        void PrepForSplitAlongX(ref string log, string searchText);
        public List<Part> ReadParts();

    }
}
