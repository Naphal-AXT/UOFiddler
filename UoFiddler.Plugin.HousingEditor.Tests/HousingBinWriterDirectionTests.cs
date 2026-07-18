using UoFiddler.Plugin.HousingEditor.Classes;
using Xunit;

namespace UoFiddler.Plugin.HousingEditor.Tests;

/// <summary>
/// Regression coverage for the real "direction" convention confirmed
/// 2026-07-18 against a real Classic client's housing.bin (see
/// HousingBinWriter's class remarks and docs/FILEFORMATS.md in the
/// sibling ThePiperBox repo for the evidence): direction is the fixed
/// 1-based position of a column within its own group's on-disk order,
/// not compacted when an earlier column in that order is zero. These
/// tests parse the raw written bytes directly (HousingBinCodec's public
/// decode API doesn't expose direction, since it isn't needed to
/// round-trip piece values) so a regression here would be silent
/// through every other test in this project.
/// </summary>
public class HousingBinWriterDirectionTests
{
    private const int WallsFileType = 5;

    [Fact]
    public void Doors_DirectionIsOneBasedTxtColumnIndex_NotCompacted()
    {
        HousingProject project = new() { ClientType = ClientType.Uop };
        HousingCategory doors = project.AddCategory("Doors", "housing.bin");
        doors.Columns.AddRange(new[] { "Piece1", "Piece2", "Piece3" });

        HousingRecord record = new() { Index = 0 };
        record.Set("Piece1", 10);
        // Piece2 left at 0 - deliberately a gap.
        record.Set("Piece3", 30);
        doors.Add(record);

        byte[] written = HousingBinWriter.Write(project);
        List<(int direction, int value)> group1 = ParseFirstEntryGroup1(written, fileType: 3);

        // Piece3 is TXT column index 2 (0-based) -> direction 3, NOT compacted to 2
        // just because Piece2 (index 1) was zero and skipped.
        Assert.Equal(new[] { (1, 10), (3, 30) }, group1);
    }

    [Fact]
    public void Stairs_Group1PutsBlockLast_Group2IsMultiColumnsInTxtOrder()
    {
        HousingProject project = new() { ClientType = ClientType.Uop };
        HousingCategory stairs = project.AddCategory("Stairs", "housing.bin");
        // Real TXT declaration order for stairs.txt.
        stairs.Columns.AddRange(new[]
        {
            "Block", "North", "East", "South", "West", "Squared1", "Squared2",
            "Rounded1", "Rounded2", "MultiNorth", "MultiEast", "MultiSouth", "MultiWest"
        });

        HousingRecord record = new() { Index = 0 };
        record.Set("Block", 100);
        record.Set("North", 101);
        record.Set("MultiSouth", 102);
        stairs.Add(record);

        byte[] written = HousingBinWriter.Write(project);
        (List<(int direction, int value)> group1, List<(int direction, int value)> group2) =
            ParseFirstEntryGroups(written, fileType: 1);

        // North is fields1 slot #1 (of 9); Block is fields1's LAST slot (#9), even though
        // Block is stairs.txt's first column - confirmed real-client quirk, not TXT order.
        Assert.Equal(new[] { (1, 101), (9, 100) }, group1);

        // MultiSouth is fields2 slot #3 of 4 (MultiNorth, MultiEast, MultiSouth, MultiWest).
        Assert.Equal(new[] { (3, 102) }, group2);
    }

    [Fact]
    public void Walls_SplitsAtEighthColumn_StructureInGroup1_WindowsInGroup2()
    {
        HousingProject project = new() { ClientType = ClientType.Uop };
        HousingCategory walls = project.AddCategory("Walls", "housing.bin");
        // Real TXT declaration order for walls.txt.
        walls.Columns.AddRange(new[]
        {
            "South1", "South2", "South3", "Corner", "East1", "East2", "East3", "Post",
            "WindowS", "AltWindowS", "WindowE", "AltWindowE", "SecondAltWindowS", "SecondAltWindowE"
        });

        HousingRecord record = new() { Index = 0 };
        record.Set("Post", 200); // last of the 8 group1 (structure) columns
        record.Set("AltWindowS", 201); // 2nd of the 6 group2 (window) columns
        walls.Add(record);

        byte[] written = HousingBinWriter.Write(project);
        (List<(int direction, int value)> group1, List<(int direction, int value)> group2) =
            ParseFirstEntryGroups(written, WallsFileType);

        Assert.Equal(new[] { (8, 200) }, group1);
        Assert.Equal(new[] { (2, 201) }, group2);
    }

    private static List<(int direction, int value)> ParseFirstEntryGroup1(byte[] housing, int fileType) =>
        ParseFirstEntryGroups(housing, fileType).group1;

    private static (List<(int direction, int value)> group1, List<(int direction, int value)> group2)
        ParseFirstEntryGroups(byte[] housing, int fileType)
    {
        int cursor = 0;
        uint fileTypeCount = ReadU32(housing, ref cursor);

        for (int f = 0; f < fileTypeCount; f++)
        {
            uint thisFileType = ReadU32(housing, ref cursor);
            uint entryCount = ReadU32(housing, ref cursor);
            bool isWalls = thisFileType == WallsFileType;

            for (int e = 0; e < entryCount; e++)
            {
                _ = ReadU32(housing, ref cursor); // categoryId
                _ = ReadU32(housing, ref cursor); // subcategoryId
                _ = ReadU32(housing, ref cursor); // featureMask
                _ = ReadU32(housing, ref cursor); // clilocId

                List<(int, int)> group1 = ReadGroup(housing, ref cursor);
                if (isWalls) _ = ReadU32(housing, ref cursor);
                List<(int, int)> group2 = ReadGroup(housing, ref cursor);
                if (!isWalls) _ = ReadU32(housing, ref cursor);

                if (thisFileType == fileType && e == 0)
                    return (group1, group2);
            }
        }

        throw new InvalidOperationException($"fileType {fileType} not found or has no entries");
    }

    private static List<(int direction, int value)> ReadGroup(byte[] housing, ref int cursor)
    {
        uint count = ReadU32(housing, ref cursor);
        List<(int, int)> values = new();

        for (int i = 0; i < count; i++)
        {
            int direction = (int)ReadU32(housing, ref cursor);
            int value = (int)ReadU32(housing, ref cursor);
            values.Add((direction, value));
        }

        return values;
    }

    private static uint ReadU32(byte[] housing, ref int cursor)
    {
        uint value = BitConverter.ToUInt32(housing, cursor);
        cursor += 4;
        return value;
    }
}
