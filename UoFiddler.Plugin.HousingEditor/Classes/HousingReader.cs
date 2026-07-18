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
using Ultima.Helpers;

namespace UoFiddler.Plugin.HousingEditor.Classes
{
    /// <summary>
    /// Extracts and decompresses housing.bin out of a post-UOP client's
    /// MultiCollection.uop.
    ///
    /// This walks the UOP container directly (header + file-table block
    /// chain + hashed file name lookup) instead of extending Ultima's
    /// core UopFileAccessor, since named-resource lookup by hash is
    /// specific to this one file and there's no reason for the shared
    /// Ultima library to carry it.
    ///
    /// housing.bin is confirmed to be the packed replacement for the 7
    /// legacy TXT files, per client/build/multicollection.def:
    ///     &lt;simplemap resourcetype="20" fileFormat="packed"
    ///                 resourcenumber="0"
    ///                 filename="Build/MultiCollection/housing.bin" /&gt;
    /// </summary>
    internal static class HousingReader
    {
        private const string HousingResource =
            "build/multicollection/housing.bin";

        private const int UopMagic = 0x50594D;

        public static byte[] ReadHousingBin(string multiCollectionPath)
        {
            if (String.IsNullOrWhiteSpace(multiCollectionPath))
                throw new ArgumentNullException(nameof(multiCollectionPath));

            if (!File.Exists(multiCollectionPath))
                throw new FileNotFoundException(multiCollectionPath);

            using FileStream stream = new FileStream(
                multiCollectionPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            using BinaryReader reader = new BinaryReader(stream);

            if (reader.ReadInt32() != UopMagic)
                throw new InvalidDataException(
                    "Not a valid UOP file: " + multiCollectionPath);

            _ = reader.ReadUInt32(); // version
            _ = reader.ReadUInt32(); // format timestamp / signature

            long nextBlock = reader.ReadInt64();

            _ = reader.ReadUInt32(); // block capacity
            _ = reader.ReadInt32();  // file count

            ulong targetHash = UopUtils.HashFileName(
                HousingResource.ToLowerInvariant());

            long entryOffset = -1;
            int compressedLength = 0;
            int decompressedLength = 0;
            CompressionFlag flag = CompressionFlag.None;

            while (nextBlock != 0)
            {
                stream.Seek(nextBlock, SeekOrigin.Begin);

                int filesInBlock = reader.ReadInt32();
                nextBlock = reader.ReadInt64();

                for (int i = 0; i < filesInBlock; i++)
                {
                    long offset = reader.ReadInt64();
                    int headerLength = reader.ReadInt32();
                    int fileCompressedLength = reader.ReadInt32();
                    int fileDecompressedLength = reader.ReadInt32();
                    ulong hash = reader.ReadUInt64();

                    _ = reader.ReadUInt32(); // adler32/crc

                    short fileFlag = reader.ReadInt16();

                    if (offset == 0 || hash != targetHash)
                        continue;

                    entryOffset = offset + headerLength;
                    compressedLength = fileCompressedLength;
                    decompressedLength = fileDecompressedLength;
                    flag = (CompressionFlag)fileFlag;
                }
            }

            if (entryOffset < 0)
                throw new FileNotFoundException(
                    "housing.bin not found inside MultiCollection.uop");

            stream.Seek(entryOffset, SeekOrigin.Begin);

            byte[] data = reader.ReadBytes(compressedLength);

            if (flag == CompressionFlag.Zlib)
            {
                (bool success, byte[] result) = UopUtils.Decompress(data);

                if (!success)
                    throw new InvalidDataException(
                        "Failed to decompress housing.bin.");

                if (result.Length != decompressedLength)
                    throw new InvalidDataException(
                        $"housing.bin decompressed to {result.Length} bytes, " +
                        $"expected {decompressedLength}.");

                return result;
            }

            return data;
        }
    }
}
