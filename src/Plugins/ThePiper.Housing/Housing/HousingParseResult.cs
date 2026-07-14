using System.Collections.Generic;
using ThePiper.Housing.Core;

namespace ThePiper.Housing.Housing;

public sealed class HousingParseResult
{
    public HousingHeader Header { get; init; } = new();
    public List<HousingEntry> Entries { get; } = new();
}
