using Index5.Domain.Entities;

namespace Index5.Domain.Interfaces;

public interface ICotahistParser
{
    List<CotacaoB3> ParseFile(string filePath);
    CotacaoB3? GetClosingQuote(string quotesFolder, string ticker);
    List<string> GetAllAvailableTickers(string quotesFolder);
}
