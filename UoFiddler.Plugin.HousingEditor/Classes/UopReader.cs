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
using UoFiddler.Plugin.HousingEditor.Classes;

namespace UoFiddler.Plugin.HousingEditor
{
    /// <summary>
    /// Loads housing.bin out of a post-UOP client's MultiCollection.uop,
    /// via Classes/HousingReader (which walks the UOP container itself -
    /// see that class for details on the format and why it isn't part
    /// of the shared Ultima library).
    ///
    /// Its internal per-object binary layout is NOT yet reverse
    /// engineered here - that requires comparing real decompressed
    /// bytes against a known-good legacy TXT export from a client of a
    /// similar version (see HousingCorrelation.ExportRawBinary and
    /// HousingCorrelation.Export, which produce the two sides of that
    /// comparison). Today this reader only extracts and exposes the
    /// raw decompressed bytes for inspection.
    /// </summary>
    public static class UopReader
    {
        public static HousingProject Load(string clientPath)
        {
            if (String.IsNullOrWhiteSpace(clientPath))
                throw new ArgumentNullException(nameof(clientPath));

            string multiCollectionPath =
                Path.Combine(clientPath, "MultiCollection.uop");

            if (!File.Exists(multiCollectionPath))
                throw new FileNotFoundException(multiCollectionPath);

            HousingProject project = new HousingProject
            {
                ClientPath = clientPath,
                ClientType = ClientType.Uop,
                RawHousingBin = HousingReader.ReadHousingBin(
                    multiCollectionPath)
            };

            return project;
        }
    }
}
