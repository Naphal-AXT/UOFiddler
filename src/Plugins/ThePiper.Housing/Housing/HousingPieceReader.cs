using ThePiper.Housing.Core;
using ThePiper.Housing.IO;

namespace ThePiper.Housing.Housing;

public sealed class HousingPieceReader
{
    public HousingPiece Read(HousingBinaryReader reader)
    {
        return new HousingPiece
        {
            TileId = reader.ReadUInt16(),
            X = (short)reader.ReadByte(),
            Y = (short)reader.ReadByte(),
            Z = unchecked((sbyte)reader.ReadByte())
        };
    }
}
