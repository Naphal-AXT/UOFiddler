using System.Collections.Generic;
using ThePiper.Housing.Core;

namespace ThePiper.Housing.Import;

public interface IHousingDefinitionSource
{
    IEnumerable<TileReference> Load();
}
