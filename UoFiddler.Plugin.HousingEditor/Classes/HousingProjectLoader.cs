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
using System.IO;

namespace UoFiddler.Plugin.HousingEditor
{
    /// <summary>
    /// Detects whether a client folder is pre-UOP (legacy TXT files) or
    /// post-UOP (MultiCollection.uop) and loads it with the matching
    /// reader.
    /// </summary>
    public static class HousingProjectLoader
    {
        public static HousingProject Load(string clientPath)
        {
            if (String.IsNullOrWhiteSpace(clientPath))
                throw new ArgumentNullException(nameof(clientPath));

            string multiCollectionPath =
                Path.Combine(clientPath, "MultiCollection.uop");

            return File.Exists(multiCollectionPath)
                ? UopReader.Load(clientPath)
                : LegacyReader.Load(clientPath);
        }
    }
}
