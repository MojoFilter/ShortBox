using ShortBox.Services;

namespace ShortBox.Test;

[TestClass]
public class FileNameParserTest
{
    [DataTestMethod]
    [DataRow("Captain America and the Winter Soldier Special 001 (2023) (Digital) (Zone-Empire).cbr", "Captain America and the Winter Soldier Special", "001")]
    [DataRow("Captain Carter 003 (2022) (Digital) (Zone-Empire).cbr", "Captain Carter", "003")]
    [DataRow("Daredevil & Echo 001 (2023) (Digital) (Zone-Empire).cbr", "Daredevil & Echo", "001")]
    [DataRow("Darkhawk 004 (2022) (Digital) (Zone-Empire).cbr", "Darkhawk", "004")]
    public void FileNameParses(string fileName, string expectedSeries, string expectedNumber)
    {
        ComicFileNameParser parser = new();
        var result = parser.Parse(fileName);
        Assert.AreEqual(expectedSeries, result.Series);
        Assert.AreEqual(expectedNumber, result.Number);
    }
}