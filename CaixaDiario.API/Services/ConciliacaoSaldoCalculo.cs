using CaixaDiario.API.DTOs.ConciliacaoSaldo;
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

// Money Vision ("toda vez preciso lançar ajuste de saldo"): cobertura do extrato, status de
// conciliação com o banco e o diagnóstico de uma diferença — tudo leitura pura, sem repositório.
public static class ConciliacaoSaldoCalculo
{
    public const decimal Tolerancia = 0.01m;
    // Buraco menor que isso (fim de semana + feriado) não é "dia sem extrato" de verdade.
    private const int DiasMinimosLacuna = 5;
    private const double SimilaridadeMinimaDuplicata = 0.9;
    // Busca de combinação: limita o tamanho pra nunca explodir (C(40,3) ≈ 10 mil).
    private const int MaxCandidatosCombinacao = 40;
    private const int MaxItensCombinacao = 3;

    private static readonly string[] PalavrasDeEntrada = { "RECEBID", "ESTORNO", "DEVOLU", "REEMBOLSO", "CASHBACK", "CREDITO EM CONTA" };
    private static readonly string[] PalavrasDeSaida = { "ENVIAD", "PAGAMENTO", "PGTO", "COMPRA", "DEBITO AUTOMATICO" };

    public static SortedSet<DateOnly> DiasComExtrato(
        IEnumerable<PeriodoExtrato> periodos, IEnumerable<TransacaoImportada> importadas, IEnumerable<RegistroDiario> registrosDaConta)
    {
        var dias = new SortedSet<DateOnly>();
        foreach (var p in periodos)
            for (var d = p.De; d <= p.Ate; d = d.AddDays(1)) dias.Add(d);
        foreach (var t in importadas.Where(t => t.Status != "Ignorada")) dias.Add(t.Data);
        foreach (var r in registrosDaConta.Where(r => !r.Excluido && (r.Entradas.Any(e => e.FitId != null) || r.Saidas.Any(s => s.FitId != null))))
            dias.Add(r.Data);
        return dias;
    }

    public static PontoConciliacaoDto AvaliarPonto(ContaBancaria conta, IEnumerable<RegistroDiario> registros, PontoConciliacao ponto)
    {
        var saldoApp = SaldoEncadeamento.SaldoNoFimDe(conta, registros, ponto.Data);
        var diferenca = ponto.SaldoBanco - saldoApp;
        return new PontoConciliacaoDto
        {
            Data = ponto.Data, SaldoBanco = ponto.SaldoBanco, SaldoApp = saldoApp, Diferenca = diferenca,
            Conciliado = Math.Abs(diferenca) <= Tolerancia, Origem = ponto.Origem,
        };
    }

    public static StatusConciliacaoDto CalcularStatus(
        ContaBancaria conta, List<RegistroDiario> registrosDaConta, List<PontoConciliacao> pontos, SortedSet<DateOnly> diasComExtrato, DateOnly hoje)
    {
        var avaliados = pontos.OrderBy(p => p.Data).Select(p => AvaliarPonto(conta, registrosDaConta, p)).ToList();
        var ultimo = avaliados.LastOrDefault();
        DateOnly? divergenteDesde = null;
        if (ultimo is { Conciliado: false })
        {
            var ultimoOk = avaliados.LastOrDefault(p => p.Conciliado)?.Data;
            divergenteDesde = avaliados.First(p => !p.Conciliado && (ultimoOk == null || p.Data > ultimoOk)).Data;
        }

        var extratoAte = diasComExtrato.Count > 0 ? diasComExtrato.Max : (DateOnly?)null;
        return new StatusConciliacaoDto
        {
            ExtratoAte = extratoAte,
            DiasSemExtrato = extratoAte.HasValue ? Math.Max(0, hoje.DayNumber - extratoAte.Value.DayNumber) : null,
            SaldoApp = SaldoEncadeamento.SaldoAtual(conta, registrosDaConta),
            UltimoPonto = ultimo,
            DivergenteDesde = divergenteDesde,
        };
    }

    public static List<PeriodoDto> Lacunas(SortedSet<DateOnly> diasComExtrato, DateOnly de, DateOnly ate)
    {
        var lacunas = new List<PeriodoDto>();
        DateOnly? inicio = null;
        for (var d = de; d <= ate; d = d.AddDays(1))
        {
            if (!diasComExtrato.Contains(d)) { inicio ??= d; continue; }
            FecharLacuna(d.AddDays(-1));
        }
        FecharLacuna(ate);
        return lacunas;

        void FecharLacuna(DateOnly fim)
        {
            if (inicio.HasValue && fim.DayNumber - inicio.Value.DayNumber + 1 >= DiasMinimosLacuna)
                lacunas.Add(new PeriodoDto { De = inicio.Value, Ate = fim });
            inicio = null;
        }
    }

