using Index5.Application.DTOs;
using Index5.Domain.Entities;
using Index5.Domain.Interfaces;

namespace Index5.Application.Services;

public class ClienteService
{
    private readonly IClienteRepository _clienteRepo;
    private readonly IUnitOfWork _unitOfWork;

    public ClienteService(IClienteRepository clienteRepo, IUnitOfWork unitOfWork)
    {
        _clienteRepo = clienteRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<AdesaoResponse> AderirAsync(AdesaoRequest request)
    {
        var existing = await _clienteRepo.GetByCpfAsync(request.Cpf);
        if (existing != null)
            throw new InvalidOperationException("CLIENTE_CPF_DUPLICADO");

        if (request.ValorMensal < 100)
            throw new InvalidOperationException("VALOR_MENSAL_INVALIDO");

        var cliente = new Cliente
        {
            Nome = request.Nome,
            Cpf = request.Cpf,
            Email = request.Email,
            ValorMensal = request.ValorMensal,
            Ativo = true,
            DataAdesao = DateTime.UtcNow,
            ContaGrafica = new ContaGrafica
            {
                NumeroConta = $"FLH-{DateTime.UtcNow.Ticks % 1000000:D6}",
                Tipo = "FILHOTE",
                DataCriacao = DateTime.UtcNow
            }
        };

        await _clienteRepo.AddAsync(cliente);
        await _unitOfWork.SaveChangesAsync();

        return new AdesaoResponse
        {
            ClienteId = cliente.Id,
            Nome = cliente.Nome,
            Cpf = cliente.Cpf,
            Email = cliente.Email,
            ValorMensal = cliente.ValorMensal,
            Ativo = cliente.Ativo,
            DataAdesao = cliente.DataAdesao,
            ContaGrafica = new ContaGraficaDto
            {
                Id = cliente.ContaGrafica.Id,
                NumeroConta = cliente.ContaGrafica.NumeroConta,
                Tipo = cliente.ContaGrafica.Tipo,
                DataCriacao = cliente.ContaGrafica.DataCriacao
            }
        };
    }

    public async Task<SaidaResponse> SairAsync(int clienteId)
    {
        var cliente = await _clienteRepo.GetByIdAsync(clienteId);
        if (cliente == null)
            throw new KeyNotFoundException("CLIENTE_NAO_ENCONTRADO");

        if (!cliente.Ativo)
            throw new InvalidOperationException("CLIENTE_JA_INATIVO");

        cliente.Ativo = false;
        cliente.DataSaida = DateTime.UtcNow;
        _clienteRepo.Update(cliente);
        await _unitOfWork.SaveChangesAsync();

        return new SaidaResponse
        {
            ClienteId = cliente.Id,
            Nome = cliente.Nome,
            Ativo = false,
            DataSaida = cliente.DataSaida,
            Mensagem = "Adesao encerrada. Sua posicao em custodia foi mantida."
        };
    }

    public async Task<AlterarValorResponse> AlterarValorMensalAsync(int clienteId, AlterarValorRequest request)
    {
        var cliente = await _clienteRepo.GetByIdAsync(clienteId);
        if (cliente == null)
            throw new KeyNotFoundException("CLIENTE_NAO_ENCONTRADO");

        if (request.NovoValorMensal < 100)
            throw new InvalidOperationException("VALOR_MENSAL_INVALIDO");

        var valorAnterior = cliente.ValorMensal;
        cliente.ValorMensal = request.NovoValorMensal;
        _clienteRepo.Update(cliente);
        await _unitOfWork.SaveChangesAsync();

        return new AlterarValorResponse
        {
            ClienteId = cliente.Id,
            ValorMensalAnterior = valorAnterior,
            ValorMensalNovo = cliente.ValorMensal,
            DataAlteracao = DateTime.UtcNow,
            Mensagem = "Valor mensal atualizado. O novo valor sera considerado a partir da proxima data de compra."
        };
    }

    public async Task<CarteiraResponse> ConsultarCarteiraAsync(int clienteId, Func<string, decimal> getCotacao)
    {
        var cliente = await _clienteRepo.GetByIdAsync(clienteId);
        if (cliente == null)
            throw new KeyNotFoundException("CLIENTE_NAO_ENCONTRADO");

        var custodias = cliente.ContaGrafica?.Custodias?.ToList() ?? new List<CustodiaFilhote>();

        var ativos = custodias.Select(c =>
        {
            var cotacaoAtual = getCotacao(c.Ticker);
            var valorAtual = c.Quantidade * cotacaoAtual;
            var pl = (cotacaoAtual - c.PrecoMedio) * c.Quantidade;
            var plPercentual = c.PrecoMedio > 0 ? ((cotacaoAtual - c.PrecoMedio) / c.PrecoMedio) * 100 : 0;

            return new AtivoCarteiraDto
            {
                Ticker = c.Ticker,
                Quantidade = c.Quantidade,
                PrecoMedio = c.PrecoMedio,
                CotacaoAtual = cotacaoAtual,
                ValorAtual = valorAtual,
                Pl = pl,
                PlPercentual = Math.Round(plPercentual, 2)
            };
        }).ToList();

        var valorAtualTotal = ativos.Sum(a => a.ValorAtual);
        var valorInvestido = custodias.Sum(c => c.Quantidade * c.PrecoMedio);
        var plTotal = valorAtualTotal - valorInvestido;
        var rentabilidade = valorInvestido > 0 ? (plTotal / valorInvestido) * 100 : 0;

        foreach (var ativo in ativos)
        {
            ativo.ComposicaoCarteira = valorAtualTotal > 0
                ? Math.Round((ativo.ValorAtual / valorAtualTotal) * 100, 2)
                : 0;
        }

        return new CarteiraResponse
        {
            ClienteId = cliente.Id,
            Nome = cliente.Nome,
            ContaGrafica = cliente.ContaGrafica?.NumeroConta ?? "",
            DataConsulta = DateTime.UtcNow,
            Resumo = new ResumoCarteiraDto
            {
                ValorTotalInvestido = Math.Round(valorInvestido, 2),
                ValorAtualCarteira = Math.Round(valorAtualTotal, 2),
                PlTotal = Math.Round(plTotal, 2),
                RentabilidadePercentual = Math.Round(rentabilidade, 2)
            },
            Ativos = ativos
        };
    }
}
