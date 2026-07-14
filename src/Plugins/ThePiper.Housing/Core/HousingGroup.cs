using System.Collections.Generic;

namespace ThePiper.Housing.Core;

public sealed class HousingGroup
{
    public int GroupId { get; set; }
    public List<HousingPiece> Pieces { get; } = new();
}
