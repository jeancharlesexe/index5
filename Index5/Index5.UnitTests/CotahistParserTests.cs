using Index5.Domain.Entities;
using Index5.Infrastructure.Cotacoes;

namespace Index5.UnitTests;

public class CotahistParserTests
{
    private readonly CotahistParser _parser;
    private readonly string _testDataDir;

    public CotahistParserTests()
    {
        _parser = new CotahistParser();
        _testDataDir = Path.Combine(Path.GetTempPath(), "index5_test_cotacoes");
        Directory.CreateDirectory(_testDataDir);
    }

    private void CriarArquivoCotahist(string filename, List<string> lines)
    {
        File.WriteAllLines(Path.Combine(_testDataDir, filename), lines);
    }

    private string CriarLinhaCotahist(string ticker, int tipoMercado, decimal preco, string data = "20260226")
    {
        // Layout COTAHIST: posição fixa
        var line = new char[245];
        Array.Fill(line, ' ');

        // RecordType (0-1) = "01"
        "01".CopyTo(0, line, 0, 2);

        // DataPregao (2-9)
        data.CopyTo(0, line, 2, 8);

        // CodigoBDI (10-11)
        "02".CopyTo(0, line, 10, 2);

        // Ticker (12-23)
        var tickerPadded = ticker.PadRight(12);
        tickerPadded.CopyTo(0, line, 12, 12);

        // TipoMercado (24-26)
        tipoMercado.ToString("D3").CopyTo(0, line, 24, 3);

        // NomeEmpresa (27-38)
        "EMPRESA     ".CopyTo(0, line, 27, 12);

        // Precos: multiplicar por 100 e preencher com 13 dígitos
        var precoStr = ((long)(preco * 100)).ToString("D13");
        precoStr.CopyTo(0, line, 56, 13);  // Abertura
        precoStr.CopyTo(0, line, 69, 13);  // Máximo
        precoStr.CopyTo(0, line, 82, 13);  // Mínimo
        precoStr.CopyTo(0, line, 95, 13);  // Médio
        precoStr.CopyTo(0, line, 108, 13); // Fechamento

        // Quantidade Negociada (152-169)
        "000000000000001000".CopyTo(0, line, 152, 18);

        // Volume Negociado (170-187)
        "000000000000350000".CopyTo(0, line, 170, 18);

        return new string(line);
    }

    [Fact]
    public void ParseFile_DeveParserLinhasValidas()
    {
        var lines = new List<string>
        {
            "00COTAHIST.2026 HEADER",
            CriarLinhaCotahist("PETR4", 10, 39.61m),
            CriarLinhaCotahist("VALE3", 10, 89.21m),
            "99COTAHIST.2026 TRAILER"
        };

        CriarArquivoCotahist("COTAHIST_DTEST01.TXT", lines);

        var result = _parser.ParseFile(Path.Combine(_testDataDir, "COTAHIST_DTEST01.TXT"));

        Assert.Equal(2, result.Count);
        Assert.Equal("PETR4", result[0].Ticker);
        Assert.Equal(39.61m, result[0].PrecoFechamento);
        Assert.Equal("VALE3", result[1].Ticker);
        Assert.Equal(89.21m, result[1].PrecoFechamento);
    }

    [Fact]
    public void ParseFile_DeveIgnorarHeader()
    {
        var lines = new List<string>
        {
            "00COTAHIST.2026 HEADER",
            CriarLinhaCotahist("PETR4", 10, 35m),
            "99COTAHIST.2026 TRAILER"
        };

        CriarArquivoCotahist("COTAHIST_DTEST02.TXT", lines);

        var result = _parser.ParseFile(Path.Combine(_testDataDir, "COTAHIST_DTEST02.TXT"));

        Assert.Single(result);
    }

    [Fact]
    public void ParseFile_DeveFiltrarMercadoAVista()
    {
        var lines = new List<string>
        {
            CriarLinhaCotahist("PETR4", 10, 35m),   // Vista
            CriarLinhaCotahist("PETR4F", 20, 34.5m), // Fracionário
            CriarLinhaCotahist("PETR4T", 30, 35.5m), // Termo (deve ignorar)
        };

        CriarArquivoCotahist("COTAHIST_DTEST03.TXT", lines);

        var result = _parser.ParseFile(Path.Combine(_testDataDir, "COTAHIST_DTEST03.TXT"));

        Assert.Equal(2, result.Count); // Apenas Vista e Fracionário
        Assert.DoesNotContain(result, r => r.Ticker == "PETR4T");
    }

    [Fact]
    public void GetClosingQuote_DeveRetornarCotacao_QuandoTickerExiste()
    {
        var subDir = Path.Combine(_testDataDir, "test_closing_quote");
        Directory.CreateDirectory(subDir);

        var lines = new List<string>
        {
            CriarLinhaCotahist("PETR4", 10, 39.61m),
            CriarLinhaCotahist("VALE3", 10, 89.21m)
        };

        File.WriteAllLines(Path.Combine(subDir, "COTAHIST_D26022026.TXT"), lines);

        var result = _parser.GetClosingQuote(subDir, "PETR4");

        Assert.NotNull(result);
        Assert.Equal("PETR4", result!.Ticker);
        Assert.Equal(39.61m, result.PrecoFechamento);
    }

    [Fact]
    public void GetClosingQuote_DeveRetornarNull_QuandoTickerNaoExiste()
    {
        var lines = new List<string>
        {
            CriarLinhaCotahist("PETR4", 10, 39.61m)
        };

        CriarArquivoCotahist("COTAHIST_D26022026B.TXT", lines);

        var result = _parser.GetClosingQuote(_testDataDir, "XPTO11");

        Assert.Null(result);
    }

    [Fact]
    public void GetClosingQuote_DeveRetornarNull_QuandoPastaInexistente()
    {
        var result = _parser.GetClosingQuote("/pasta/inexistente/xyz", "PETR4");
        Assert.Null(result);
    }

    [Fact]
    public void GetClosingQuote_DeveRetornarPrecoCorreto()
    {
        var lines = new List<string>
        {
            CriarLinhaCotahist("ITUB4", 10, 47.67m),
            CriarLinhaCotahist("BBDC4", 10, 20.98m),
            CriarLinhaCotahist("WEGE3", 10, 49.38m)
        };

        CriarArquivoCotahist("COTAHIST_D27022026.TXT", lines);

        Assert.Equal(47.67m, _parser.GetClosingQuote(_testDataDir, "ITUB4")?.PrecoFechamento);
        Assert.Equal(20.98m, _parser.GetClosingQuote(_testDataDir, "BBDC4")?.PrecoFechamento);
        Assert.Equal(49.38m, _parser.GetClosingQuote(_testDataDir, "WEGE3")?.PrecoFechamento);
    }
}
