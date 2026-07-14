using System.Collections.Generic;

namespace ThePiper.Housing.Core;

public sealed class HousingEntry
{
    public int EntryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<HousingGroup> Groups { get; } = new();
}
