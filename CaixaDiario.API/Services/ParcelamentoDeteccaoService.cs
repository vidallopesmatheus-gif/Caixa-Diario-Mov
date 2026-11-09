using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

// Rodada de ajustes "parcelamentos": quando a importação da fatura detecta uma parcela (ver
// ParcelaDescricaoParser), este serviço resolve o Parcelamento (cria na primeira vez que aparece,
// reaproveita nas seguintes) e mantém Contas a Pagar sincronizado — as parcelas que ainda não
// chegaram por nenhuma fatura real ficam como PREVISTAS (ContaProvisionada), e viram reais (somem
// daqui) assim que a fatura do mês correspondente trouxer a parcela de verdade.
public class ParcelamentoDeteccaoService : IParcelamentoDeteccaoService
{
    // Mesma margem usada em outros matches de "é a mesma coisa que vi antes" no app (ver
    // TransferenciaService/ImportacaoService) — parcelas de uma mesma compra têm valor idêntico na
    // prática, a tolerância é só pra absorver arredondamento de centavo entre meses.
    private const decimal ToleranciaValor = 0.01m;
    // Parcela lançada pelo app pode ter o valor digitado/dividido pelo usuário (centavos diferentes
    // do que o banco cobra) — margem maior, compensada por exigir o lugar reservado da parcela.
    private const decimal ToleranciaValorLancadoPeloApp = 0.02m;

    private readonly IParcelamentoRepository _parcelamentoRepo;
    private readonly IRegistroRepository _registroRepo;

    public ParcelamentoDeteccaoService(IParcelamentoRepository parcelamentoRepo, IRegistroRepository registroRepo)
    {
        _parcelamentoRepo = parcelamentoRepo;
        _registroRepo = registroRepo;
    }

    public async Task<Parcelamento> ProcessarParcelaDetectadaAsync(
        ContaBancaria contaCartao, DateOnly dataVencimentoFatura,
        string comerciante, string? categoria, int numero, int total, decimal valorParcela)
    {
        var registros = await _registroRepo.ListarPorContaAsync(contaCartao.Id);

        var parcelamento = await ResolverOuCriarParcelamentoAsync(registros, contaCartao, dataVencimentoFatura, comerciante, categoria, numero, total, valorParcela);

        await RemoverPrevistaSubstituidaAsync(registros, parcelamento.Id, numero);
        await RemoverParcelaManualSubstituidaAsync(registros, parcelamento.Id, numero);
        await GerarPrevistasQueFaltamAsync(registros, contaCartao, parcelamento, dataVencimentoFatura, comerciante, categoria, numero, total, valorParcela);

        // A parcela recém-chegada é a última do parcelamento e não sobrou nenhuma prevista — quita.
        if (numero == total)
        {
            parcelamento.Status = "Quitado";
            await _parcelamentoRepo.AtualizarAsync(parcelamento);
        }

        return parcelamento;
    }

    private async Task<Parcelamento> ResolverOuCriarParcelamentoAsync(
        List<RegistroDiario> registros, ContaBancaria contaCartao, DateOnly dataVencimentoFatura, string comerciante,
        string? categoria, int numero, int total, decimal valorParcela)
    {
        var ativos = await _parcelamentoRepo.ListarAtivosPorContaAsync(contaCartao.Id);
        var existente = ativos.FirstOrDefault(p =>
            p.TotalParcelas == total
            && NormalizarComparacao(p.DescricaoBase) == NormalizarComparacao(comerciante)
            && Math.Abs(p.ValorParcela - valorParcela) <= valorParcela * ToleranciaValor);
        if (existente != null) return existente;

        // Parcelamento lançado pelo próprio app (ex.: "🏆 Macbook" via Conquistei) tem descrição
        // diferente da que o banco usa ("Trocafone - Parcela 2/10") — casa pelo lugar reservado pra
        // esta parcela (prevista, ou manual ainda sem FitId) com valor ±2%. Só decide sozinho com
        // um único candidato.
        var porLugarReservado = ativos.Where(p =>
            p.TotalParcelas == total
            && Math.Abs(p.ValorParcela - valorParcela) <= valorParcela * ToleranciaValorLancadoPeloApp
            && TemLugarReservado(registros, p.Id, numero)).ToList();
        if (porLugarReservado.Count == 1) return porLugarReservado[0];

        var novo = new Parcelamento
        {
            Id = Guid.NewGuid(),
            ClienteId = contaCartao.ClienteId,
            ContaCartaoId = contaCartao.Id,
            DescricaoBase = comerciante,
            Categoria = categoria,
            ValorParcela = valorParcela,
            TotalParcelas = total,
            // Parcelas 1..numero-1 já aconteceram antes de o app existir/detectar — a data da
            // primeira é inferida pra trás a partir da parcela atual, nunca observada de verdade.
            DataPrimeira = dataVencimentoFatura.AddMonths(-(numero - 1)),
            ValorTotal = valorParcela * total,
            Status = "Ativo",
            CriadoEm = DateTime.UtcNow,
        };
        return await _parcelamentoRepo.AdicionarAsync(novo);
    }

