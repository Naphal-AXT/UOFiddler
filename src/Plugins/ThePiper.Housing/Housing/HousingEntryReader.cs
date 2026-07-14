using ThePiper.Housing.Core;
using ThePiper.Housing.IO;

namespace ThePiper.Housing.Housing;

public sealed class HousingEntryReader
{
    public HousingEntry Read(HousingBinaryReader reader)
    {
        return new HousingEntry
        {
            EntryId = reader.ReadInt32()
        };
    }
}