    // "Diferença de R$ 684,06 em 07/11. Possíveis causas: …". chavesImportadas: (data, valor com
    // sinal, descrição normalizada) das linhas que vieram de arquivo sem FITID (CSV/XLSX) — o que
    // não é FITID nem está aqui foi lançado à mão.
    // Sem saldoBanco (nenhum informado nem registrado na data): mesmas causas, só sem a diferença
    // e sem a combinação que a zera. paresMantidos: "Manter os dois" do Revisar duplicatas.
    public static DiagnosticoDiferencaDto Diagnosticar(
        ContaBancaria conta, List<RegistroDiario> registrosCliente, List<PontoConciliacao> pontos, SortedSet<DateOnly> diasComExtrato,
        HashSet<(DateOnly, decimal, string)> chavesImportadas, DateOnly data, decimal? saldoBanco,
        HashSet<(Guid, Guid)>? paresMantidos = null, IReadOnlyList<AchadoContrapartida>? contrapartidas = null)
    {
        var registrosDaConta = registrosCliente.Where(r => r.ContaBancariaId == conta.Id && !r.Excluido).ToList();
        var saldoApp = SaldoEncadeamento.SaldoNoFimDe(conta, registrosDaConta, data);
        var ultimoOk = pontos
            .Where(p => p.Data < data)
            .OrderByDescending(p => p.Data)
            .Select(p => AvaliarPonto(conta, registrosDaConta, p))
            .FirstOrDefault(p => p.Conciliado);
        var primeiroDia = registrosDaConta.Count > 0 ? registrosDaConta.Min(r => r.Data) : data;
        var desde = ultimoOk != null ? ultimoOk.Data.AddDays(1) : primeiroDia;

        var linhas = registrosDaConta.Where(r => r.Data >= desde && r.Data <= data)
            .SelectMany(r => r.Entradas.Select(e => (Linha: Linha(e.Id, r.Data, e.Descricao, e.Valor, e.Categoria), e.TipoCusto, e.FitId, e.TransferenciaId, Provisoria: false))
                .Concat(r.Saidas.Select(s => (Linha: Linha(s.Id, r.Data, s.Descricao, -s.Valor, s.Categoria), s.TipoCusto, s.FitId, s.TransferenciaId, s.Provisoria))))
            .ToList();
        var ajustes = linhas.Where(x => LancamentoFiltro.EhAjusteConciliacao(x.TipoCusto))
            .Select(x => ComEfeito(x.Linha, -x.Linha.Valor)).ToList();
        linhas = linhas.Where(x => !LancamentoFiltro.EhAjusteConciliacao(x.TipoCusto)).ToList();

        var fitIdPorLinha = linhas.Where(x => x.FitId != null).ToDictionary(x => x.Linha.Id, x => x.FitId!);
        var duplicatas = AgruparDuplicatas(linhas.Select(x => x.Linha).ToList(), fitIdPorLinha, paresMantidos ?? new());
        var sinalSuspeito = linhas.Where(x => x.TransferenciaId == null && SinalSuspeito(x.Linha))
            .Select(x => ComEfeito(x.Linha, -2 * x.Linha.Valor)).ToList();

        var transferenciasNoCliente = registrosCliente.Where(r => !r.Excluido)
            .SelectMany(r => r.Entradas.Where(e => e.TransferenciaId.HasValue).Select(e => (e.TransferenciaId!.Value, r.ContaBancariaId))
                .Concat(r.Saidas.Where(s => s.TransferenciaId.HasValue).Select(s => (s.TransferenciaId!.Value, r.ContaBancariaId))))
            .GroupBy(x => x.Item1)
            .ToDictionary(g => g.Key, g => g.Count());
        var transferenciasSemPar = linhas.Where(x => x.TransferenciaId.HasValue && transferenciasNoCliente.GetValueOrDefault(x.TransferenciaId.Value) < 2)
            .Select(x => ComEfeito(x.Linha, -x.Linha.Valor)).ToList();

        var manuais = linhas.Where(x => x.FitId == null && x.TransferenciaId == null && !x.Provisoria
                && !chavesImportadas.Contains(ChaveImportada(x.Linha.Data, x.Linha.Valor, x.Linha.Descricao)))
            .Select(x => ComEfeito(x.Linha, -x.Linha.Valor)).ToList();

        // Money Vision ("contrapartida falsa"): pontas que o app inventou nesta conta — sem o
        // lançamento real no extrato, ou repetindo um lançamento real solto. Corrigir = tirar a inventada.
        List<LinhaDiagnosticoDto> Contrapartidas(Func<AchadoContrapartida, bool> filtro) =>
            (contrapartidas ?? Array.Empty<AchadoContrapartida>()).Where(filtro)
                .SelectMany(a => new[] { a.Criada, a.OutraCriada! }.Where(p => p != null && p.ContaId == conta.Id))
                .Where(p => p.Data >= desde && p.Data <= data)
                .Select(p => ComEfeito(Linha(p.ItemId, p.Data, p.Descricao, p.ValorComSinal, "Transferência"), -p.ValorComSinal))
                .ToList();

        var diferenca = saldoBanco - saldoApp;
        var diagnostico = new DiagnosticoDiferencaDto
        {
            Data = data, SaldoBanco = saldoBanco, SaldoApp = saldoApp, Diferenca = diferenca, AnalisadoDesde = desde,
            Lacunas = Lacunas(diasComExtrato, desde, data),
            Duplicatas = duplicatas, SinalSuspeito = sinalSuspeito, TransferenciasSemPar = transferenciasSemPar, Manuais = manuais,
            Ajustes = ajustes,
            ContrapartidasSemLancamento = Contrapartidas(a => a.Tipo == TipoAchadoContrapartida.SemLancamentoNoExtrato),
            ContrapartidasDuplicadas = Contrapartidas(a => a.Tipo is TipoAchadoContrapartida.DuplicadaComReal or TipoAchadoContrapartida.ParDuplicado),
        };
        if (diferenca.HasValue) diagnostico.Combinacao = BuscarCombinacao(diagnostico, diferenca.Value);
        return diagnostico;
    }

