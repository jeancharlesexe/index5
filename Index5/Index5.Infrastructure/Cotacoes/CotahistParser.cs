using Index5.Domain.Entities;
using Index5.Domain.Interfaces;

namespace Index5.Infrastructure.Cotacoes;

public class CotahistParser : ICotahistParser
{
    public List<CotacaoB3> ParseFile(string filePath)
    {
        var quotes = new List<CotacaoB3>();
        foreach (var line in File.ReadLines(filePath))
        {
            var quote = ParseLine(line);
            if (quote != null) quotes.Add(quote);
        }
        return quotes;
    }

    public CotacaoB3? GetClosingQuote(string quotesFolder, string ticker)
    {
        if (!Directory.Exists(quotesFolder)) return null;

        var files = Directory.GetFiles(quotesFolder, "COTAHIST_D*.TXT")
            .OrderByDescending(f => f)
            .ToList();

        CotacaoB3? latestQuote = null;

        foreach (var file in files)
        {
            foreach (var line in File.ReadLines(file))
            {
                if (line.Length < 245) continue;
                
                var lineTicker = line.Substring(12, 12).Trim();
                if (!lineTicker.Equals(ticker, StringComparison.OrdinalIgnoreCase)) continue;

                var quote = ParseLine(line);
                if (quote != null && (latestQuote == null || quote.DataPregao > latestQuote.DataPregao))
                {
                    latestQuote = quote;
                }
            }

            if (latestQuote != null) return latestQuote;
        }

        return null;
    }

    private CotacaoB3? ParseLine(string line)
    {
        if (line.Length < 245) return null;

        var recordType = line.Substring(0, 2);
        if (recordType != "01") return null;

        var marketType = int.Parse(line.Substring(24, 3).Trim());
        // 10 = Vista, 20 = Fracionário
        if (marketType != 10 && marketType != 20) return null;

        return new CotacaoB3
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
        };
    }

    private static decimal ParsePrice(string rawValue)
    {
        if (long.TryParse(rawValue.Trim(), out var value))
            return value / 100m;
        return 0m;
    }
}
