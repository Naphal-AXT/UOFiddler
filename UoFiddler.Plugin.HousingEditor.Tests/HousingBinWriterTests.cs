using UoFiddler.Plugin.HousingEditor.Classes;
using Xunit;

namespace UoFiddler.Plugin.HousingEditor.Tests;

public class HousingBinWriterTests
{
    [Fact]
    public void Write_ThenDecode_RoundTripsAllFieldValues()
    {
        // A project with two housing.bin-sourced categories, as HousingEditorControl builds
        // after Correlate... - Write() should reproduce bytes that decode back to the same values.
        HousingProject project = new() { ClientType = ClientType.Uop };

        HousingCategory doors = project.AddCategory("Doors", "housing.bin");
        doors.Columns.AddRange(new[] { "Piece1", "Piece2", "FeatureMask" });
        HousingRecord doorRecord = new() { Index = 0, Comment = "Test Door" };
        doorRecord.Set("Piece1", 111);
        doorRecord.Set("Piece2", 222);
        doorRecord.Set("FeatureMask", 64);
        doors.Add(doorRecord);

        HousingCategory walls = project.AddCategory("Walls", "housing.bin");
        walls.Columns.AddRange(new[] { "South1", "East1", "Style" });
        HousingRecord wallRecord = new() { Index = 0, Comment = "Test Wall" };
        wallRecord.Set("South1", 333);
        wallRecord.Set("East1", 444);
        wallRecord.Set("Style", 2);
        walls.Add(wallRecord);

        byte[] written = HousingBinWriter.Write(project);

        bool doorsOk = HousingBinCodec.TryDecodeCategory(written, doors, out List<HousingRecord>? redecodedDoors);
        bool wallsOk = HousingBinCodec.TryDecodeCategory(written, walls, out List<HousingRecord>? redecodedWalls);

        Assert.True(doorsOk);
        Assert.True(wallsOk);

        HousingRecord doorResult = Assert.Single(redecodedDoors!);
        Assert.Equal(111, doorResult.Get("Piece1"));
        Assert.Equal(222, doorResult.Get("Piece2"));
        Assert.Equal(64, doorResult.Get("FeatureMask"));

        HousingRecord wallResult = Assert.Single(redecodedWalls!);
        Assert.Equal(333, wallResult.Get("South1"));
        Assert.Equal(444, wallResult.Get("East1"));
    }

    [Fact]
    public void Write_SkipsCategoriesWithUnrecognizedNames()
    {
        HousingProject project = new() { ClientType = ClientType.Uop };
        HousingCategory notReal = project.AddCategory("NotARealCategory", "housing.bin");
        notReal.Columns.Add("Piece1");
        HousingRecord record = new() { Index = 0 };
        record.Set("Piece1", 1);
        notReal.Add(record);

        // Should not throw, and the resulting bytes should describe zero fileType sections.
        byte[] written = HousingBinWriter.Write(project);

        uint fileTypeCount = BitConverter.ToUInt32(written, 0);
        Assert.Equal(0u, fileTypeCount);
    }
}
