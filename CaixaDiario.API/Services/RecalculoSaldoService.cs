using CaixaDiario.API.DTOs.Admin;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

// Money Vision: "saldo das contas não fecha com o extrato" — migração de reparo pra produção.
// Reencadeia TODAS as contas pela função única (SaldoEncadeamento): Inicio = saldo final do dia
// anterior, SaldoFinal = Inicio + efeito real do dia. Mesmo padrão dry-run-por-padrão do
// MigracaoRecorrenciasService/LimpezaTransferenciasService: sem confirmar=true, só relatório.
public class RecalculoSaldoService : IRecalculoSaldoService
{
    private readonly IContaBancariaRepository _contaRepo;
    private readonly IRegistroRepository _registroRepo;

    public RecalculoSaldoService(IContaBancariaRepository contaRepo, IRegistroRepository registroRepo)
    {
        _contaRepo = contaRepo;
        _registroRepo = registroRepo;
    }

    public async Task<RecalculoSaldoResultDto> RecalcularTodasAsContasAsync(bool confirmar)
    {
        var resultado = new RecalculoSaldoResultDto { Confirmado = confirmar };
        var contas = await _contaRepo.ListarTodasAsync();

        foreach (var conta in contas)
        {
            var registros = (await _registroRepo.ListarPorContaAsync(conta.Id))
                .Where(r => !r.Excluido)
                .OrderBy(r => r.Data)
                .ToList();
            if (registros.Count == 0) continue;

            var saldoAtualAntes = registros[^1].SaldoFinal;
            var calculados = SaldoEncadeamento.Calcular(conta.SaldoInicial, registros);
            var primeiraDivergente = registros
                .Select((r, i) => (r, c: calculados[i]))
                .FirstOrDefault(x => x.r.Inicio != x.c.Inicio || x.r.SaldoFinal != x.c.Final);
            if (primeiraDivergente.r == null) continue;

            if (confirmar)
                foreach (var registro in SaldoEncadeamento.Encadear(conta.SaldoInicial, registros))
                    await _registroRepo.AtualizarAsync(registro);

            resultado.Itens.Add(new DivergenciaSaldoContaDto
            {
                ClienteId = conta.ClienteId,
                ContaBancariaId = conta.Id,
                ContaBancariaNome = conta.Nome,
                PrimeiraDataDivergente = primeiraDivergente.r.Data,
                SaldoAtualAntes = saldoAtualAntes,
                SaldoAtualDepois = calculados[^1].Final,
            });
        }

        resultado.ContasComDivergencia = resultado.Itens.Count;
        return resultado;
    }

    // Money Vision ("ajuste mexeu no DRE"): todo lançamento que é ajuste de saldo (categoria ou
    // descrição "Ajuste de saldo", ou o tipo legado "AjusteSaldo") passa pro tipo de sistema
    // AjusteConciliacao — fica fora do DRE/indicadores e para de ser reclassificável. O saldo não
    // muda (é o mesmo lançamento, só a classificação).
    public async Task<MigracaoAjustesConciliacaoResultDto> MigrarAjustesConciliacaoAsync(bool confirmar)
    {
        var resultado = new MigracaoAjustesConciliacaoResultDto { Confirmado = confirmar };

        foreach (var conta in await _contaRepo.ListarTodasAsync())
        {
            foreach (var registro in (await _registroRepo.ListarPorContaAsync(conta.Id)).Where(r => !r.Excluido))
            {
                var mudou = false;
                foreach (var e in registro.Entradas.Where(e => PrecisaConverter(e.TipoCusto, e.Categoria, e.Descricao)).ToList())
                {
                    resultado.Itens.Add(Item(conta, registro, e.Descricao, e.Valor, e.TipoCusto, e.Categoria));
                    mudou = true;
                    if (!confirmar) continue;
                    e.TipoCusto = LancamentoFiltro.TipoAjusteConciliacao;
                    e.Categoria = LancamentoFiltro.CategoriaAjusteSaldo;
                    e.PendenteCategorizacao = false;
                    e.CategoriaSugerida = false;
                }
                foreach (var s in registro.Saidas.Where(s => PrecisaConverter(s.TipoCusto, s.Categoria, s.Descricao)).ToList())
                {
                    resultado.Itens.Add(Item(conta, registro, s.Descricao, -s.Valor, s.TipoCusto, s.Categoria));
                    mudou = true;
                    if (!confirmar) continue;
                    s.TipoCusto = LancamentoFiltro.TipoAjusteConciliacao;
                    s.Categoria = LancamentoFiltro.CategoriaAjusteSaldo;
                    s.PendenteCategorizacao = false;
                    s.CategoriaSugerida = false;
                }

                if (!mudou || !confirmar) continue;
                registro.Entradas = new List<Models.ItemFinanceiro>(registro.Entradas); // força o EF a detectar a mudança (jsonb)
                registro.Saidas = new List<Models.ItemFinanceiroSaida>(registro.Saidas);
                registro.SalvoEm = DateTime.UtcNow;
                await _registroRepo.AtualizarAsync(registro);
            }
        }

        resultado.LancamentosConvertidos = resultado.Itens.Count;
        return resultado;
    }

