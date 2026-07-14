namespace ThePiper.Housing.Core;

public sealed class TileReference
{
    public ushort TileId { get; init; }
    public string Category { get; init; } = string.Empty;
    public string SourceFile { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}
