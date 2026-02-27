using Index5.Domain.Entities;
using Index5.Domain.Interfaces;

namespace Index5.Application.Services;

public class RebalanceamentoService
{
    private readonly IClienteRepository _clienteRepo;
    private readonly ICustodiaRepository _custodiaRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IKafkaProducer _kafkaProducer;

    public RebalanceamentoService(
        IClienteRepository clienteRepo,
        ICustodiaRepository custodiaRepo,
        IUnitOfWork unitOfWork,
        IKafkaProducer kafkaProducer)
    {
        _clienteRepo = clienteRepo;
        _custodiaRepo = custodiaRepo;
        _unitOfWork = unitOfWork;
        _kafkaProducer = kafkaProducer;
    }

    public async Task<RebalanceamentoSummary> RebalancearTodosClientesAsync(
        CestaRecomendacao novaCesta, 
        CestaRecomendacao? cestaAntiga,
        Func<string, decimal> getCotacao)
    {
        var clientes = await _clienteRepo.GetAllActivesAsync();
        int clientesProcessados = 0;

        foreach (var cliente in clientes)
        {
            await RebalancearClienteAsync(cliente, novaCesta, getCotacao);
            clientesProcessados++;
        }

        await _unitOfWork.SaveChangesAsync();

        var ativosRemovidos = cestaAntiga?.Itens.Select(i => i.Ticker)
            .Except(novaCesta.Itens.Select(i => i.Ticker)).ToList() ?? new List<string>();
            
        var ativosAdicionados = novaCesta.Itens.Select(i => i.Ticker)
            .Except(cestaAntiga?.Itens.Select(i => i.Ticker) ?? new List<string>()).ToList();

        return new RebalanceamentoSummary
        {
            ClientesAfetados = clientesProcessados,
            AtivosRemovidos = ativosRemovidos,
            AtivosAdicionados = ativosAdicionados
        };
    }

