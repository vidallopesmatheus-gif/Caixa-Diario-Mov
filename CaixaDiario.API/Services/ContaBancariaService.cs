using CaixaDiario.API.DTOs.ContasBancarias;
using CaixaDiario.API.DTOs.Registros;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class ContaBancariaService : IContaBancariaService
{
    private static readonly HashSet<string> TiposValidos = new() { "Caixa", "ContaCorrente", "Investimento", "CartaoCredito" };

    private readonly IContaBancariaRepository _contaRepo;
    private readonly IRegistroRepository _registroRepo;
    private readonly IMetaRepository _metaRepo;
    private readonly ITransferenciaRepository _transferenciaRepo;
    private readonly ITransacaoImportadaRepository _transacaoImportadaRepo;
    private readonly IContaRecorrenteRepository _contaRecorrenteRepo;
    private readonly IFaturaRepository _faturaRepo;
    private readonly IPagamentoFaturaRepository _pagamentoFaturaRepo;

    public ContaBancariaService(
        IContaBancariaRepository contaRepo, IRegistroRepository registroRepo, IMetaRepository metaRepo,
        ITransferenciaRepository transferenciaRepo, ITransacaoImportadaRepository transacaoImportadaRepo,
        IContaRecorrenteRepository contaRecorrenteRepo, IFaturaRepository faturaRepo, IPagamentoFaturaRepository pagamentoFaturaRepo)
    {
        _contaRepo = contaRepo;
        _registroRepo = registroRepo;
        _metaRepo = metaRepo;
        _transferenciaRepo = transferenciaRepo;
        _transacaoImportadaRepo = transacaoImportadaRepo;
        _contaRecorrenteRepo = contaRecorrenteRepo;
        _faturaRepo = faturaRepo;
        _pagamentoFaturaRepo = pagamentoFaturaRepo;
    }

    public async Task<List<ContaBancariaDto>> ListarPorClienteAsync(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);
        var contas = await _contaRepo.ListarPorClienteAsync(clienteId);
        var registros = await _registroRepo.ListarPorClienteAsync(clienteId);
        var metas = await _metaRepo.ListarPorClienteAsync(clienteId);
        var faturasEmAbertoPorConta = await ObterFaturasEmAbertoPorContaAsync(contas, registros);
        return contas.Select(c => MapToDto(c, registros, metas, faturasEmAbertoPorConta.GetValueOrDefault(c.Id))).ToList();
    }

    public async Task<ContaBancariaDto> ObterPorIdAsync(Guid id, Guid usuarioLogadoId, string perfil)
    {
        var conta = await _contaRepo.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        VerificarAcesso(conta.ClienteId, usuarioLogadoId, perfil);
        var registros = await _registroRepo.ListarPorContaAsync(id);
        var metas = await _metaRepo.ListarPorClienteAsync(conta.ClienteId);
        var faturasEmAbertoPorConta = await ObterFaturasEmAbertoPorContaAsync(new List<ContaBancaria> { conta }, registros);
        return MapToDto(conta, registros, metas, faturasEmAbertoPorConta.GetValueOrDefault(conta.Id));
    }

    // Money Vision: "card mostra R$ 0,00, mas a API e as faturas dizem outra coisa" (item 4) —
    // mesma fonte do botão "Pagar" na tela de Faturas (FaturaCartaoService.CalcularFaturas),
    // nunca o SaldoDevedor calculado a partir do ledger inteiro da conta (que pode estar zerado
    // mesmo com faturas reais não pagas, se um pagamento não foi vinculado a nenhuma fatura).
    private async Task<Dictionary<Guid, decimal>> ObterFaturasEmAbertoPorContaAsync(List<ContaBancaria> contas, List<RegistroDiario> registros)
    {
        var resultado = new Dictionary<Guid, decimal>();
        foreach (var conta in contas.Where(c => c.Tipo == "CartaoCredito"))
        {
            var registrosDaConta = registros.Where(r => r.ContaBancariaId == conta.Id && !r.Excluido).ToList();
            var pagamentos = await _pagamentoFaturaRepo.ListarPorContaCartaoAsync(conta.Id);
            var faturas = await _faturaRepo.ListarPorContaAsync(conta.Id);
            var calculadas = FaturaCartaoService.CalcularFaturas(conta, registrosDaConta, pagamentos, faturas, DataLocalHelper.Hoje());
            resultado[conta.Id] = calculadas.Where(f => f.Status != "Paga").Sum(f => f.SaldoDevedor);
        }
        return resultado;
    }

    public async Task<ContaBancariaDto> CriarAsync(CriarContaBancariaDto dto, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(dto.ClienteId, usuarioLogadoId, perfil);
        if (!TiposValidos.Contains(dto.Tipo))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, $"Tipo inválido. Use: {string.Join(", ", TiposValidos)}");

        var conta = new ContaBancaria
        {
            Id = Guid.NewGuid(),
            ClienteId = dto.ClienteId,
            Nome = dto.Nome.Trim(),
            Tipo = dto.Tipo,
            SaldoInicial = dto.SaldoInicial,
            Ativa = true,
            DataCriacao = DateTime.UtcNow,
            Limite = dto.Tipo == "CartaoCredito" ? dto.Limite : null,
            DiaFechamento = dto.Tipo == "CartaoCredito" ? dto.DiaFechamento : null,
            DiaVencimento = dto.Tipo == "CartaoCredito" ? dto.DiaVencimento : null,
        };

        var criada = await _contaRepo.AdicionarAsync(conta);
        return MapToDto(criada, new List<RegistroDiario>(), new List<MetaAnual>());
    }

    public async Task<ContaBancariaDto> AtualizarAsync(Guid id, AtualizarContaBancariaDto dto, Guid usuarioLogadoId, string perfil)
    {
        var conta = await _contaRepo.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        VerificarAcesso(conta.ClienteId, usuarioLogadoId, perfil);
        if (!TiposValidos.Contains(dto.Tipo))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, $"Tipo inválido. Use: {string.Join(", ", TiposValidos)}");

        var saldoInicialMudou = conta.SaldoInicial != dto.SaldoInicial;
        conta.Nome = dto.Nome.Trim();
        conta.Tipo = dto.Tipo;
        conta.SaldoInicial = dto.SaldoInicial;
        conta.Ativa = dto.Ativa;
        conta.Limite = dto.Tipo == "CartaoCredito" ? dto.Limite : null;
        conta.DiaFechamento = dto.Tipo == "CartaoCredito" ? dto.DiaFechamento : null;
        conta.DiaVencimento = dto.Tipo == "CartaoCredito" ? dto.DiaVencimento : null;

        var atualizada = await _contaRepo.AtualizarAsync(conta);
        // Bug real em produção (saldo não fecha com o extrato): editar o saldo inicial depois que a
        // conta já tem registros nunca resincronizava a cadeia — o primeiro dia continuava com o
        // Inicio antigo pra sempre, e todo o resto do extrato arrastava a diferença.
        if (saldoInicialMudou)
            await RegistroService.RecalcularContaInteiraAsync(_registroRepo, id, atualizada.SaldoInicial);
        var registros = await _registroRepo.ListarPorContaAsync(id);
        var metas = await _metaRepo.ListarPorClienteAsync(conta.ClienteId);
        return MapToDto(atualizada, registros, metas);
    }

    // Conta sem nenhum vínculo (lançamento, conta a pagar/receber, transferência, transação
    // importada ou meta) é removida de verdade — senão, só inativa (preserva histórico/DRE/saldos).
    public async Task<ExclusaoContaBancariaResultDto> ExcluirOuInativarAsync(Guid id, Guid usuarioLogadoId, string perfil)
    {
        var conta = await _contaRepo.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        VerificarAcesso(conta.ClienteId, usuarioLogadoId, perfil);

        // Conta a pagar/receber e transferências não têm FK própria na conta (a primeira vive
        // dentro do jsonb de outro RegistroDiario) — por isso o cliente inteiro é varrido, do
        // mesmo jeito que ObterPendenciasAsync já faz.
        var registrosDoCliente = await _registroRepo.ListarPorClienteAsync(conta.ClienteId);
        var contasProvisionadas = registrosDoCliente
            .SelectMany(r => r.ContasReceber.Concat(r.ContasPagar))
            .Count(cp => cp.ContaBancariaId == id);

        var transferencias = (await _transferenciaRepo.ListarPorClienteAsync(conta.ClienteId))
            .Count(t => t.ContaOrigemId == id || t.ContaDestinoId == id);

        var resultado = new ExclusaoContaBancariaResultDto
        {
            DiasComLancamento = await _registroRepo.ContarTodosPorContaAsync(id),
            ContasProvisionadas = contasProvisionadas,
            Transferencias = transferencias,
            TransacoesImportadas = (await _transacaoImportadaRepo.ListarPorContaAsync(id)).Count,
            MetasVinculadas = (await _metaRepo.ListarPorClienteAsync(conta.ClienteId)).Count(m => m.ContaInvestimentoId == id),
            ContasRecorrentes = await _contaRecorrenteRepo.ContarPorContaBancariaAsync(id),
        };

        if (resultado.TotalVinculos == 0)
        {
            await _contaRepo.RemoverAsync(conta);
            resultado.Excluida = true;
            return resultado;
        }

        conta.Ativa = false;
        await _contaRepo.AtualizarAsync(conta);
        resultado.Excluida = false;
        return resultado;
    }

    public async Task<List<LancamentoExtratoDto>> ObterExtratoAsync(
        Guid contaId, Guid usuarioLogadoId, string perfil, DateOnly? de, DateOnly? ate)
    {
        var conta = await _contaRepo.ObterPorIdAsync(contaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        VerificarAcesso(conta.ClienteId, usuarioLogadoId, perfil);

        var registros = (await _registroRepo.ListarPorContaAsync(contaId))
            .OrderBy(r => r.Data)
            .ToList();

        // Item 2.7: Sequencia é a ordem real de processamento (cronológica) — necessária porque
        // OrderByDescending(Data) por si só é estável e preserva a ordem ORIGINAL (ascendente)
        // entre linhas do MESMO dia. Sem Sequencia, um dia com 2+ lançamentos mostrava no topo a
        // linha mais ANTIGA daquele dia (com um SaldoAcumulado intermediário), não a mais recente
        // — que é a única que corresponde ao saldo atual de verdade da conta.
        var linhas = new List<(DateOnly Data, int Sequencia, LancamentoExtratoDto Dto)>();
        decimal saldo = conta.SaldoInicial;
        var sequencia = 0;

        foreach (var r in registros)
        {
            // Sincroniza com o início do dia armazenado, evitando deriva por edições manuais de saldo.
            saldo = r.Inicio;

            foreach (var entrada in r.Entradas)
            {
                saldo += entrada.Valor;
                linhas.Add((r.Data, sequencia++, new LancamentoExtratoDto
                {
                    Id = entrada.Id == Guid.Empty ? null : entrada.Id,
                    Data = r.Data.ToString("yyyy-MM-dd"),
                    Descricao = entrada.Descricao,
                    Categoria = entrada.Categoria,
                    Valor = entrada.Valor,
                    SaldoAcumulado = saldo,
                    PendenteCategorizacao = entrada.PendenteCategorizacao,
                    TransferenciaId = entrada.TransferenciaId,
                    RegraCategorizacaoId = entrada.RegraCategorizacaoId,
                    TipoCusto = entrada.TipoCusto,
                }));
            }

            // Cada ContaProvisionada vive numa única lista (deste r) — não filtra por
            // DataBaixa == r.Data: a data de pagamento pode legitimamente ser diferente do dia do
            // registro/vencimento (ex.: pago com atraso), e sem essa linha a baixa desaparecia do
            // extrato mesmo afetando o saldo.
            // LancamentoVinculadoId preenchido (baixa criou um lançamento novo OU vinculou a um já
            // existente — Fase 0.5) significa que esse dinheiro JÁ está contado via o próprio
            // Entrada/Saída, nos loops abaixo — somar aqui também duplicava a linha e o saldo
            // acumulado do dia. Só sintetiza essa linha pra baixas antigas sem lançamento real.
            foreach (var recebido in r.ContasReceber.Where(cp => cp.Pago && !cp.LancamentoVinculadoId.HasValue))
            {
                var valorEfetivo = recebido.ValorRealizado ?? recebido.Valor;
                saldo += valorEfetivo;
                linhas.Add((r.Data, sequencia++, new LancamentoExtratoDto
                {
                    Data = (recebido.DataBaixa ?? r.Data).ToString("yyyy-MM-dd"),
                    Descricao = $"{recebido.Descricao} (recebimento)",
                    Categoria = recebido.Categoria,
                    Valor = valorEfetivo,
                    SaldoAcumulado = saldo,
                }));
            }

            foreach (var saida in r.Saidas)
            {
                saldo -= saida.Valor;
                linhas.Add((r.Data, sequencia++, new LancamentoExtratoDto
                {
                    Id = saida.Id == Guid.Empty ? null : saida.Id,
                    Data = r.Data.ToString("yyyy-MM-dd"),
                    Descricao = saida.Descricao,
                    Categoria = saida.Categoria,
                    Valor = -saida.Valor,
                    SaldoAcumulado = saldo,
                    PendenteCategorizacao = saida.PendenteCategorizacao,
                    TransferenciaId = saida.TransferenciaId,
                    RegraCategorizacaoId = saida.RegraCategorizacaoId,
                    TipoCusto = saida.TipoCusto,
                }));
            }

            foreach (var pago in r.ContasPagar.Where(cp => cp.Pago && !cp.LancamentoVinculadoId.HasValue))
            {
                var valorEfetivo = pago.ValorRealizado ?? pago.Valor;
                saldo -= valorEfetivo;
                linhas.Add((r.Data, sequencia++, new LancamentoExtratoDto
                {
                    Data = (pago.DataBaixa ?? r.Data).ToString("yyyy-MM-dd"),
                    Descricao = $"{pago.Descricao} (pagamento)",
                    Categoria = pago.Categoria,
                    Valor = -valorEfetivo,
                    SaldoAcumulado = saldo,
                }));
            }
        }

        IEnumerable<(DateOnly Data, int Sequencia, LancamentoExtratoDto Dto)> filtradas = linhas;
        if (de.HasValue) filtradas = filtradas.Where(l => l.Data >= de.Value);
        if (ate.HasValue) filtradas = filtradas.Where(l => l.Data <= ate.Value);

        return filtradas
            .OrderByDescending(l => l.Data)
            .ThenByDescending(l => l.Sequencia)
            .Select(l => l.Dto)
            .ToList();
    }

    public async Task<PendenciasContaDto> ObterPendenciasAsync(Guid contaId, Guid usuarioLogadoId, string perfil)
    {
        var conta = await _contaRepo.ObterPorIdAsync(contaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        VerificarAcesso(conta.ClienteId, usuarioLogadoId, perfil);

        var registros = await _registroRepo.ListarPorClienteAsync(conta.ClienteId);

        var recebiveis = registros
            .SelectMany(r => r.ContasReceber)
            .Where(cp => !cp.Pago && cp.ContaBancariaId == contaId)
            .Select(MapProvisionadaToDto)
            .OrderBy(d => d.DataVencimento)
            .ToList();

        var pagamentos = registros
            .SelectMany(r => r.ContasPagar)
            .Where(cp => !cp.Pago && cp.ContaBancariaId == contaId)
            .Select(MapProvisionadaToDto)
            .OrderBy(d => d.DataVencimento)
            .ToList();

        return new PendenciasContaDto { Recebiveis = recebiveis, Pagamentos = pagamentos };
    }

    public async Task<ContaBancariaDto> RegistrarRendimentoAsync(Guid contaId, RegistrarRendimentoDto dto, Guid usuarioLogadoId, string perfil)
    {
        var conta = await _contaRepo.ObterPorIdAsync(contaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        VerificarAcesso(conta.ClienteId, usuarioLogadoId, perfil);
        if (conta.Tipo != "Investimento")
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Rendimento só pode ser registrado em conta do tipo Investimento.");
        if (dto.Valor == 0)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Valor do rendimento não pode ser zero.");
        if (dto.Data > DataLocalHelper.Hoje())
            throw new ApiException(400, CodigoRetorno.DATA_FUTURA, "Não é possível registrar data futura.", "data");

        var (registro, novo) = await RegistroDiaHelper.ResolverOuCriarAsync(_registroRepo, conta, dto.Data);
        var descricao = string.IsNullOrWhiteSpace(dto.Descricao) ? "Rendimento" : dto.Descricao;

        if (dto.Valor > 0)
        {
            registro.Entradas.Add(new ItemFinanceiro
            {
                Descricao = descricao, Valor = dto.Valor, Categoria = "Rendimento", TipoCusto = LancamentoFiltro.TipoRendimento,
            });
            registro.SaldoFinal += dto.Valor;
        }
        else
        {
            registro.Saidas.Add(new ItemFinanceiroSaida
            {
                Descricao = descricao, Valor = -dto.Valor, Categoria = "Rendimento", TipoCusto = LancamentoFiltro.TipoRendimento,
            });
            registro.SaldoFinal += dto.Valor; // dto.Valor já é negativo aqui
        }
        registro.SalvoEm = DateTime.UtcNow;
        await RegistroDiaHelper.PersistirAsync(_registroRepo, registro, novo);
        // Bug real em produção (saldo não fecha com o extrato): rendimento nunca cascateava — um
        // dia futuro já materializado (ex.: recorrência) ficava com Inicio desatualizado.
        await RegistroService.RecalcularContaInteiraAsync(_registroRepo, contaId, conta.SaldoInicial);

        var registrosAtualizados = await _registroRepo.ListarPorContaAsync(contaId);
        var metas = await _metaRepo.ListarPorClienteAsync(conta.ClienteId);
        return MapToDto(conta, registrosAtualizados, metas);
    }

    // Item 4 (Money Vision "saldo não fecha com o extrato"): "quero acertar o saldo com o banco"
    // nunca é uma troca silenciosa do Inicio — vira UM lançamento visível (categoria "Ajuste de
    // saldo", LancamentoFiltro.TipoAjusteSaldo — já excluído do DRE/médias, mas sempre visível no
    // extrato/Caixa), na data que o usuário escolher.
    public async Task<ContaBancariaDto> AjustarSaldoAsync(Guid contaId, AjustarSaldoDto dto, Guid usuarioLogadoId, string perfil)
    {
        var conta = await _contaRepo.ObterPorIdAsync(contaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        VerificarAcesso(conta.ClienteId, usuarioLogadoId, perfil);
        if (dto.Data > DataLocalHelper.Hoje())
            throw new ApiException(400, CodigoRetorno.DATA_FUTURA, "Não é possível registrar data futura.", "data");

        var (registro, novo) = await RegistroDiaHelper.ResolverOuCriarAsync(_registroRepo, conta, dto.Data);
        var diferenca = dto.SaldoReal - registro.SaldoFinal;
        if (diferenca == 0m)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "O saldo informado já bate com o saldo calculado nesta data.", "saldoReal");

        var descricao = string.IsNullOrWhiteSpace(dto.Descricao) ? "Ajuste de saldo" : dto.Descricao;
        if (diferenca > 0)
        {
            registro.Entradas.Add(new ItemFinanceiro
            {
                Descricao = descricao, Valor = diferenca, Categoria = "Ajuste de saldo", TipoCusto = LancamentoFiltro.TipoAjusteSaldo,
            });
        }
        else
        {
            registro.Saidas.Add(new ItemFinanceiroSaida
            {
                Descricao = descricao, Valor = -diferenca, Categoria = "Ajuste de saldo", TipoCusto = LancamentoFiltro.TipoAjusteSaldo,
            });
        }
        registro.SaldoFinal += diferenca;
        registro.SalvoEm = DateTime.UtcNow;
        await RegistroDiaHelper.PersistirAsync(_registroRepo, registro, novo);
        await RegistroService.RecalcularContaInteiraAsync(_registroRepo, contaId, conta.SaldoInicial);

        var registrosAtualizados = await _registroRepo.ListarPorContaAsync(contaId);
        var metas = await _metaRepo.ListarPorClienteAsync(conta.ClienteId);
        return MapToDto(conta, registrosAtualizados, metas);
    }

    public async Task<ContaBancariaDto> VincularMetaAsync(Guid contaId, Guid metaId, Guid usuarioLogadoId, string perfil)
    {
        var conta = await _contaRepo.ObterPorIdAsync(contaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        VerificarAcesso(conta.ClienteId, usuarioLogadoId, perfil);
        if (conta.Tipo != "Investimento")
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Só é possível vincular metas a uma conta do tipo Investimento.");

        var metas = await _metaRepo.ListarPorClienteAsync(conta.ClienteId);
        var meta = metas.FirstOrDefault(m => m.Id == metaId)
            ?? throw new ApiException(404, CodigoRetorno.META_NAO_ENCONTRADA, "Meta não encontrada.");

        meta.ContaInvestimentoId = contaId;
        await _metaRepo.SalvarAsync(meta);

        var registros = await _registroRepo.ListarPorContaAsync(contaId);
        var metasAtualizadas = await _metaRepo.ListarPorClienteAsync(conta.ClienteId);
        return MapToDto(conta, registros, metasAtualizadas);
    }

    public async Task<ContaBancariaDto> DesvincularMetaAsync(Guid contaId, Guid metaId, Guid usuarioLogadoId, string perfil)
    {
        var conta = await _contaRepo.ObterPorIdAsync(contaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        VerificarAcesso(conta.ClienteId, usuarioLogadoId, perfil);

        var metas = await _metaRepo.ListarPorClienteAsync(conta.ClienteId);
        var meta = metas.FirstOrDefault(m => m.Id == metaId)
            ?? throw new ApiException(404, CodigoRetorno.META_NAO_ENCONTRADA, "Meta não encontrada.");

        meta.ContaInvestimentoId = null;
        await _metaRepo.SalvarAsync(meta);

        var registros = await _registroRepo.ListarPorContaAsync(contaId);
        var metasAtualizadas = await _metaRepo.ListarPorClienteAsync(conta.ClienteId);
        return MapToDto(conta, registros, metasAtualizadas);
    }

    public async Task<List<SaldoMensalDto>> ObterHistoricoSaldoMensalAsync(Guid contaId, Guid usuarioLogadoId, string perfil, int meses = 12)
    {
        var conta = await _contaRepo.ObterPorIdAsync(contaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        VerificarAcesso(conta.ClienteId, usuarioLogadoId, perfil);

        var registros = (await _registroRepo.ListarPorContaAsync(contaId))
            .Where(r => !r.Excluido)
            .OrderBy(r => r.Data)
            .ToList();

        var hoje = DataLocalHelper.Hoje();
        var inicioMesAtual = new DateOnly(hoje.Year, hoje.Month, 1);
        var resultado = new List<SaldoMensalDto>();

        for (var i = meses - 1; i >= 0; i--)
        {
            var mesRef = inicioMesAtual.AddMonths(-i);
            // Saldo de "fim do mês" = SaldoFinal do último registro até (e incluindo) o último dia
            // do mês — não precisa existir lançamento NESSE mês específico, só o mais recente até ali.
            var fimDoMes = mesRef.AddMonths(1).AddDays(-1);
            var saldo = registros.Where(r => r.Data <= fimDoMes).Select(r => r.SaldoFinal).DefaultIfEmpty(conta.SaldoInicial).Last();
            resultado.Add(new SaldoMensalDto { Mes = $"{mesRef.Year:D4}-{mesRef.Month:D2}", Saldo = saldo });
        }

        return resultado;
    }

    private static ContaProvisionadaDto MapProvisionadaToDto(ContaProvisionada c) => new()
    {
        Descricao = c.Descricao,
        Valor = c.Valor,
        DataVencimento = c.DataVencimento,
        Pago = c.Pago,
        Categoria = c.Categoria,
        RecorrenciaId = c.RecorrenciaId,
        DataBaixa = c.DataBaixa,
        ContaBancariaId = c.ContaBancariaId,
    };

    private static void VerificarAcesso(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
    }

    /// <summary>Saldo atual = SaldoFinal do registro mais recente da conta, ou SaldoInicial se não houver nenhum.</summary>
    public static decimal ObterSaldoAtual(ContaBancaria conta, IEnumerable<RegistroDiario> registros) =>
        registros
            .Where(r => r.ContaBancariaId == conta.Id && !r.Excluido)
            .OrderByDescending(r => r.Data)
            .FirstOrDefault()?.SaldoFinal ?? conta.SaldoInicial;

    private static readonly string[] MesesAbreviados =
        { "jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez" };

    // Money Vision — Bloco 5, item 5: null quando a meta já foi atingida (nada a prever) OU quando
    // o ritmo de aporte dos últimos 3 meses não é positivo (parado ou resgatando — projetar um
    // "nunca" ou uma data no passado só confundiria, melhor não mostrar nada).
    private static string? CalcularPrevisaoAtingimento(decimal valorSonho, decimal saldoAtualConta, decimal mediaAporteMensal, DateOnly inicioMesAtual)
    {
        var faltam = valorSonho - saldoAtualConta;
        if (faltam <= 0 || mediaAporteMensal <= 0) return null;
        var mesesNecessarios = (int)Math.Ceiling(faltam / mediaAporteMensal);
        var dataAlvo = inicioMesAtual.AddMonths(mesesNecessarios);
        return $"{MesesAbreviados[dataAlvo.Month - 1]}/{dataAlvo.Year}";
    }

    private static ContaBancariaDto MapToDto(
        ContaBancaria c, IEnumerable<RegistroDiario> registros, IEnumerable<MetaAnual> metasDoCliente, decimal? faturasEmAberto = null)
    {
        var regsOrdenados = registros
            .Where(r => r.ContaBancariaId == c.Id && !r.Excluido)
            .OrderByDescending(r => r.Data)
            .ToList();

        var saldoAtual = regsOrdenados.FirstOrDefault()?.SaldoFinal ?? c.SaldoInicial;

        var hoje = DataLocalHelper.Hoje();
        var regsDoMes = regsOrdenados.Where(r => r.Data.Year == hoje.Year && r.Data.Month == hoje.Month);
        // Transferências e rendimento não são receita/despesa — não entram nos cards de entradas/saídas do mês.
        var entradasMes = regsDoMes.SelectMany(r => r.Entradas).Where(e => LancamentoFiltro.EhOperacional(e.TipoCusto)).Sum(e => e.Valor);
        var saidasMes = regsDoMes.SelectMany(r => r.Saidas).Where(s => LancamentoFiltro.EhOperacional(s.TipoCusto)).Sum(s => s.Valor);

        var dto = new ContaBancariaDto
        {
            Id = c.Id,
            ClienteId = c.ClienteId,
            Nome = c.Nome,
            Tipo = c.Tipo,
            SaldoInicial = c.SaldoInicial,
            SaldoAtual = saldoAtual,
            EntradasMes = entradasMes,
            SaidasMes = saidasMes,
            PendentesCategorizacao = regsOrdenados.Sum(r =>
                r.Entradas.Count(e => e.PendenteCategorizacao) + r.Saidas.Count(s => s.PendenteCategorizacao)),
            Ativa = c.Ativa,
            DataCriacao = c.DataCriacao,
        };

        if (c.Tipo == "Investimento")
        {
            var todasEntradas = regsOrdenados.SelectMany(r => r.Entradas).ToList();
            var todasSaidas = regsOrdenados.SelectMany(r => r.Saidas).ToList();

            var transferenciasIn = todasEntradas.Where(e => e.TipoCusto == LancamentoFiltro.TipoTransferencia).Sum(e => e.Valor);
            var transferenciasOut = todasSaidas.Where(s => s.TipoCusto == LancamentoFiltro.TipoTransferencia).Sum(s => s.Valor);
            dto.TotalAportado = c.SaldoInicial + transferenciasIn - transferenciasOut;

            var rendimentoIn = todasEntradas.Where(e => e.TipoCusto == LancamentoFiltro.TipoRendimento).Sum(e => e.Valor);
            var rendimentoOut = todasSaidas.Where(s => s.TipoCusto == LancamentoFiltro.TipoRendimento).Sum(s => s.Valor);
            dto.RendimentoAcumulado = rendimentoIn - rendimentoOut;
            dto.RentabilidadePercentual = dto.TotalAportado > 0
                ? Math.Round(dto.RendimentoAcumulado.Value / dto.TotalAportado.Value * 100, 2)
                : null;

            // ── Bloco 5, item 3 (Money Vision): resumo do MÊS e rentabilidade mês/ano — diferente
            // do acumulado desde o início acima. ──
            var regsDoMesAtual = regsOrdenados.Where(r => r.Data.Year == hoje.Year && r.Data.Month == hoje.Month).ToList();
            var aporteMes = regsDoMesAtual.SelectMany(r => r.Entradas).Where(e => e.TipoCusto == LancamentoFiltro.TipoTransferencia).Sum(e => e.Valor);
            var resgateMes = regsDoMesAtual.SelectMany(r => r.Saidas).Where(s => s.TipoCusto == LancamentoFiltro.TipoTransferencia).Sum(s => s.Valor);
            dto.AporteMesAtual = aporteMes;
            dto.ResgateMesAtual = resgateMes;
            dto.AporteLiquidoMesAtual = aporteMes - resgateMes;

            var rendimentoInMes = regsDoMesAtual.SelectMany(r => r.Entradas).Where(e => e.TipoCusto == LancamentoFiltro.TipoRendimento).Sum(e => e.Valor);
            var rendimentoOutMes = regsDoMesAtual.SelectMany(r => r.Saidas).Where(s => s.TipoCusto == LancamentoFiltro.TipoRendimento).Sum(s => s.Valor);
            dto.RendimentoMesAtual = rendimentoInMes - rendimentoOutMes;

            var inicioMesAtual = new DateOnly(hoje.Year, hoje.Month, 1);
            var saldoInicioMes = regsOrdenados.Where(r => r.Data < inicioMesAtual).OrderByDescending(r => r.Data).FirstOrDefault()?.SaldoFinal ?? c.SaldoInicial;
            dto.RentabilidadeMesAtualPercentual = saldoInicioMes > 0 ? Math.Round(dto.RendimentoMesAtual.Value / saldoInicioMes * 100, 2) : null;

            var inicioAnoAtual = new DateOnly(hoje.Year, 1, 1);
            var saldoInicioAno = regsOrdenados.Where(r => r.Data < inicioAnoAtual).OrderByDescending(r => r.Data).FirstOrDefault()?.SaldoFinal ?? c.SaldoInicial;
            var rendimentoAnoAtual = regsOrdenados.Where(r => r.Data.Year == hoje.Year)
                .Sum(r => r.Entradas.Where(e => e.TipoCusto == LancamentoFiltro.TipoRendimento).Sum(e => e.Valor)
                    - r.Saidas.Where(s => s.TipoCusto == LancamentoFiltro.TipoRendimento).Sum(s => s.Valor));
            dto.RentabilidadeAnoAtualPercentual = saldoInicioAno > 0 ? Math.Round(rendimentoAnoAtual / saldoInicioAno * 100, 2) : null;

            var datasComRendimento = regsOrdenados
                .Where(r => r.Entradas.Any(e => e.TipoCusto == LancamentoFiltro.TipoRendimento) || r.Saidas.Any(s => s.TipoCusto == LancamentoFiltro.TipoRendimento))
                .Select(r => r.Data)
                .ToList();
            dto.UltimaAtualizacaoRendimento = datasComRendimento.Count > 0 ? datasComRendimento.Max() : null;

            // ── Bloco 5, item 5: "no seu ritmo de aporte você chega lá em <mês/ano>" — ritmo é a
            // média do aporte líquido (aportes - resgates) dos 3 meses anteriores ao atual
            // (meses completos, não o mês em andamento, que ainda não terminou). ──
            var mediaAporte3Meses = Enumerable.Range(1, 3)
                .Select(i => inicioMesAtual.AddMonths(-i))
                .Select(mesRef =>
                {
                    var regsDoMesRef = regsOrdenados.Where(r => r.Data.Year == mesRef.Year && r.Data.Month == mesRef.Month);
                    var inRef = regsDoMesRef.SelectMany(r => r.Entradas).Where(e => e.TipoCusto == LancamentoFiltro.TipoTransferencia).Sum(e => e.Valor);
                    var outRef = regsDoMesRef.SelectMany(r => r.Saidas).Where(s => s.TipoCusto == LancamentoFiltro.TipoTransferencia).Sum(s => s.Valor);
                    return inRef - outRef;
                })
                .Average();

            var metasVinculadas = metasDoCliente.Where(m => m.ContaInvestimentoId == c.Id).ToList();
            dto.MetasVinculadas = metasVinculadas
                .Select(m => new MetaVinculadaDto
                {
                    Id = m.Id, Ano = m.Ano, Sonho = m.Sonho, ValorSonho = m.ValorSonho,
                    PrevisaoAtingimentoMesAno = CalcularPrevisaoAtingimento(m.ValorSonho, saldoAtual, mediaAporte3Meses, inicioMesAtual),
                })
                .ToList();

            var somaMetas = metasVinculadas.Sum(m => m.ValorSonho);
            dto.ProgressoCombinadoPercentual = somaMetas > 0
                ? Math.Round(Math.Min(1m, saldoAtual / somaMetas) * 100, 1)
                : null;
        }

        if (c.Tipo == "CartaoCredito")
        {
            dto.Limite = c.Limite;
            dto.DiaFechamento = c.DiaFechamento;
            dto.DiaVencimento = c.DiaVencimento;
            // SaldoAtual do cartão é negativo (dívida) ou zero — SaldoDevedor é a mesma coisa em
            // valor absoluto, mais direto de exibir num card. Se por algum motivo estiver positivo
            // (crédito a favor do cliente, ex. fatura paga a mais), a dívida é zero.
            dto.SaldoDevedor = Math.Max(0m, -saldoAtual);
            dto.LimiteDisponivel = c.Limite.HasValue ? c.Limite.Value - dto.SaldoDevedor.Value : null;
            // Fallback pro ledger quando o chamador não calculou a soma por fatura (ver
            // ObterFaturasEmAbertoPorContaAsync) — nunca pior que o comportamento de antes.
            dto.FaturasEmAberto = faturasEmAberto ?? dto.SaldoDevedor;

            // "Este mês" do cartão: compra é sempre saída que não é pagamento de fatura; pagamento
            // é a entrada sintética criada ao vincular/pagar a fatura (ver FaturaCartaoService).
            dto.ComprasMes = regsDoMes.SelectMany(r => r.Saidas)
                .Where(s => s.TipoCusto != LancamentoFiltro.TipoPagamentoFatura).Sum(s => s.Valor);
            dto.PagamentosMes = regsDoMes.SelectMany(r => r.Entradas)
                .Where(e => e.TipoCusto == LancamentoFiltro.TipoPagamentoFatura).Sum(e => e.Valor);
        }

        return dto;
    }
}