    public static (DateOnly, decimal, string) ChaveImportada(DateOnly data, decimal valorComSinal, string descricao) =>
        (data, Math.Round(valorComSinal, 2), Normalizar(descricao));

    private static LinhaDiagnosticoDto Linha(Guid id, DateOnly data, string descricao, decimal valor, string? categoria) =>
        new() { Id = id, Data = data, Descricao = descricao, Valor = valor, Categoria = categoria };

    private static LinhaDiagnosticoDto ComEfeito(LinhaDiagnosticoDto l, decimal efeito) =>
        new() { Id = l.Id, Data = l.Data, Descricao = l.Descricao, Valor = l.Valor, Categoria = l.Categoria, EfeitoSeCorrigir = efeito };

    // Mesma data + mesmo valor + descrição ≥ 90% parecida. Duas linhas iguais podem ser legítimas
    // (2 Ubers) — por isso é só "possível", quem decide é o usuário. Nunca sugere de novo um par
    // que o usuário mandou manter, nem duas linhas com FITIDs diferentes: o próprio banco já disse
    // que são dois lançamentos.
    private static List<GrupoDuplicataDto> AgruparDuplicatas(
        List<LinhaDiagnosticoDto> linhas, Dictionary<Guid, string> fitIdPorLinha, HashSet<(Guid, Guid)> paresMantidos)
    {
        var grupos = new List<GrupoDuplicataDto>();
        var usados = new HashSet<Guid>();
        foreach (var l in linhas.OrderBy(l => l.Data))
        {
            if (usados.Contains(l.Id)) continue;
            var iguais = linhas.Where(o => o.Id != l.Id && !usados.Contains(o.Id) && o.Data == l.Data && o.Valor == l.Valor
                && Similaridade(o.Descricao, l.Descricao) >= SimilaridadeMinimaDuplicata
                && !ParLegitimo(l.Id, o.Id, fitIdPorLinha, paresMantidos)).ToList();
            if (iguais.Count == 0) continue;

            usados.Add(l.Id);
            foreach (var o in iguais) usados.Add(o.Id);
            var excedentes = iguais.Select(o => ComEfeito(o, -o.Valor)).ToList();
            grupos.Add(new GrupoDuplicataDto
            {
                Itens = excedentes.Prepend(ComEfeito(l, 0m)).ToList(),
                SomaExcedente = excedentes.Sum(o => o.Valor),
            });
        }
        return grupos;
    }

    public static bool ParLegitimo(Guid a, Guid b, Dictionary<Guid, string> fitIdPorLinha, HashSet<(Guid, Guid)> paresMantidos) =>
        paresMantidos.Contains(ParOrdenado(a, b))
        || (fitIdPorLinha.TryGetValue(a, out var fitA) && fitIdPorLinha.TryGetValue(b, out var fitB) && fitA != fitB);

    // Mesma normalização de DuplicataDispensada (menor Guid primeiro).
    public static (Guid, Guid) ParOrdenado(Guid a, Guid b) => a.CompareTo(b) <= 0 ? (a, b) : (b, a);

    private static bool SinalSuspeito(LinhaDiagnosticoDto l)
    {
        var desc = Normalizar(l.Descricao);
        return l.Valor < 0 ? PalavrasDeEntrada.Any(desc.Contains) : PalavrasDeSaida.Any(desc.Contains);
    }

