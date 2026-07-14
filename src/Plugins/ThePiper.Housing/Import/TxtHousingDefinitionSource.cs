using System.Collections.Generic;
using ThePiper.Housing.Core;

namespace ThePiper.Housing.Import;

public sealed class TxtHousingDefinitionSource : IHousingDefinitionSource
{
    public IEnumerable<TileReference> Load()
    {
        yield break;
    }
}
