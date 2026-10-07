using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class RecorrenciaService : IRecorrenciaService
{
    private readonly IContaRecorrenteRepository _contaRepo;
    private readonly IRegistroRepository _registroRepo;
    private readonly IOcorrenciaRecorrenteDispensadaRepository _dispensadaRepo;

    public RecorrenciaService(
        IContaRecorrenteRepository contaRepo, IRegistroRepository registroRepo,
        IOcorrenciaRecorrenteDispensadaRepository dispensadaRepo)
    {
        _contaRepo = contaRepo;
        _registroRepo = registroRepo;
        _dispensadaRepo = dispensadaRepo;
    }

    public async Task DispensarOcorrenciaAsync(Guid clienteId, Guid recorrenciaId, DateOnly dataVencimento)
    {
        if (await _dispensadaRepo.ExisteAsync(recorrenciaId, dataVencimento)) return;
        await _dispensadaRepo.AdicionarAsync(new OcorrenciaRecorrenteDispensada
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, RecorrenciaId = recorrenciaId,
            DataVencimento = dataVencimento, CriadoEm = DateTime.UtcNow,
        });
    }

    private static int DiffMeses(DateOnly a, DateOnly b) => (b.Year - a.Year) * 12 + (b.Month - a.Month);

    /// <summary>
    /// Indica se a conta recorrente gera uma ocorrência na data <paramref name="dia"/>.
    /// Regra de borda de fim-de-mês (D6): a correspondência é por dia exato
    /// (<c>dia.Day == DataInicio.Day</c>). Meses que não possuem aquele dia
    /// (ex.: dia 31 em fevereiro) simplesmente NÃO geram ocorrência — não há ajuste para o
    /// último dia do mês.
    /// </summary>
    public static bool OcorreEm(ContaRecorrente c, DateOnly dia)
    {
        if (dia < c.DataInicio) return false;
        if (c.DataFim.HasValue && dia > c.DataFim.Value) return false;

        if (!Bate(c, dia)) return false;

        if (c.QuantidadeParcelas.HasValue)
        {
            var indice = ContarOcorrenciasAte(c, dia); // índice 1-based desta ocorrência
            if (indice > c.QuantidadeParcelas.Value) return false;
        }
        return true;
    }

    // DiaVencimento (Fase 1.1) é um override explícito do dia do mês — DataInicio continua sendo
    // só "quando a recorrência começou" (usado pra contagem de meses/parcelas), nunca mais usado
    // como dia-de-vencimento quando DiaVencimento está preenchido.
    private static int DiaAlvo(ContaRecorrente c) => c.DiaVencimento ?? c.DataInicio.Day;

    // Verifica apenas o casamento da periodicidade (sem limites de DataFim/parcelas).
    private static bool Bate(ContaRecorrente c, DateOnly dia) => c.Periodicidade switch
    {
        "Semanal"    => (dia.DayNumber - c.DataInicio.DayNumber) % 7 == 0,
        "Quinzenal"  => (dia.DayNumber - c.DataInicio.DayNumber) % 14 == 0,
        "Mensal"     => dia.Day == DiaAlvo(c),
        "Trimestral" => dia.Day == DiaAlvo(c) && DiffMeses(c.DataInicio, dia) % 3 == 0,
        "Semestral"  => dia.Day == DiaAlvo(c) && DiffMeses(c.DataInicio, dia) % 6 == 0,
        "Anual"      => dia.Day == DiaAlvo(c) && dia.Month == c.DataInicio.Month,
        _ => false,
    };

    /// <summary>
    /// Nº de ocorrências (1-based) desde <c>DataInicio</c> até <paramref name="dia"/> inclusive,
    /// contando apenas as datas em que a periodicidade casa. Retorna 0 se <paramref name="dia"/>
    /// for anterior ao início ou não houver ocorrência até lá.
    /// </summary>
    public static int ContarOcorrenciasAte(ContaRecorrente c, DateOnly dia)
    {
        if (dia < c.DataInicio) return 0;

        return c.Periodicidade switch
        {
            "Semanal"    => (dia.DayNumber - c.DataInicio.DayNumber) / 7 + 1,
            "Quinzenal"  => (dia.DayNumber - c.DataInicio.DayNumber) / 14 + 1,
            // Para periodicidades mensais/anuais, contamos quantas datas-âncora casam até o dia.
            _ => ContarMensais(c, dia),
        };
    }

    private static int ContarMensais(ContaRecorrente c, DateOnly dia)
    {
        // Itera mês a mês a partir do início, construindo a âncora real no DiaAlvo (não no dia
        // clampado de DataInicio — importante quando DiaVencimento diverge do dia de início).
        var diaAlvo = DiaAlvo(c);
        var count = 0;
        for (var passos = 0; ; passos++)
        {
            var mesBase = c.DataInicio.AddMonths(passos);
            if (new DateOnly(mesBase.Year, mesBase.Month, 1) > dia) break;

            var diasNoMes = DateTime.DaysInMonth(mesBase.Year, mesBase.Month);
            if (diaAlvo > diasNoMes) continue; // mês sem esse dia (ex.: 31 em fevereiro) — sem ocorrência

            var anchor = new DateOnly(mesBase.Year, mesBase.Month, diaAlvo);
            if (anchor > dia) break;
            if (Bate(c, anchor)) count++;
        }
        return count;
    }

    // Fase 1.1: quando ValorVariavel, o previsto de cada ocorrência NOVA é a média do valor
    // efetivo (ValorRealizado ?? Valor) das últimas 3 ocorrências já pagas dessa recorrência —
    // nunca o Valor cadastrado, que aqui só serve de fallback sem histórico ainda. Usado também
    // por ProjecaoService e pelo relatório Previsto×Realizado — nunca duplicar essa conta.
    public static decimal CalcularValorPrevisto(ContaRecorrente conta, List<RegistroDiario> registrosDoCliente)
    {
        if (!conta.ValorVariavel) return conta.Valor;

        var ultimasPagas = registrosDoCliente
            .SelectMany(r => r.ContasReceber.Concat(r.ContasPagar))
            .Where(c => c.RecorrenciaId == conta.Id && c.Pago && c.DataBaixa.HasValue)
            .OrderByDescending(c => c.DataBaixa)
            .Take(3)
            .Select(c => c.ValorRealizado ?? c.Valor)
            .ToList();

        return ultimasPagas.Count > 0
            ? Math.Round(ultimasPagas.Average(), 2, MidpointRounding.AwayFromZero)
            : conta.Valor;
    }

    public async Task MaterializarMesAtualAsync(Guid clienteId)
    {
        var hoje = DataLocalHelper.Hoje();
        var primeiroDia = new DateOnly(hoje.Year, hoje.Month, 1);
        var ultimoDia = primeiroDia.AddMonths(1).AddDays(-1);

        var ativas = await _contaRepo.ListarAtivasPorClienteAsync(clienteId);
        if (ativas.Count == 0) return;

        var registrosDoMes = await _registroRepo.ListarPorPeriodoAsync(clienteId, primeiroDia, ultimoDia);

        // Dedup por (RecorrenciaId, dia de vencimento): cada ocorrência no mês é única.
        // Honra a periodicidade (D6): uma conta pode ter várias ocorrências no mês (ex.: Semanal).
        var materializados = new HashSet<(Guid, DateOnly)>(
            registrosDoMes.SelectMany(r =>
                r.ContasReceber.Concat(r.ContasPagar)
                    .Where(c => c.RecorrenciaId.HasValue && c.DataVencimento.HasValue)
                    .Select(c => (c.RecorrenciaId!.Value, c.DataVencimento!.Value))));

        // Ocorrências que o cliente excluiu explicitamente — "não materializado neste mês" não
        // significa "ainda não gerado" quando o motivo é essa exclusão deliberada.
        var dispensadas = new HashSet<(Guid, DateOnly)>(
            (await _dispensadaRepo.ListarPorClienteAsync(clienteId))
                .Select(d => (d.RecorrenciaId, d.DataVencimento)));

        // Calcula todas as (conta, dia) a materializar neste mês.
        var aMaterializar = new List<(ContaRecorrente Conta, DateOnly Dia)>();
        foreach (var conta in ativas)
        {
            for (var dia = primeiroDia; dia <= ultimoDia; dia = dia.AddDays(1))
            {
                if (!OcorreEm(conta, dia)) continue;
                if (materializados.Contains((conta.Id, dia))) continue;
                if (dispensadas.Contains((conta.Id, dia))) continue;
                aMaterializar.Add((conta, dia));
            }
        }

        if (aMaterializar.Count == 0) return;

        // Saldo-semente para registros novos: último SaldoFinal anterior à data do registro.
        var todos = await _registroRepo.ListarPorClienteAsync(clienteId);

        // Find-or-create do registro de cada (dia, conta) que recebe ocorrência — a chave TEM que
        // incluir a conta: duas recorrências de contas diferentes vencendo no mesmo dia não podem
        // cair no mesmo RegistroDiario (bug da Fase 0.2: antes a chave era só o dia, então a
        // segunda recorrência do dia acabava gravada na conta errada).
        var registrosTocados = new Dictionary<(DateOnly Dia, Guid ContaId), RegistroDiario>();
        var novosRegistros = new List<RegistroDiario>();

        RegistroDiario ObterRegistro(DateOnly dia, Guid contaBancariaId)
        {
            var chave = (dia, contaBancariaId);
            if (registrosTocados.TryGetValue(chave, out var existente)) return existente;

            var reg = registrosDoMes.FirstOrDefault(r => r.Data == dia && r.ContaBancariaId == contaBancariaId);
            if (reg == null)
            {
                var saldoAnterior = todos
                    .Where(r => r.ContaBancariaId == contaBancariaId && r.Data < dia)
                    .OrderByDescending(r => r.Data)
                    .FirstOrDefault()?.SaldoFinal ?? 0m;

                reg = new RegistroDiario
                {
                    Id = Guid.NewGuid(),
                    ClienteId = clienteId,
                    ContaBancariaId = contaBancariaId,
                    Data = dia,
                    Inicio = saldoAnterior,
                    SaldoFinal = saldoAnterior,
                    CriadoEm = DateTime.UtcNow,
                    SalvoEm = DateTime.UtcNow,
                };
                novosRegistros.Add(reg);
            }

            registrosTocados[chave] = reg;
            return reg;
        }

        foreach (var (conta, dia) in aMaterializar)
        {
            var registro = ObterRegistro(dia, conta.ContaBancariaId);
            var nova = new ContaProvisionada
            {
                Id = Guid.NewGuid(),
                Descricao = conta.Descricao,
                Valor = CalcularValorPrevisto(conta, todos),
                DataVencimento = dia,
                Pago = false,
                Categoria = conta.Categoria,
                RecorrenciaId = conta.Id,
                ContaBancariaId = conta.ContaBancariaId,
            };

            if (conta.Tipo == "Receber")
                registro.ContasReceber.Add(nova);
            else
                registro.ContasPagar.Add(nova);
        }

        foreach (var novo in novosRegistros)
            await _registroRepo.AdicionarAsync(novo);

        foreach (var registro in registrosTocados.Values.Where(r => !novosRegistros.Contains(r)))
            await _registroRepo.AtualizarAsync(registro);
    }
}
