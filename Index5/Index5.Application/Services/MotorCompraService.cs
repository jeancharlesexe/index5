using Index5.Application.DTOs;
using Index5.Domain.Entities;
using Index5.Domain.Interfaces;

namespace Index5.Application.Services;

public class MotorCompraService
{
    private readonly IClienteRepository _clienteRepo;
    private readonly ICestaRepository _cestaRepo;
    private readonly ICustodiaRepository _custodiaRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IKafkaProducer _kafkaProducer;

    public MotorCompraService(
        IClienteRepository clienteRepo,
        ICestaRepository cestaRepo,
        ICustodiaRepository custodiaRepo,
        IUnitOfWork unitOfWork,
        IKafkaProducer kafkaProducer)
    {
        _clienteRepo = clienteRepo;
        _cestaRepo = cestaRepo;
        _custodiaRepo = custodiaRepo;
        _unitOfWork = unitOfWork;
        _kafkaProducer = kafkaProducer;
    }

    public async Task<ExecutarCompraResponse> ExecutarCompraAsync(
        string dataReferencia,
        Func<string, decimal> getCotacao)
    {
        var cesta = await _cestaRepo.GetActiveAsync();
        if (cesta == null)
            throw new InvalidOperationException("CESTA_NAO_ENCONTRADA");

        var clientes = await _clienteRepo.GetAllActivesAsync();
        if (clientes.Count == 0)
            throw new InvalidOperationException("NENHUM_CLIENTE_ATIVO");

        var valorPorCliente = clientes
            .Select(c => new { Cliente = c, Aporte = Math.Round(c.ValorMensal / 3, 2) })
            .ToList();

        var totalConsolidado = valorPorCliente.Sum(x => x.Aporte);

        // Step 1: Calculate how many shares to buy for each ticker
        var ordensCompra = new List<OrdemCompraDto>();
        var quantidadesPorTicker = new Dictionary<string, int>();

        foreach (var item in cesta.Itens)
        {
            var valorParaEsteAtivo = totalConsolidado * (item.Percentual / 100m);
            var cotacao = getCotacao(item.Ticker);
            if (cotacao <= 0) continue;

            var quantidadeCalculada = (int)Math.Truncate(valorParaEsteAtivo / cotacao);

            // Step 2: Check master custody for existing shares
            var masterCustodia = await _custodiaRepo.GetMasterByTickerAsync(item.Ticker);
            var saldoMaster = masterCustodia?.Quantidade ?? 0;

            var quantidadeAComprar = Math.Max(0, quantidadeCalculada - saldoMaster);
            var quantidadeDisponivel = quantidadeCalculada;

            quantidadesPorTicker[item.Ticker] = quantidadeDisponivel;

            if (quantidadeAComprar > 0)
            {
                var detalhes = CalcularLotesDetalhes(item.Ticker, quantidadeAComprar);

                ordensCompra.Add(new OrdemCompraDto
                {
                    Ticker = item.Ticker,
                    QuantidadeTotal = quantidadeAComprar,
                    Detalhes = detalhes,
                    PrecoUnitario = cotacao,
                    ValorTotal = quantidadeAComprar * cotacao
                });
            }

            // Reduce master custody since we're using those shares
            if (saldoMaster > 0 && masterCustodia != null)
            {
                var usedFromMaster = Math.Min(saldoMaster, quantidadeCalculada);
                masterCustodia.Quantidade -= usedFromMaster;
                _custodiaRepo.UpdateMaster(masterCustodia);
            }
        }

        // Step 3: Distribute shares to each client
        var distribuicoes = new List<DistribuicaoClienteDto>();
        var residuosMap = new Dictionary<string, int>();
        int eventosIR = 0;

        foreach (var ticker in quantidadesPorTicker.Keys)
        {
            residuosMap[ticker] = quantidadesPorTicker[ticker];
        }

        foreach (var vc in valorPorCliente)
        {
            var cliente = vc.Cliente;
            var proporcao = vc.Aporte / totalConsolidado;
            var ativosDistribuidos = new List<AtivoDistribuidoDto>();

            foreach (var item in cesta.Itens)
            {
                if (!quantidadesPorTicker.ContainsKey(item.Ticker)) continue;

                var totalDisponivel = quantidadesPorTicker[item.Ticker];
                var qtdCliente = (int)Math.Truncate(totalDisponivel * proporcao);

                if (qtdCliente <= 0) continue;

                residuosMap[item.Ticker] -= qtdCliente;

                ativosDistribuidos.Add(new AtivoDistribuidoDto
                {
                    Ticker = item.Ticker,
                    Quantidade = qtdCliente
                });

                // Update client custody
                var contaGraficaId = cliente.ContaGrafica?.Id ?? 0;
                var custodia = await _custodiaRepo.GetByContaAndTickerAsync(contaGraficaId, item.Ticker);
                var cotacao = getCotacao(item.Ticker);

                if (custodia != null)
                {
                    var pmAnterior = custodia.PrecoMedio;
                    var qtdAnterior = custodia.Quantidade;
                    custodia.PrecoMedio = (qtdAnterior * pmAnterior + qtdCliente * cotacao) / (qtdAnterior + qtdCliente);
                    custodia.Quantidade += qtdCliente;
                    _custodiaRepo.Update(custodia);
                }
                else
                {
                    await _custodiaRepo.AddAsync(new CustodiaFilhote
                    {
                        ContaGraficaId = contaGraficaId,
                        Ticker = item.Ticker,
                        Quantidade = qtdCliente,
                        PrecoMedio = cotacao
                    });
                }

                // Add to Historical Operations
                await _custodiaRepo.AddHistoricoAsync(new OperacaoHistorico
                {
                    ClienteId = cliente.Id,
                    Ticker = item.Ticker,
                    TipoOperacao = "COMPRA",
                    Quantidade = qtdCliente,
                    PrecoUnitario = cotacao,
                    ValorTotal = qtdCliente * cotacao,
                    DataOperacao = DateTime.UtcNow,
                    Motivo = "COMPRA_PROGRAMADA"
                });

                // Publish IR dedo-duro to Kafka
                var valorOperacao = qtdCliente * cotacao;
                var irDedoDuro = Math.Round(valorOperacao * 0.00005m, 2);

                try
                {
                    await _kafkaProducer.PublishAsync("ir-dedo-duro", cliente.Cpf, new
                    {
                        clienteId = cliente.Id,
                        cpf = cliente.Cpf,
                        ticker = item.Ticker,
                        valorOperacao = valorOperacao,
                        valorIR = irDedoDuro,
                        data = DateTime.UtcNow
                    });
                    eventosIR++;
                }
                catch
                {
                    // Kafka unavailable - log but don't fail
                }
            }

            distribuicoes.Add(new DistribuicaoClienteDto
            {
                ClienteId = cliente.Id,
                Nome = cliente.Nome,
                ValorAporte = vc.Aporte,
                Ativos = ativosDistribuidos
            });
        }

        // Step 4: Save residuals to master custody
        var residuosResponse = new List<ResiduoMasterDto>();
        foreach (var (ticker, residuo) in residuosMap)
        {
            if (residuo <= 0) continue;

            var masterCustodia = await _custodiaRepo.GetMasterByTickerAsync(ticker);
            var cotacao = getCotacao(ticker);

            if (masterCustodia != null)
            {
                var pmAnterior = masterCustodia.PrecoMedio;
                var qtdAnterior = masterCustodia.Quantidade;
                masterCustodia.PrecoMedio = qtdAnterior + residuo > 0
                    ? (qtdAnterior * pmAnterior + residuo * cotacao) / (qtdAnterior + residuo)
                    : cotacao;
                masterCustodia.Quantidade += residuo;
                masterCustodia.Origem = $"Residuo distribuicao {dataReferencia}";
                _custodiaRepo.UpdateMaster(masterCustodia);
            }
            else
            {
                await _custodiaRepo.AddMasterAsync(new CustodiaMaster
                {
                    Ticker = ticker,
                    Quantidade = residuo,
                    PrecoMedio = cotacao,
                    Origem = $"Residuo distribuicao {dataReferencia}"
                });
            }

            residuosResponse.Add(new ResiduoMasterDto
            {
                Ticker = ticker,
                Quantidade = residuo
            });
        }

        await _unitOfWork.SaveChangesAsync();

        return new ExecutarCompraResponse
        {
            DataExecucao = DateTime.UtcNow,
            TotalClientes = clientes.Count,
            TotalConsolidado = totalConsolidado,
            OrdensCompra = ordensCompra,
            Distribuicoes = distribuicoes,
            ResiduosCustMaster = residuosResponse,
            EventosIRPublicados = eventosIR,
            Mensagem = $"Compra programada executada com sucesso para {clientes.Count} clientes."
        };
    }

    private List<DetalheOrdemDto> CalcularLotesDetalhes(string ticker, int quantidade)
    {
        var detalhes = new List<DetalheOrdemDto>();

        var lotePadrao = quantidade / 100;
        var fracionario = quantidade % 100;

        if (lotePadrao > 0)
        {
            detalhes.Add(new DetalheOrdemDto
            {
                Tipo = "LOTE_PADRAO",
                Ticker = ticker,
                Quantidade = lotePadrao * 100
            });
        }

        if (fracionario > 0)
        {
            detalhes.Add(new DetalheOrdemDto
            {
                Tipo = "FRACIONARIO",
                Ticker = ticker + "F",
                Quantidade = fracionario
            });
        }

        return detalhes;
    }
}
