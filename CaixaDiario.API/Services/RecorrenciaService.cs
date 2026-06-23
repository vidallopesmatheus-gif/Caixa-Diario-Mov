using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class RecorrenciaService : IRecorrenciaService
{
    private readonly IContaRecorrenteRepository _contaRepo;
    private readonly IRegistroRepository _registroRepo;

    public RecorrenciaService(IContaRecorrenteRepository contaRepo, IRegistroRepository registroRepo)
    {
        _contaRepo = contaRepo;
        _registroRepo = registroRepo;
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

    // Verifica apenas o casamento da periodicidade (sem limites de DataFim/parcelas).
    private static bool Bate(ContaRecorrente c, DateOnly dia) => c.Periodicidade switch
    {
        "Semanal"    => (dia.DayNumber - c.DataInicio.DayNumber) % 7 == 0,
        "Quinzenal"  => (dia.DayNumber - c.DataInicio.DayNumber) % 14 == 0,
        "Mensal"     => dia.Day == c.DataInicio.Day,
        "Trimestral" => dia.Day == c.DataInicio.Day && DiffMeses(c.DataInicio, dia) % 3 == 0,
        "Semestral"  => dia.Day == c.DataInicio.Day && DiffMeses(c.DataInicio, dia) % 6 == 0,
        "Anual"      => dia.Day == c.DataInicio.Day && dia.Month == c.DataInicio.Month,
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
        // Itera mês a mês a partir do início e conta as datas-âncora que casam com a
        // periodicidade (Bate) e que existem (DateOnly.AddMonths faz clamp do dia, então
        // checamos dia.Day == DataInicio.Day para honrar a regra de dia-exato do D6).
        var count = 0;
        for (var passos = 0; ; passos++)
        {
            var anchor = c.DataInicio.AddMonths(passos);
            if (anchor > dia) break;
            // anchor.Day pode ter sido clampado (ex.: 31 -> 28); Bate() exige dia exato.
            if (Bate(c, anchor)) count++;
        }
        return count;
    }

    public async Task MaterializarMesAtualAsync(Guid clienteId)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
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

        // Calcula todas as (conta, dia) a materializar neste mês.
        var aMaterializar = new List<(ContaRecorrente Conta, DateOnly Dia)>();
        foreach (var conta in ativas)
        {
            for (var dia = primeiroDia; dia <= ultimoDia; dia = dia.AddDays(1))
            {
                if (!OcorreEm(conta, dia)) continue;
                if (materializados.Contains((conta.Id, dia))) continue;
                aMaterializar.Add((conta, dia));
            }
        }

        if (aMaterializar.Count == 0) return;

        // Saldo-semente para registros novos: último SaldoFinal anterior à data do registro.
        var todos = await _registroRepo.ListarPorClienteAsync(clienteId);

        // Find-or-create do registro de cada dia que recebe ocorrência.
        var registrosTocados = new Dictionary<DateOnly, RegistroDiario>();
        var novosRegistros = new List<RegistroDiario>();

        RegistroDiario ObterRegistro(DateOnly dia)
        {
            if (registrosTocados.TryGetValue(dia, out var existente)) return existente;

            var reg = registrosDoMes.FirstOrDefault(r => r.Data == dia);
            if (reg == null)
            {
                var saldoAnterior = todos
                    .Where(r => r.Data < dia)
                    .OrderByDescending(r => r.Data)
                    .FirstOrDefault()?.SaldoFinal ?? 0m;

                reg = new RegistroDiario
                {
                    Id = Guid.NewGuid(),
                    ClienteId = clienteId,
                    Data = dia,
                    Inicio = saldoAnterior,
                    SaldoFinal = saldoAnterior,
                    CriadoEm = DateTime.UtcNow,
                    SalvoEm = DateTime.UtcNow,
                };
                novosRegistros.Add(reg);
            }

            registrosTocados[dia] = reg;
            return reg;
        }

        foreach (var (conta, dia) in aMaterializar)
        {
            var registro = ObterRegistro(dia);
            var nova = new ContaProvisionada
            {
                Descricao = conta.Descricao,
                Valor = conta.Valor,
                DataVencimento = dia,
                Pago = false,
                Categoria = conta.Categoria,
                RecorrenciaId = conta.Id,
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
