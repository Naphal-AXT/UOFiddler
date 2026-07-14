using System.IO;
using ThePiper.Housing.Core;
using ThePiper.Housing.IO;

namespace ThePiper.Housing.Housing;

public sealed class HousingParser
{
    public HousingEntry Parse(Stream stream)
    {
        var reader = new HousingBinaryReader(stream);
        _ = new HousingHeaderReader().Read(reader);
        return new HousingEntryReader().Read(reader);
    }
}
