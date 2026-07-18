using UoFiddler.Plugin.HousingEditor.Classes;
using UoFiddler.Plugin.HousingEditor.Tests.Fixtures;
using Xunit;

namespace UoFiddler.Plugin.HousingEditor.Tests;

public class HousingBinCodecTests
{
    private static HousingCategory MakeLegacyCategory(string name, params string[] columns)
    {
        HousingCategory category = new() { Name = name };
        category.Columns.AddRange(columns);
        return category;
    }

    [Fact]
    public void TryDecodeCategory_ExactMatch_DecodesAllRecords()
    {
        // Doors (fileType 3): the "unknown" dword goes after fields2, not between groups.
        HousingCategory legacy = MakeLegacyCategory("Doors", "Piece1", "Piece2", "FeatureMask");
        HousingRecord row0 = legacy.AddRecord();
        row0.Set("Piece1", 100);
        row0.Set("Piece2", 200);
        row0.Set("FeatureMask", 0);
        row0.Comment = "Plain Door";

        HousingRecord row1 = legacy.AddRecord();
        row1.Set("Piece1", 300);
        row1.Set("Piece2", 400);
        row1.Set("FeatureMask", 16);
        row1.Comment = "AOS Door";

        var section = new SyntheticHousingBin.FileTypeSection { FileType = 3 };
        section.Entries.Add(new SyntheticHousingBin.Entry
        {
            CategoryId = 0,
            FeatureMask = 0,
            ClilocId = 0,
            Fields1 = { new() { Direction = 0, Value = 100 }, new() { Direction = 1, Value = 200 } }
        });
        section.Entries.Add(new SyntheticHousingBin.Entry
        {
            CategoryId = 1,
            FeatureMask = 16,
            ClilocId = 12345,
            Fields1 = { new() { Direction = 0, Value = 300 }, new() { Direction = 1, Value = 400 } }
        });

        byte[] housingBin = SyntheticHousingBin.Build(section);

        bool ok = HousingBinCodec.TryDecodeCategory(housingBin, legacy, out List<HousingRecord>? decoded);

        Assert.True(ok);
        Assert.Equal(2, decoded!.Count);

        HousingRecord decoded0 = Assert.Single(decoded, r => r.Index == 0);
        Assert.Equal(100, decoded0.Get("Piece1"));
        Assert.Equal(200, decoded0.Get("Piece2"));
        Assert.Equal(0, decoded0.Get("FeatureMask"));
        Assert.Equal("Plain Door", decoded0.Comment);

        HousingRecord decoded1 = Assert.Single(decoded, r => r.Index == 1);
        Assert.Equal(300, decoded1.Get("Piece1"));
        Assert.Equal(400, decoded1.Get("Piece2"));
        Assert.Equal(16, decoded1.Get("FeatureMask"));
        Assert.Equal(12345, decoded1.Get("ClilocId"));
    }

    [Fact]
    public void TryDecodeCategory_Walls_HandlesBetweenGroupsUnknownDword()
    {
        // Walls (fileType 5) has an extra dword BETWEEN fields1 and fields2, not after -
        // the one structural difference the codec has to special-case. A section built with
        // the wrong branch would misalign every subsequent read and fail to decode at all.
        HousingCategory legacy = MakeLegacyCategory("Walls", "South1", "East1", "FeatureMask", "Style");
        HousingRecord row0 = legacy.AddRecord();
        row0.Set("South1", 500);
        row0.Set("East1", 600);
        row0.Set("FeatureMask", 0);
        row0.Set("Style", 0);

        var section = new SyntheticHousingBin.FileTypeSection { FileType = 5 };
        section.Entries.Add(new SyntheticHousingBin.Entry
        {
            CategoryId = 0,
            SubcategoryId = 0,
            FeatureMask = 0,
            Fields1 = { new() { Direction = 0, Value = 500 } },
            Fields2 = { new() { Direction = 0, Value = 600 } }
        });

        byte[] housingBin = SyntheticHousingBin.Build(section);

        bool ok = HousingBinCodec.TryDecodeCategory(housingBin, legacy, out List<HousingRecord>? decoded);

        Assert.True(ok);
        HousingRecord decoded0 = Assert.Single(decoded!);
        Assert.Equal(500, decoded0.Get("South1"));
        Assert.Equal(600, decoded0.Get("East1"));
    }