    // Par = um ajuste e outro de sinal oposto até 7 dias depois (o "zera em 30/09, reabre em 01/10").
    private const int JanelaParDias = 7;
    private static readonly System.Globalization.CultureInfo PtBr = new("pt-BR");

    private sealed record AjusteNaConta(Models.RegistroDiario Registro, Guid Id, decimal Valor, string Descricao, string? TipoCusto, string? Categoria);

    // Money Vision ("ajustes da migração que o usuário não entende"): o par que se anula sai, o par
    // que "zera e reabre" a conta vira UM ajuste líquido, e o ajuste solto com descrição "saldo"
    // ganha uma descrição que explica. O saldo atual de nenhuma conta muda (a soma é preservada).
    // Sem "confirmar": só relatório, nada é alterado nem em memória.
    public async Task<ConsolidacaoAjustesResultDto> ConsolidarAjustesAsync(bool confirmar)
    {
        var resultado = new ConsolidacaoAjustesResultDto { Confirmado = confirmar };

        foreach (var conta in await _contaRepo.ListarTodasAsync())
        {
            var registros = (await _registroRepo.ListarPorContaAsync(conta.Id)).Where(r => !r.Excluido).OrderBy(r => r.Data).ToList();
            var ajustes = registros
                .SelectMany(r => r.Entradas.Where(e => LancamentoFiltro.PareceAjusteConciliacao(e.TipoCusto, e.Categoria, e.Descricao))
                        .Select(e => new AjusteNaConta(r, e.Id, e.Valor, e.Descricao, e.TipoCusto, e.Categoria))
                    .Concat(r.Saidas.Where(s => LancamentoFiltro.PareceAjusteConciliacao(s.TipoCusto, s.Categoria, s.Descricao))
                        .Select(s => new AjusteNaConta(r, s.Id, -s.Valor, s.Descricao, s.TipoCusto, s.Categoria))))
                .ToList();
            if (ajustes.Count == 0) continue;

            var saldoAntes = SaldoEncadeamento.SaldoAtual(conta, registros);
            var itensDaConta = new List<AjusteConsolidadoDto>();
            var alteracoes = new List<Action>();
            var usados = new HashSet<Guid>();

            foreach (var a in ajustes)
            {
                if (!usados.Add(a.Id)) continue;
                var b = ajustes.FirstOrDefault(x => !usados.Contains(x.Id) && x.Valor != 0 && Math.Sign(x.Valor) == -Math.Sign(a.Valor)
                    && x.Registro.Data >= a.Registro.Data && x.Registro.Data.DayNumber - a.Registro.Data.DayNumber <= JanelaParDias);

                if (b == null)
                {
                    if (!string.Equals(a.Descricao.Trim(), "saldo", StringComparison.OrdinalIgnoreCase)) continue;
                    var descricao = DescricaoMigracao(a.Registro.Data, SaldoEncadeamento.SaldoNoFimDe(conta, registros, a.Registro.Data));
                    itensDaConta.Add(Consolidado(conta, "Renomear", new[] { a }, a.Valor, a.Registro.Data, descricao));
                    alteracoes.Add(() => Renomear(a, descricao));
                    continue;
                }

                usados.Add(b.Id);
                var liquido = a.Valor + b.Valor;
                if (liquido == 0)
                {
                    itensDaConta.Add(Consolidado(conta, "RemoverPar", new[] { a, b }, null, null, null));
                    alteracoes.Add(() => { Remover(a); Remover(b); });
                    continue;
                }

                // Fica na data do primeiro: o saldo do fim do segundo dia (o "informado") não muda.
                var descricaoLiquido = DescricaoMigracao(b.Registro.Data, SaldoEncadeamento.SaldoNoFimDe(conta, registros, b.Registro.Data));
                itensDaConta.Add(Consolidado(conta, "Consolidar", new[] { a, b }, liquido, a.Registro.Data, descricaoLiquido));
                alteracoes.Add(() => { Remover(a); Remover(b); Adicionar(a.Registro, a.Id, liquido, descricaoLiquido); });
            }
            if (itensDaConta.Count == 0) continue;

            var saldoDepois = saldoAntes;
            if (confirmar)
            {
                alteracoes.ForEach(alterar => alterar());
                foreach (var registro in SaldoEncadeamento.Encadear(conta.SaldoInicial, registros))
                {
                    registro.Entradas = new List<Models.ItemFinanceiro>(registro.Entradas); // força o EF a detectar a mudança (jsonb)
                    registro.Saidas = new List<Models.ItemFinanceiroSaida>(registro.Saidas);
                    await _registroRepo.AtualizarAsync(registro);
                }
                saldoDepois = SaldoEncadeamento.SaldoAtual(conta, registros);
            }
            itensDaConta.ForEach(i => { i.SaldoAtualAntes = saldoAntes; i.SaldoAtualDepois = saldoDepois; });
            resultado.Itens.AddRange(itensDaConta);
        }

        return resultado;
    }

