using System.Collections.Generic;
using ThePiper.Housing.Core;

namespace ThePiper.Housing.Correlation;

public sealed class CorrelationResult
{
    public List<TileReference> Matches { get; } = new();
    public List<ushort> MissingTileIds { get; } = new();
}
