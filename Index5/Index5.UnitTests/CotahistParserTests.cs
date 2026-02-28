using FluentAssertions;
using Index5.Infrastructure.Cotacoes;
using System.IO;

namespace Index5.UnitTests;

public class CotahistParserTests
{
    private readonly CotahistParser _parser;
    private readonly string _testFolder;

    public CotahistParserTests()
    {
        _parser = new CotahistParser();
        _testFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testFolder);
    }

    private string CreateFakeB3File(string name, string ticker, decimal price, string date = "20260225")
    {
        var filePath = Path.Combine(_testFolder, name);
        var priceStr = ((long)(price * 100)).ToString().PadLeft(13, '0');
        
        // Build line char by char to ensure offsets
        char[] lineChars = new string(' ', 245).ToCharArray();
        
        "01".CopyTo(0, lineChars, 0, 2); // Type
        date.CopyTo(0, lineChars, 2, 8); // Date
        "02".CopyTo(0, lineChars, 10, 2); // BDI
        ticker.PadRight(12).CopyTo(0, lineChars, 12, 12); // Ticker
        "010".CopyTo(0, lineChars, 24, 3); // Market
        "NAME".PadRight(12).CopyTo(0, lineChars, 27, 12);
        
        priceStr.CopyTo(0, lineChars, 56, 13); // Abertura
        priceStr.CopyTo(0, lineChars, 69, 13); // Max
        priceStr.CopyTo(0, lineChars, 82, 13); // Min
        priceStr.CopyTo(0, lineChars, 95, 13); // Med
        priceStr.CopyTo(0, lineChars, 108, 13); // Fechamento
        
        "000000000000001000".CopyTo(0, lineChars, 152, 18); // Qty
        "000000000000001000".CopyTo(0, lineChars, 170, 18); // Vol

        var lines = new List<string> { 
            "00COTAHIST.2026".PadRight(245),
            new string(lineChars),
            "99".PadRight(245)
        };
        
        File.WriteAllLines(filePath, lines);
        return filePath;
    }

    [Fact]
    public void ParseFile_ValidLine_DecodesCorrectly()
    {
        var path = CreateFakeB3File("COTAHIST_D25022026.TXT", "PETR4", 39.50m);
        var result = _parser.ParseFile(path);

        result.Should().HaveCount(1);
        result[0].Ticker.Should().Be("PETR4");
        result[0].PrecoFechamento.Should().Be(39.50m);
    }

    [Fact]
    public void GetClosingQuote_MultipleFiles_ReturnsLatest()
    {
        CreateFakeB3File("COTAHIST_D24022026.TXT", "VALE3", 20.00m, "20260224");
        CreateFakeB3File("COTAHIST_D25022026.TXT", "VALE3", 25.00m, "20260225");

        var result = _parser.GetClosingQuote(_testFolder, "VALE3");

        result.Should().NotBeNull();
        result!.PrecoFechamento.Should().Be(25.00m);
    }

    [Fact]
    public void GetAllAvailableTickers_ReturnsSortedUniqueList()
    {
        CreateFakeB3File("COTAHIST_D26022026.txt", "BBAS3", 10m);
        var result = _parser.GetAllAvailableTickers(_testFolder);
        result.Should().Contain("BBAS3");
    }

    [Fact]
    public void GetClosingQuote_FolderNotFound_ReturnsNull()
    {
        var result = _parser.GetClosingQuote("invalid_path", "PETR4");
        result.Should().BeNull();
    }
}
