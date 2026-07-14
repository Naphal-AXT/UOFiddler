using ThePiper.Housing.Core;
using ThePiper.Housing.IO;

namespace ThePiper.Housing.Housing;

public sealed class HousingGroupReader
{
    public HousingGroup Read(HousingBinaryReader reader)
    {
        return new HousingGroup
        {
            GroupId = reader.ReadInt32()
        };
    }
}
