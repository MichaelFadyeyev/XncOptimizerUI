using System.Collections.ObjectModel;

namespace XncOptimizerUI.MVVM.ViewModels.Machining
{
    /// <summary>The elements checked in one program table, in the order they were checked.</summary>
    public sealed class CheckedSet<T> : ObservableCollection<T>
    {
        /// <summary>Adds or removes <paramref name="item"/>: the table's check box callback.</summary>
        public void Set(T item, bool isChecked)
        {
            if (isChecked)
            {
                Add(item);
            }
            else
            {
                Remove(item);
            }
        }

        /// <summary>Keeps a checked element checked after it was re-read as <paramref name="current"/>.</summary>
        public void Replace(T previous, T current)
        {
            var index = IndexOf(previous);

            if (index >= 0)
            {
                this[index] = current;
            }
        }
    }
}
