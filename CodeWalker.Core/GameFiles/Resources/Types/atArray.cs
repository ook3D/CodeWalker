using System.ComponentModel;

namespace CodeWalker.GameFiles
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class atArray<T> : ResourceSimpleList64<T> where T : IResourceSystemBlock, new()
    {
        public ulong ElementsPointer => EntriesPointer;
        public ushort Count => EntriesCount;
        public ushort Capacity => EntriesCapacity;

        public T[] Items
        {
            get => data_items;
            set => data_items = value;
        }
    }
}