    private static string DescricaoMigracao(DateOnly data, decimal saldo) =>
        $"Ajuste automático da migração — saldo informado anteriormente em {data:dd/MM} era R$ {saldo.ToString("N2", PtBr)}";

    private static AjusteConsolidadoDto Consolidado(
        Models.ContaBancaria conta, string acao, IEnumerable<AjusteNaConta> originais, decimal? valorFinal, DateOnly? dataFinal, string? descricao) => new()
    {
        ClienteId = conta.ClienteId, ContaBancariaId = conta.Id, ContaBancariaNome = conta.Nome, Acao = acao,
        Originais = originais.Select(o => Item(conta, o.Registro, o.Descricao, o.Valor, o.TipoCusto, o.Categoria)).ToList(),
        ValorFinal = valorFinal, DataFinal = dataFinal, DescricaoNova = descricao,
    };

    private static void Remover(AjusteNaConta a)
    {
        a.Registro.Entradas.RemoveAll(e => e.Id == a.Id);
        a.Registro.Saidas.RemoveAll(s => s.Id == a.Id);
        a.Registro.SalvoEm = DateTime.UtcNow;
    }

    private static void Renomear(AjusteNaConta a, string descricao)
    {
        foreach (var e in a.Registro.Entradas.Where(e => e.Id == a.Id)) e.Descricao = descricao;
        foreach (var s in a.Registro.Saidas.Where(s => s.Id == a.Id)) s.Descricao = descricao;
        a.Registro.SalvoEm = DateTime.UtcNow;
    }

    private static void Adicionar(Models.RegistroDiario registro, Guid id, decimal valorComSinal, string descricao)
    {
        if (valorComSinal > 0)
            registro.Entradas.Add(new Models.ItemFinanceiro
            {
                Id = id, Descricao = descricao, Valor = valorComSinal,
                Categoria = LancamentoFiltro.CategoriaAjusteSaldo, TipoCusto = LancamentoFiltro.TipoAjusteConciliacao,
            });
        else
            registro.Saidas.Add(new Models.ItemFinanceiroSaida
            {
                Id = id, Descricao = descricao, Valor = -valorComSinal,
                Categoria = LancamentoFiltro.CategoriaAjusteSaldo, TipoCusto = LancamentoFiltro.TipoAjusteConciliacao,
            });
    }

    private static bool PrecisaConverter(string? tipoCusto, string? categoria, string? descricao) =>
        tipoCusto != LancamentoFiltro.TipoAjusteConciliacao && LancamentoFiltro.PareceAjusteConciliacao(tipoCusto, categoria, descricao);

    private static AjusteConvertidoDto Item(
        Models.ContaBancaria conta, Models.RegistroDiario r, string descricao, decimal valor, string? tipoCusto, string? categoria) => new()
    {
        ClienteId = conta.ClienteId, ContaBancariaId = conta.Id, Data = r.Data, Descricao = descricao, Valor = valor,
        TipoCustoAnterior = tipoCusto, CategoriaAnterior = categoria,
    };
}
