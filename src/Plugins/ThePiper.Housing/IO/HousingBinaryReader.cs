using System.IO;

namespace ThePiper.Housing.IO;

public sealed class HousingBinaryReader
{
    private readonly BinaryReader _reader;

    public HousingBinaryReader(Stream stream)
    {
        _reader = new BinaryReader(stream);
    }

    public int ReadInt32() => _reader.ReadInt32();
    public ushort ReadUInt16() => _reader.ReadUInt16();
    public byte ReadByte() => _reader.ReadByte();
}