    // Menor conjunto de correções (excluir duplicata/manual/transferência sem par, ou inverter sinal)
    // cuja soma de efeitos zera a diferença. Nunca corrige nada — só aponta.
    private static CombinacaoDiagnosticoDto? BuscarCombinacao(DiagnosticoDiferencaDto d, decimal diferenca)
    {
        if (Math.Abs(diferenca) <= Tolerancia) return null;

        var candidatos = d.Duplicatas.SelectMany(g => g.Itens.Skip(1)).Select(l => (Linha: l, Tipo: "duplicata"))
            .Concat(d.SinalSuspeito.Select(l => (Linha: l, Tipo: "sinal")))
            .Concat(d.TransferenciasSemPar.Select(l => (Linha: l, Tipo: "transferencia")))
            .Concat(d.ContrapartidasDuplicadas.Concat(d.ContrapartidasSemLancamento).Select(l => (Linha: l, Tipo: "contrapartida")))
            .Concat(d.Manuais.Select(l => (Linha: l, Tipo: "manual")))
            .GroupBy(c => (c.Linha.Id, c.Tipo)).Select(g => g.First())
            .Take(MaxCandidatosCombinacao)
            .ToList();

        for (var tamanho = 1; tamanho <= MaxItensCombinacao; tamanho++)
        {
            var achado = Combinar(candidatos, tamanho, 0, new List<(LinhaDiagnosticoDto Linha, string Tipo)>(), diferenca);
            if (achado != null)
                return new CombinacaoDiagnosticoDto { Resumo = Resumir(achado), Itens = achado.Select(a => a.Linha).ToList() };
        }
        return null;
    }

    private static List<(LinhaDiagnosticoDto Linha, string Tipo)>? Combinar(
        List<(LinhaDiagnosticoDto Linha, string Tipo)> candidatos, int faltam, int inicio,
        List<(LinhaDiagnosticoDto Linha, string Tipo)> escolhidos, decimal alvo)
    {
        if (faltam == 0)
            return Math.Abs(escolhidos.Sum(e => e.Linha.EfeitoSeCorrigir) - alvo) <= Tolerancia ? new(escolhidos) : null;

        for (var i = inicio; i < candidatos.Count; i++)
        {
            // A mesma linha não entra duas vezes (ex.: duplicata E manual ao mesmo tempo).
            if (escolhidos.Any(e => e.Linha.Id == candidatos[i].Linha.Id)) continue;
            escolhidos.Add(candidatos[i]);
            var achado = Combinar(candidatos, faltam - 1, i + 1, escolhidos, alvo);
            escolhidos.RemoveAt(escolhidos.Count - 1);
            if (achado != null) return achado;
        }
        return null;
    }

    private static string Resumir(List<(LinhaDiagnosticoDto Linha, string Tipo)> itens)
    {
        var partes = itens.GroupBy(i => i.Tipo).Select(g => (g.Key, Qtd: g.Count()) switch
        {
            ("duplicata", 1) => "excluir esta duplicata",
            ("duplicata", var n) => $"excluir estas {n} duplicatas",
            ("sinal", 1) => "inverter o sinal deste lançamento",
            ("sinal", var n) => $"inverter o sinal destes {n} lançamentos",
            ("transferencia", 1) => "excluir esta transferência sem par",
            ("transferencia", var n) => $"excluir estas {n} transferências sem par",
            ("contrapartida", 1) => "corrigir esta contrapartida de transferência",
            ("contrapartida", var n) => $"corrigir estas {n} contrapartidas de transferência",
            (_, 1) => "excluir este lançamento manual",
            (_, var n) => $"excluir estes {n} lançamentos manuais",
        });
        var texto = string.Join(" e ", partes);
        return char.ToUpperInvariant(texto[0]) + texto[1..] + " zera a diferença";
    }

    private static string Normalizar(string s) =>
        new string(s.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray()).ToUpperInvariant().Trim();

    // 1 − distância de Levenshtein / tamanho da maior — "PIX ENVIADO JOAO" × "PIX ENVIADO JOÃO" = 1.
    private static double Similaridade(string a, string b)
    {
        a = Normalizar(a);
        b = Normalizar(b);
        if (a == b) return 1;
        if (a.Length == 0 || b.Length == 0) return 0;

        var anterior = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var atual = new int[b.Length + 1];
            atual[0] = i;
            for (var j = 1; j <= b.Length; j++)
                atual[j] = Math.Min(Math.Min(atual[j - 1] + 1, anterior[j] + 1), anterior[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            anterior = atual;
        }
        return 1 - (double)anterior[b.Length] / Math.Max(a.Length, b.Length);
    }
}
