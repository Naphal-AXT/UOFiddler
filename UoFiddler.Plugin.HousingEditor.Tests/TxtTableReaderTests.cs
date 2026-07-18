using System.IO;
using UoFiddler.Plugin.HousingEditor;
using Xunit;

namespace UoFiddler.Plugin.HousingEditor.Tests;

public class TxtTableReaderTests
{
    private static string WriteTempFile(string content)
    {
        string path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Read_ParsesTypeLineHeaderAndRows()
    {
        // Matches the real doors.txt/walls.txt shape: type line, header line, tab-separated rows.
        string content =
            "int\tint\tint\tstring\r\n" +
            "Piece1\tPiece2\tFeatureMask\tComment\r\n" +
            "1657\t1659\t0\tMetal Door\r\n" +
            "8177\t8179\t16\tMetal Gate\r\n";

        string path = WriteTempFile(content);
        try
        {
            TxtTable table = TxtTableReader.Read(path);

            Assert.Equal("int\tint\tint\tstring", table.TypeLine);
            Assert.Equal(new[] { "Piece1", "Piece2", "FeatureMask", "Comment" }, table.Columns);
            Assert.Equal(2, table.Rows.Count);

            TxtRow row0 = table.Rows[0];
            Assert.Equal(1657, row0.Get("Piece1"));
            Assert.Equal(1659, row0.Get("Piece2"));
            Assert.Equal(0, row0.Get("FeatureMask"));
            Assert.Equal("Metal Door", row0.Comment);

            TxtRow row1 = table.Rows[1];
            Assert.Equal(8177, row1.Get("Piece1"));
            Assert.Equal("Metal Gate", row1.Comment);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_ParsesHexValuesWithPrefix()
    {
        string content = "int\r\nFlags\r\n0x2A\r\n";

        string path = WriteTempFile(content);
        try
        {
            TxtTable table = TxtTableReader.Read(path);

            Assert.Equal(0x2A, table.Rows[0].Get("Flags"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_SkipsBlankLinesBetweenTypeLineAndHeader()
    {
        string content = "int\r\n\r\n\r\nPiece1\r\n42\r\n";

        string path = WriteTempFile(content);
        try
        {
            TxtTable table = TxtTableReader.Read(path);

            Assert.Equal(new[] { "Piece1" }, table.Columns);
            Assert.Equal(42, table.Rows[0].Get("Piece1"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_SkipsBlankDataLines()
    {
        string content = "int\r\nPiece1\r\n1\r\n\r\n2\r\n";

        string path = WriteTempFile(content);
        try
        {
            TxtTable table = TxtTableReader.Read(path);

            Assert.Equal(2, table.Rows.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_MissingFile_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() => TxtTableReader.Read(@"C:\definitely\not\a\real\path.txt"));
    }

    [Fact]
    public void Read_EmptyPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TxtTableReader.Read(""));
    }
}
