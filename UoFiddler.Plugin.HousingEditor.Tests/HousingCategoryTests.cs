using Xunit;

namespace UoFiddler.Plugin.HousingEditor.Tests;

public class HousingCategoryTests
{
    /// <summary>
    /// Regression test for a real bug: Add() used to reassign every record's
    /// Index to its sequential position in the list, silently overwriting the
    /// meaningful (deliberately sparse) index HousingBinCodec assigns - the
    /// legacy TXT row number a binary entry was matched to. A category with
    /// a gap (a TXT row housing.bin has no entry for) had every record added
    /// after that gap mislabeled. See UoFiddler.Plugin.HousingEditor/README.md
    /// ("Writing housing.bin") for the full story.
    /// </summary>
    [Fact]
    public void Add_PreservesRecordIndex_EvenWithGaps()
    {
        HousingCategory category = new() { Name = "Walls" };

        HousingRecord recordA = new() { Index = 5, Comment = "A" };
        HousingRecord recordB = new() { Index = 7, Comment = "B" }; // gap at 6

        category.Add(recordA);
        category.Add(recordB);

        Assert.Equal(5, recordA.Index);
        Assert.Equal(7, recordB.Index);
    }

    [Fact]
    public void Add_SetsCategoryNameAndSourceFile()
    {
        HousingCategory category = new() { Name = "Walls", SourceFile = "housing.bin" };
        HousingRecord record = new() { Index = 0 };

        category.Add(record);

        Assert.Equal("Walls", record.CategoryName);
        Assert.Equal("housing.bin", record.SourceFile);
    }

    [Fact]
    public void AddRecord_AssignsSequentialIndex()
    {
        // AddRecord() (used by LegacyReader, always TXT-sourced -> never sparse)
        // is a different path from Add() and is expected to number sequentially.
        HousingCategory category = new() { Name = "Doors" };

        HousingRecord first = category.AddRecord();
        HousingRecord second = category.AddRecord();

        Assert.Equal(0, first.Index);
        Assert.Equal(1, second.Index);
    }
}