    private async Task RebalancearClienteAsync(Cliente cliente, CestaRecomendacao novaCesta, Func<string, decimal> getCotacao)
    {
        if (cliente.ContaGrafica == null) return;

        var custodiaAtual = await _custodiaRepo.GetByContaGraficaIdAsync(cliente.ContaGrafica.Id);
        
        // Calcular Valor Total Atual da Carteira
        decimal valorTotalCarteira = 0;
        var precosAtuais = new Dictionary<string, decimal>();

        foreach (var item in custodiaAtual)
        {
            var preco = getCotacao(item.Ticker);
            if (preco <= 0) preco = item.PrecoMedio; // Fallback
            precosAtuais[item.Ticker] = preco;
            valorTotalCarteira += item.Quantidade * preco;
        }

        // Se o cliente nao tem nada, nao ha o que rebalancear (ele vai entrar no proximo aporte mensal)
        if (valorTotalCarteira <= 0) return;

        // Lista de tickers envolvidos (atuais + novos)
        var todosTickers = custodiaAtual.Select(c => c.Ticker)
            .Union(novaCesta.Itens.Select(i => i.Ticker))
            .Distinct();

        decimal totalVendasNoMes = 0;
        var detalhesVenda = new List<dynamic>();

        foreach (var ticker in todosTickers)
        {
            var itemCesta = novaCesta.Itens.FirstOrDefault(i => i.Ticker == ticker);
            var itemCustodia = custodiaAtual.FirstOrDefault(c => c.Ticker == ticker);

            decimal percentualAlvo = itemCesta?.Percentual ?? 0;
            decimal precoAtual = precosAtuais.ContainsKey(ticker) ? precosAtuais[ticker] : getCotacao(ticker);
            
            if (precoAtual <= 0) continue;

            int qtdAtual = itemCustodia?.Quantidade ?? 0;
            decimal valorAlvo = valorTotalCarteira * (percentualAlvo / 100m);
            int qtdAlvo = (int)Math.Truncate(valorAlvo / precoAtual);

            int diff = qtdAlvo - qtdAtual;

            if (diff < 0) // Vender
            {
                int qtdVenda = Math.Abs(diff);
                totalVendasNoMes += qtdVenda * precoAtual;
                
                if (itemCustodia != null)
                {
                    detalhesVenda.Add(new { 
                        ticker = ticker, 
                        quantidade = qtdVenda, 
                        precoVenda = precoAtual, 
                        precoMedio = itemCustodia.PrecoMedio,
                        lucro = qtdVenda * (precoAtual - itemCustodia.PrecoMedio)
                    });

                    itemCustodia.Quantidade -= qtdVenda;
                    if (itemCustodia.Quantidade <= 0)
                    {
                        // Aqui poderíamos remover da DB, mas vamos manter com 0 ou deixar o Repo lidar
                        _custodiaRepo.Update(itemCustodia); 
                    }
                    else
                    {
                        _custodiaRepo.Update(itemCustodia);
                    }
                    
                    await NotificarKafkaDedoDuro(cliente, ticker, "VENDA", qtdVenda, precoAtual);

                    // Add to Historical Operations
                    await _custodiaRepo.AddHistoricoAsync(new OperacaoHistorico
                    {
                        ClienteId = cliente.Id,
                        Ticker = ticker,
                        TipoOperacao = "VENDA",
                        Quantidade = qtdVenda,
                        PrecoUnitario = precoAtual,
                        ValorTotal = qtdVenda * precoAtual,
                        DataOperacao = DateTime.UtcNow,
                        Motivo = "REBALANCEAMENTO"
                    });
                }
            }
            else if (diff > 0) // Comprar
            {
                int qtdCompra = diff;
                if (itemCustodia != null)
                {
                    var pmAnterior = itemCustodia.PrecoMedio;
                    var qtdAnterior = itemCustodia.Quantidade;
                    itemCustodia.PrecoMedio = (qtdAnterior * pmAnterior + qtdCompra * precoAtual) / (qtdAnterior + qtdCompra);
                    itemCustodia.Quantidade += qtdCompra;
                    _custodiaRepo.Update(itemCustodia);
                }
                else
                {
                    await _custodiaRepo.AddAsync(new CustodiaFilhote
                    {
                        ContaGraficaId = cliente.ContaGrafica.Id,
                        Ticker = ticker,
                        Quantidade = qtdCompra,
                        PrecoMedio = precoAtual
                    });
                }
                
                await NotificarKafkaDedoDuro(cliente, ticker, "COMPRA", qtdCompra, precoAtual);

                // Add to Historical Operations
                await _custodiaRepo.AddHistoricoAsync(new OperacaoHistorico
                {
                    ClienteId = cliente.Id,
                    Ticker = ticker,
                    TipoOperacao = "COMPRA",
                    Quantidade = qtdCompra,
                    PrecoUnitario = precoAtual,
                    ValorTotal = qtdCompra * precoAtual,
                    DataOperacao = DateTime.UtcNow,
                    Motivo = "REBALANCEAMENTO"
                });
            }
        }

        // RN-058 / RN-059: IR sobre Vendas
        if (totalVendasNoMes > 20000)
        {
            decimal lucroTotal = detalhesVenda.Sum(d => (decimal)d.lucro);
            if (lucroTotal > 0)
            {
                decimal valorIR = Math.Round(lucroTotal * 0.20m, 2);
                await NotificarKafkaIRVenda(cliente, totalVendasNoMes, lucroTotal, valorIR, detalhesVenda);
            }
        }
    }

    private async Task NotificarKafkaDedoDuro(Cliente cliente, string ticker, string tipo, int qtd, decimal preco)
    {
        var valorOperacao = qtd * preco;
        var valorIR = Math.Round(valorOperacao * 0.00005m, 2);

        try {
            await _kafkaProducer.PublishAsync("ir-dedo-duro", cliente.Cpf, new {
                tipo = "IR_DEDO_DURO",
                clienteId = cliente.Id,
                cpf = cliente.Cpf,
                ticker = ticker,
                tipoOperacao = tipo,
                quantidade = qtd,
                precoUnitario = preco,
                valorOperacao = valorOperacao,
                aliquota = 0.00005m,
                valorIR = valorIR,
                dataOperacao = DateTime.UtcNow
            });
        } catch { /* Ignorar erro Kafka em ambiente local */ }
    }

    private async Task NotificarKafkaIRVenda(Cliente cliente, decimal totalVendas, decimal lucro, decimal valorIR, List<dynamic> detalhes)
    {
        try {
            await _kafkaProducer.PublishAsync("ir-venda", cliente.Cpf, new {
                tipo = "IR_VENDA",
                clienteId = cliente.Id,
                cpf = cliente.Cpf,
                mesReferencia = DateTime.UtcNow.ToString("yyyy-MM"),
                totalVendasMes = totalVendas,
                lucroLiquido = lucro,
                aliquota = 0.20m,
                valorIR = valorIR,
                detalhes = detalhes,
                dataCalculo = DateTime.UtcNow
            });
        } catch { /* Ignorar */ }
    }
}

public class RebalanceamentoSummary
{
    public int ClientesAfetados { get; set; }
    public List<string> AtivosRemovidos { get; set; } = new();
    public List<string> AtivosAdicionados { get; set; } = new();
}
