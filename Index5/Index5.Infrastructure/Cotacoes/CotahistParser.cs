using Index5.Application.Interfaces;
using Index5.Domain.Entities;

namespace Index5.Infrastructure.Cotacoes;

public class CotahistParser : ICotahistParser
{
    public List<CotacaoB3> ParseFile(string filePath)
    {
        var quotes = new List<CotacaoB3>();

        foreach (var line in File.ReadLines(filePath))
        {
            if (line.Length < 245)
                continue;

            var recordType = line.Substring(0, 2);
            if (recordType != "01")
                continue;

            var marketType = int.Parse(line.Substring(24, 3).Trim());
            if (marketType != 10 && marketType != 20)
                continue;

            quotes.Add(new CotacaoB3
            {
                DataPregao = DateTime.ParseExact(
                    line.Substring(2, 8), "yyyyMMdd",
                    System.Globalization.CultureInfo.InvariantCulture),
                CodigoBDI = line.Substring(10, 2).Trim(),
                Ticker = line.Substring(12, 12).Trim(),
                TipoMercado = marketType,
                NomeEmpresa = line.Substring(27, 12).Trim(),
                PrecoAbertura = ParsePrice(line.Substring(56, 13)),
                PrecoMaximo = ParsePrice(line.Substring(69, 13)),
                PrecoMinimo = ParsePrice(line.Substring(82, 13)),
                PrecoMedio = ParsePrice(line.Substring(95, 13)),
                PrecoFechamento = ParsePrice(line.Substring(108, 13)),
                QuantidadeNegociada = long.Parse(line.Substring(152, 18).Trim()),
                VolumeNegociado = ParsePrice(line.Substring(170, 18))
            });
        }

        return quotes;
    }

    public CotacaoB3? GetClosingQuote(string quotesFolder, string ticker)
    {
        var files = Directory.GetFiles(quotesFolder, "COTAHIST_D*.TXT")
            .OrderByDescending(f => f)
            .ToList();

        foreach (var file in files)
        {
            var quotes = ParseFile(file);
            var quote = quotes
                .Where(q => q.Ticker.Equals(ticker, StringComparison.OrdinalIgnoreCase))
                .Where(q => q.TipoMercado == 10)
                .FirstOrDefault();

            if (quote != null)
                return quote;
        }

        return null;
    }

    private static decimal ParsePrice(string rawValue)
    {
        if (long.TryParse(rawValue.Trim(), out var value))
            return value / 100m;
        return 0m;
    }
}