    private async Task RemoverPrevistaSubstituidaAsync(List<RegistroDiario> registros, Guid parcelamentoId, int numero)
    {
        foreach (var registro in registros)
        {
            var prevista = registro.ContasPagar.FirstOrDefault(c => c.ParcelamentoId == parcelamentoId && c.NumeroParcela == numero);
            if (prevista == null) continue;

            registro.ContasPagar = registro.ContasPagar.Where(c => c.Id != prevista.Id).ToList();
            registro.SalvoEm = DateTime.UtcNow;
            await _registroRepo.AtualizarAsync(registro);
            return; // só pode existir uma prevista por (parcelamento, número)
        }
    }

    private static bool TemLugarReservado(List<RegistroDiario> registros, Guid parcelamentoId, int numero) =>
        registros.Any(r =>
            r.ContasPagar.Any(c => c.ParcelamentoId == parcelamentoId && c.NumeroParcela == numero && !c.Pago)
            || r.Saidas.Any(s => s.ParcelamentoId == parcelamentoId && s.NumeroParcela == numero && s.FitId == null));

    // Parcela que o app lançou como realizada (ex.: a 1ª de "Lançar agora") e que a fatura real
    // agora traz de verdade: a real substitui a manual — senão a mesma parcela contaria duas vezes
    // no saldo e no DRE. Nunca toca em parcela que já veio de um arquivo (tem FitId).
    private async Task RemoverParcelaManualSubstituidaAsync(List<RegistroDiario> registros, Guid parcelamentoId, int numero)
    {
        foreach (var registro in registros)
        {
            var manual = registro.Saidas.FirstOrDefault(s => s.ParcelamentoId == parcelamentoId && s.NumeroParcela == numero && s.FitId == null);
            if (manual == null) continue;

            registro.Saidas = registro.Saidas.Where(s => s.Id != manual.Id).ToList();
            registro.SaldoFinal += manual.Valor;
            registro.SalvoEm = DateTime.UtcNow;
            await _registroRepo.AtualizarAsync(registro);
            return;
        }
    }

    private async Task GerarPrevistasQueFaltamAsync(
        List<RegistroDiario> registros, ContaBancaria contaCartao, Parcelamento parcelamento, DateOnly dataVencimentoFatura,
        string comerciante, string? categoria, int numero, int total, decimal valorParcela)
    {
        // Parcelas que já existem em algum lugar (prevista OU real) não podem ganhar uma segunda
        // prevista — inclui a que acabou de chegar real agora (numero), mesmo que o snapshot de
        // "registros" seja anterior à remoção feita em RemoverPrevistaSubstituidaAsync.
        var numerosExistentes = registros
            .SelectMany(r => r.ContasPagar.Where(c => c.ParcelamentoId == parcelamento.Id).Select(c => c.NumeroParcela!.Value))
            .Concat(registros.SelectMany(r => r.Saidas.Where(s => s.ParcelamentoId == parcelamento.Id).Select(s => s.NumeroParcela!.Value)))
            .ToHashSet();
        numerosExistentes.Add(numero);

        for (var k = numero + 1; k <= total; k++)
        {
            if (numerosExistentes.Contains(k)) continue;

            var dataVencimento = dataVencimentoFatura.AddMonths(k - numero);
            var (registroFuturo, novoRegistro) = await RegistroDiaHelper.ResolverOuCriarAsync(_registroRepo, contaCartao, dataVencimento);
            registroFuturo.ContasPagar = new List<ContaProvisionada>(registroFuturo.ContasPagar)
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Descricao = $"{comerciante} - Parcela {k}/{total}",
                    Valor = valorParcela,
                    DataVencimento = dataVencimento,
                    Categoria = categoria,
                    ParcelamentoId = parcelamento.Id,
                    NumeroParcela = k,
                },
            };
            registroFuturo.SalvoEm = DateTime.UtcNow;
            await RegistroDiaHelper.PersistirAsync(_registroRepo, registroFuturo, novoRegistro);
        }
    }

    private static string NormalizarComparacao(string texto) => texto.Trim().ToUpperInvariant();
}
