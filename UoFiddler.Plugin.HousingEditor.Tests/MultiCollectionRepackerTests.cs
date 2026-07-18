using System.IO;
using UoFiddler.Plugin.HousingEditor.Classes;
using Xunit;

namespace UoFiddler.Plugin.HousingEditor.Tests;

public class MultiCollectionRepackerTests
{
    private static MultiCollectionRepacker.RawEntry MakeEntry(ulong hash, int seed) =>
        new(hash, new byte[] { (byte)seed, (byte)(seed + 1), (byte)(seed + 2) }, DecompressedSize: 100 + seed, Flag: 0);

    /// <summary>
    /// Regression test for a real bug: the low-level UOP table writer only
    /// repositioned back to a table's TOC-entries area as a side effect of
    /// patching the "next table" pointer, which happens for every table
    /// except the last (there IS no next table to point at). The last
    /// table's TOC entries landed after all the file's data instead of at
    /// their real offset, which stayed all-zero - read by any UOP parser as
    /// empty/absent entries. On the real client this was verified against
    /// (872 entries, 9 tables of 100), this silently dropped the last 72
    /// entries every time. A single-table file (<= 100 entries, this test)
    /// hits exactly the same code path, since table 0 is *always* the last
    /// table when there's only one.
    /// </summary>
    [Fact]
    public void WriteRawUop_SingleTable_AllEntriesSurvive()
    {
        List<MultiCollectionRepacker.RawEntry> entries = new();
        for (int i = 0; i < 5; i++)
            entries.Add(MakeEntry((ulong)(0x1000 + i), i));

        string path = Path.GetTempFileName();
        try
        {
            MultiCollectionRepacker.WriteRawUop(entries, path);
            List<MultiCollectionRepacker.RawEntry> readBack = MultiCollectionRepacker.ReadRawEntries(path);

            Assert.Equal(entries.Count, readBack.Count);

            foreach (MultiCollectionRepacker.RawEntry expected in entries)
            {
                MultiCollectionRepacker.RawEntry actual = Assert.Single(readBack, e => e.Hash == expected.Hash);
                Assert.Equal(expected.Data, actual.Data);
                Assert.Equal(expected.DecompressedSize, actual.DecompressedSize);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Same bug, but specifically spanning a table boundary (100 entries per
    /// table) - the scenario that actually surfaced it against real data.
    /// </summary>
    [Fact]
    public void WriteRawUop_MultipleTables_LastTableEntriesSurvive()
    {
        const int entryCount = 150; // spans 2 tables (100 + 50) - table 1 is "the last table"

        List<MultiCollectionRepacker.RawEntry> entries = new();
        for (int i = 0; i < entryCount; i++)
            entries.Add(MakeEntry((ulong)(0x2000 + i), i));

        string path = Path.GetTempFileName();
        try
        {
            MultiCollectionRepacker.WriteRawUop(entries, path);
            List<MultiCollectionRepacker.RawEntry> readBack = MultiCollectionRepacker.ReadRawEntries(path);

            Assert.Equal(entryCount, readBack.Count);

            // Specifically check an entry that only exists in the second (last) table -
            // this is exactly what the bug silently dropped.
            MultiCollectionRepacker.RawEntry lastTableEntry = entries[120];
            MultiCollectionRepacker.RawEntry found = Assert.Single(readBack, e => e.Hash == lastTableEntry.Hash);
            Assert.Equal(lastTableEntry.Data, found.Data);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CanRepack_FalseWithoutMultiMulAndIdx()
    {
        string emptyDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(emptyDir);

        try
        {
            Assert.False(MultiCollectionRepacker.CanRepack(emptyDir));
        }
        finally
        {
            Directory.Delete(emptyDir, recursive: true);
        }
    }
}
