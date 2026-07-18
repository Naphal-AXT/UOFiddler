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
using Ultima;
using UoFiddler.Plugin.UopPacker.Classes;

namespace UoFiddler.Plugin.HousingEditor.Classes
{
    /// <summary>
    /// Repacks a freshly written housing.bin (see <see cref="HousingBinWriter"/>)
    /// into a complete MultiCollection.uop.
    ///
    /// Deliberately reuses <c>UoFiddler.Plugin.UopPacker.Classes.
    /// LegacyMulFileConverter.ToUop</c> instead of a plugin-local
    /// implementation - it already builds a correct MultiCollection.uop from
    /// multi.mul/multi.idx (the other 871 entries besides housing.bin
    /// itself), including the exact housing.bin identifier hash
    /// (0x126D1E99DDEDEE0A) this project independently confirmed while
    /// reverse engineering housing.bin's own binary format. Rewriting that
    /// logic here would just be a worse copy of already-working code.
    ///
    /// This means repacking needs the client's own multi.mul/multi.idx to
    /// exist alongside MultiCollection.uop, to supply the other 871
    /// entries - it is NOT reading them back out of the original
    /// MultiCollection.uop (ToUop only ever builds from mul/idx). Most
    /// Classic clients still ship both side by side (ours does), but a
    /// client that dropped the legacy mul/idx fallback files entirely
    /// can't be repacked this way.
    /// </summary>
    internal static class MultiCollectionRepacker
    {
        /// <summary>
        /// True if this client has what repacking needs: multi.mul and
        /// multi.idx alongside MultiCollection.uop.
        /// </summary>
        public static bool CanRepack(string clientPath)
        {
            return File.Exists(Path.Combine(clientPath, "multi.mul")) &&
                   File.Exists(Path.Combine(clientPath, "multi.idx"));
        }

        /// <summary>
        /// Builds a complete MultiCollection.uop at outputPath, combining
        /// the client's own multi.mul/multi.idx with the given housing.bin
        /// bytes (typically <see cref="HousingBinWriter"/>'s output).
        /// </summary>
        public static void Repack(string clientPath, byte[] housingBinBytes, string outputPath)
        {
            if (!CanRepack(clientPath))
                throw new FileNotFoundException("multi.mul/multi.idx not found next to MultiCollection.uop - can't repack without them.");

            string tempHousingBin = Path.GetTempFileName();

            try
            {
                File.WriteAllBytes(tempHousingBin, housingBinBytes);

                LegacyMulFileConverter.ToUop(
                    Path.Combine(clientPath, "multi.mul"),
                    Path.Combine(clientPath, "multi.idx"),
                    outputPath,
                    FileType.MultiCollection,
                    typeIndex: 0,
                    compressionFlag: CompressionFlag.Zlib,
                    housingBinFile: tempHousingBin);
            }
            finally
            {
                File.Delete(tempHousingBin);
            }
        }
    }
}
