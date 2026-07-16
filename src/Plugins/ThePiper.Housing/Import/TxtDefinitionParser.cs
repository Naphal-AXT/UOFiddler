using System;
using System.IO;
using ThePiper.Housing.Core;

namespace ThePiper.Housing.Import;

internal static class TxtDefinitionParser
{
    public static bool TryParse(string line, string sourceFile, out TileReference tile)
    {
        tile = default!;
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) return false;

        var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !ushort.TryParse(parts[0], out var id)) return false;

        tile = new TileReference
        {
            TileId = id,
            SourceFile = Path.GetFileName(sourceFile),
            Category = Path.GetFileNameWithoutExtension(sourceFile),
            Name = parts.Length > 1 ? parts[1] : string.Empty
        };
        return true;
    }
}