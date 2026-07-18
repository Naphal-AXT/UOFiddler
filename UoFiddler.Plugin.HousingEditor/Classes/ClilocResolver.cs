// /***************************************************************************
//  *
//  * $Author:
//  *
//  * "THE BEER-WARE LICENSE"
//  * As long as you retain this notice you can do whatever you want with
//  * this stuff. If we meet some day, and you think this stuff is worth it,
//  * you can buy me a beer in return.
//  *
//  ***************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using Ultima;

namespace UoFiddler.Plugin.HousingEditor.Classes
{
    /// <summary>
    /// Resolves housing.bin's clilocId field into an in-game label, using the
    /// client's own Cliloc.esp/Cliloc.enu next to housing.bin.
    ///
    /// Both files share the same on-disk format (confirmed against real
    /// client data): a 6-byte header, then repeating {u32 id, u8 flag,
    /// u16 length, UTF8 text} records, wrapped in Mythic-compressed framing
    /// (<see cref="Ultima.Helpers.MythicDecompress"/>). Already fully
    /// implemented and tested in <see cref="Ultima.StringList"/> - this
    /// class only picks which file(s) to load and in what order.
    ///
    /// Cliloc.esp (Spanish) is preferred over Cliloc.enu (English) - it has
    /// fewer entries (Spanish localization lags English), so lookups that
    /// miss in .esp fall back to .enu rather than returning nothing.
    /// </summary>
    internal static class ClilocResolver
    {
        private sealed class ClientClilocs
        {
            public StringList? Primary;
            public StringList? Fallback;
        }

        private static readonly Dictionary<string, ClientClilocs> Cache =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Resolves clilocId to its in-game text for the client at
        /// clientPath, preferring Cliloc.esp over Cliloc.enu. Returns an
        /// empty string if neither file has the id, or neither file exists.
        /// </summary>
        public static string Resolve(string clientPath, int clilocId)
        {
            if (clilocId == 0 || string.IsNullOrWhiteSpace(clientPath))
                return string.Empty;

            ClientClilocs clilocs = GetOrLoad(clientPath);

            string? text = clilocs.Primary?.GetString(clilocId);

            if (!string.IsNullOrEmpty(text))
                return text;

            return clilocs.Fallback?.GetString(clilocId) ?? string.Empty;
        }

        private static ClientClilocs GetOrLoad(string clientPath)
        {
            if (Cache.TryGetValue(clientPath, out ClientClilocs? cached))
                return cached;

            ClientClilocs clilocs = new()
            {
                Primary = TryLoad(clientPath, "esp"),
                Fallback = TryLoad(clientPath, "enu")
            };

            Cache[clientPath] = clilocs;
            return clilocs;
        }

        private static StringList? TryLoad(string clientPath, string language)
        {
            string path = Path.Combine(clientPath, $"Cliloc.{language}");

            if (!File.Exists(path))
                return null;

            try
            {
                return new StringList(language, path, decompress: true);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
