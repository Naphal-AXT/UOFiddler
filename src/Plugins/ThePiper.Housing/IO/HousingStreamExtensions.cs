using System.IO;

namespace ThePiper.Housing.IO;

internal static class HousingStreamExtensions
{
    public static bool EndOfStream(this BinaryReader reader)
        => reader.BaseStream.Position >= reader.BaseStream.Length;
}