    [Fact]
    public void TryDecodeCategory_CombinedEntry_SplitsTwoDesignsFromOneEntry()
    {
        // Confirmed real-world pattern (Walls' "Gothic Walls 2" + "Rose Window 1"): one binary
        // entry's two field groups pack two TXT-distinct rows' pieces together.
        HousingCategory legacy = MakeLegacyCategory("Walls", "South1", "East1");
        HousingRecord rowA = legacy.AddRecord();
        rowA.Set("South1", 100);
        rowA.Comment = "Design A";

        HousingRecord rowB = legacy.AddRecord();
        rowB.Set("East1", 200);
        rowB.Comment = "Design B";

        var section = new SyntheticHousingBin.FileTypeSection { FileType = 5 };
        section.Entries.Add(new SyntheticHousingBin.Entry
        {
            Fields1 = { new() { Direction = 0, Value = 100 } },
            Fields2 = { new() { Direction = 0, Value = 200 } }
        });

        byte[] housingBin = SyntheticHousingBin.Build(section);

        bool ok = HousingBinCodec.TryDecodeCategory(housingBin, legacy, out List<HousingRecord>? decoded);

        Assert.True(ok);
        Assert.Equal(2, decoded!.Count);
        Assert.Contains(decoded, r => r.Comment == "Design A" && r.Get("South1") == 100);
        Assert.Contains(decoded, r => r.Comment == "Design B" && r.Get("East1") == 200);
    }

    [Fact]
    public void TryDecodeCategory_SubsetMatch_EntryWithExtraSharedValue()
    {
        // Confirmed real-world pattern (Walls' Celtic-style continuation entries): an entry
        // carries one extra slot value beyond what the TXT row lists as its own columns
        // (a shared/boundary piece reused across direction slots).
        HousingCategory legacy = MakeLegacyCategory("Walls", "South1", "East1");
        HousingRecord row = legacy.AddRecord();
        row.Set("South1", 700);
        row.Set("East1", 800);

        var section = new SyntheticHousingBin.FileTypeSection { FileType = 5 };
        section.Entries.Add(new SyntheticHousingBin.Entry
        {
            Fields1 = { new() { Direction = 0, Value = 700 }, new() { Direction = 1, Value = 900 } }, // 900 is extra
            Fields2 = { new() { Direction = 0, Value = 800 } }
        });

        byte[] housingBin = SyntheticHousingBin.Build(section);

        bool ok = HousingBinCodec.TryDecodeCategory(housingBin, legacy, out List<HousingRecord>? decoded);

        Assert.True(ok);
        HousingRecord decoded0 = Assert.Single(decoded!);
        Assert.Equal(700, decoded0.Get("South1"));
        Assert.Equal(800, decoded0.Get("East1"));
    }

    [Fact]
    public void TryDecodeCategory_RowWithNoMatchingEntry_IsOmittedNotGuessed()
    {
        HousingCategory legacy = MakeLegacyCategory("Doors", "Piece1");
        HousingRecord row0 = legacy.AddRecord();
        row0.Set("Piece1", 111);
        HousingRecord row1 = legacy.AddRecord();
        row1.Set("Piece1", 222); // no corresponding binary entry below

        var section = new SyntheticHousingBin.FileTypeSection { FileType = 3 };
        section.Entries.Add(new SyntheticHousingBin.Entry
        {
            Fields1 = { new() { Direction = 0, Value = 111 } }
        });

        byte[] housingBin = SyntheticHousingBin.Build(section);

        bool ok = HousingBinCodec.TryDecodeCategory(housingBin, legacy, out List<HousingRecord>? decoded);

        Assert.True(ok);
        HousingRecord onlyDecoded = Assert.Single(decoded!);
        Assert.Equal(0, onlyDecoded.Index); // row1's index (1) never appears
    }

    [Fact]
    public void TryDecodeCategory_UnknownCategoryName_ReturnsFalse()
    {
        HousingCategory legacy = MakeLegacyCategory("NotARealCategory", "Piece1");
        legacy.AddRecord().Set("Piece1", 1);

        byte[] housingBin = SyntheticHousingBin.Build(new SyntheticHousingBin.FileTypeSection { FileType = 3 });

        bool ok = HousingBinCodec.TryDecodeCategory(housingBin, legacy, out List<HousingRecord>? decoded);

        Assert.False(ok);
        Assert.Null(decoded);
    }
}
