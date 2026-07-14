namespace ThePiper.Housing.IO;

public sealed class HousingHeaderReader
{
    public HousingHeader Read(HousingBinaryReader reader)
    {
        return new HousingHeader
        {
            Signature = reader.ReadInt32(),
            Version = reader.ReadInt32()
        };
    }
}

public sealed class HousingHeader
{
    public int Signature { get; set; }
    public int Version { get; set; }
}
