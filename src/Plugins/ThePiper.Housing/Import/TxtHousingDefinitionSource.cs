using System.Collections.Generic;
using System.IO;
using ThePiper.Housing.Core;

namespace ThePiper.Housing.Import;

public sealed class TxtHousingDefinitionSource : IHousingDefinitionSource
{
    public string? RootPath { get; init; }

    public IEnumerable<TileReference> Load()
    {
        if (string.IsNullOrWhiteSpace(RootPath) || !Directory.Exists(RootPath))
            yield break;

        foreach (var file in Directory.EnumerateFiles(RootPath, "*.txt"))
        {
            foreach (var line in File.ReadLines(file))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;
            }
        }
    }
}