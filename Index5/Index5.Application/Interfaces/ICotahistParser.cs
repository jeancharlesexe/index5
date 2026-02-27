using Index5.Domain.Entities;

namespace Index5.Application.Interfaces;

public interface ICotahistParser
{
    List<CotacaoB3> ParseFile(string filePath);
    CotacaoB3? GetClosingQuote(string quotesFolder, string ticker);
}
