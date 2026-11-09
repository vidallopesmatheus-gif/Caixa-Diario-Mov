using CaixaDiario.API.DTOs.FaturasCartao;
using CaixaDiario.API.DTOs.Metas;
using CaixaDiario.API.DTOs.Registros;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class MetaService : IMetaService
{
    private readonly IMetaRepository _metaRepository;
    private readonly IMetaProgressoService _metaProgressoService;
    private readonly IContaBancariaRepository _contaBancariaRepository;
    private readonly IRegistroRepository _registroRepository;
    private readonly ICategoriaRepository _categoriaRepository;
    private readonly IContaProvisionadaService _contaProvisionadaService;
    private readonly IFaturaCartaoService _faturaCartaoService;
    private readonly ITransferenciaService _transferenciaService;
    private readonly IFinanciamentoRepository _financiamentoRepository;
    private readonly IParcelamentoRepository _parcelamentoRepository;
    private readonly IParcelamentoDeteccaoService _parcelamentoDeteccaoService;
    private readonly IFaturaRepository _faturaRepository;
    private readonly TimeProvider _timeProvider;

    // "Já lancei essa compra": janela dos lançamentos oferecidos pra vincular.
    private const int DiasCandidatosVinculo = 90;

    public MetaService(
        IMetaRepository metaRepository, IMetaProgressoService metaProgressoService,
        IContaBancariaRepository contaBancariaRepository, IRegistroRepository registroRepository,
        ICategoriaRepository categoriaRepository, IContaProvisionadaService contaProvisionadaService,
        IFaturaCartaoService faturaCartaoService, ITransferenciaService transferenciaService,
        IFinanciamentoRepository financiamentoRepository, IParcelamentoRepository parcelamentoRepository,
        IParcelamentoDeteccaoService parcelamentoDeteccaoService, IFaturaRepository faturaRepository,
        TimeProvider? timeProvider = null)
    {
        _metaRepository = metaRepository;
        _metaProgressoService = metaProgressoService;
        _contaBancariaRepository = contaBancariaRepository;
        _registroRepository = registroRepository;
        _categoriaRepository = categoriaRepository;
        _contaProvisionadaService = contaProvisionadaService;
        _faturaCartaoService = faturaCartaoService;
        _transferenciaService = transferenciaService;
        _financiamentoRepository = financiamentoRepository;
        _parcelamentoRepository = parcelamentoRepository;
        _parcelamentoDeteccaoService = parcelamentoDeteccaoService;
        _faturaRepository = faturaRepository;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    // Item 5 (Money Vision): status do contrato de financiamento vinculado, quando existir — ver
    // MetaAnualDto.FinanciamentoStatus.
    private async Task<string?> ObterFinanciamentoStatusAsync(MetaAnual m) =>
        m.FinanciamentoId.HasValue ? (await _financiamentoRepository.ObterPorIdAsync(m.FinanciamentoId.Value))?.Status : null;

    public async Task<MetaAnualDto> ObterMetaAsync(Guid clienteId, int ano, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        // Só a meta "simples" (Meta de Faturamento Mensal) ainda é 1-por-ano — objetivos (modo
        // "metodo") são vários por cliente e são buscados/editados por Id via ListarMetasAsync.
        var meta = await _metaRepository.ObterMetaSimplesPorClienteEAnoAsync(clienteId, ano)
            ?? throw new ApiException(404, CodigoRetorno.META_NAO_ENCONTRADA, "Meta não encontrada.");

        // Se a meta estiver vinculada a uma conta de investimento, TotalInvestido passa a
        // refletir o saldo real da conta em vez do valor gravado manualmente (não persiste).
        var metas = new List<MetaAnual> { meta };
        await _metaProgressoService.AplicarSaldoDeContasVinculadasAsync(clienteId, metas);

        return MapToDto(meta, await ObterFinanciamentoStatusAsync(meta));
    }

    public async Task<List<MetaAnualDto>> ListarMetasAsync(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        var metas = await _metaRepository.ListarPorClienteAsync(clienteId);
        await _metaProgressoService.AplicarSaldoDeContasVinculadasAsync(clienteId, metas);
        var vinculos = metas.Any(m => m.Status == "Conquistada") ? await CarregarVinculosAsync(clienteId) : null;
        var resultado = new List<MetaAnualDto>();
        foreach (var m in metas.OrderByDescending(m => m.Ano))
        {
            var dto = MapToDto(m, await ObterFinanciamentoStatusAsync(m));
            if (vinculos != null && m.Status == "Conquistada") AplicarVinculos(dto, m, vinculos);
            resultado.Add(dto);
        }
        return resultado;
    }

    // Tudo que o andamento das metas conquistadas precisa, carregado uma vez por cliente.
    internal sealed record VinculosDoCliente(
        List<Parcelamento> Parcelamentos, List<RegistroDiario> Registros, IReadOnlyDictionary<Guid, string> BancoPorFaturaId);

    private async Task<VinculosDoCliente> CarregarVinculosAsync(Guid clienteId)
    {
        var parcelamentos = await _parcelamentoRepository.ListarPorClienteAsync(clienteId);
        var registros = await _registroRepository.ListarPorClienteAsync(clienteId);
        return new VinculosDoCliente(parcelamentos, registros, await ObterBancoPorFaturaIdAsync(clienteId));
    }

    private async Task<Dictionary<Guid, string>> ObterBancoPorFaturaIdAsync(Guid clienteId)
    {
        var resultado = new Dictionary<Guid, string>();
        foreach (var cartao in (await _contaBancariaRepository.ListarPorClienteAsync(clienteId)).Where(c => c.Tipo == "CartaoCredito"))
            foreach (var fatura in await _faturaRepository.ListarPorContaAsync(cartao.Id))
                resultado[fatura.Id] = fatura.Banco;
        return resultado;
    }

    private static void AplicarVinculos(MetaAnualDto dto, MetaAnual meta, VinculosDoCliente vinculos)
    {
        dto.ParcelamentoIds = vinculos.Parcelamentos.Where(p => p.MetaId == meta.Id).Select(p => p.Id).ToList();
        dto.LancamentoIds = vinculos.Registros.SelectMany(r => r.Saidas)
            .Where(s => s.MetaVinculadaId == meta.Id).Select(s => s.Id).ToList();
        dto.Andamento = CalcularAndamento(meta, vinculos);
    }

    // "Pago R$ 409,16 de R$ 4.091,60" — parcela paga é a que NÃO está mais prevista em Contas a
    // Pagar (a fatura real já trouxe), não a contagem de saídas: parcelamento descoberto na parcela
    // N > 1 nunca teve as parcelas 1..N-1 lançadas no app, mas elas já foram pagas de verdade.
    internal static AndamentoConquistaDto? CalcularAndamento(MetaAnual meta, VinculosDoCliente vinculos)
    {
        var parcelamentos = vinculos.Parcelamentos.Where(p => p.MetaId == meta.Id).ToList();
        var idsParcelamentos = parcelamentos.Select(p => p.Id).ToHashSet();
        var saidas = vinculos.Registros.Where(r => !r.Excluido).SelectMany(r => r.Saidas).ToList();
        var previstas = vinculos.Registros.Where(r => !r.Excluido).SelectMany(r => r.ContasPagar).ToList();

        var avulsas = saidas.Where(s => (s.MetaVinculadaId == meta.Id || s.OrigemMetaId == meta.Id)
            && !(s.ParcelamentoId.HasValue && idsParcelamentos.Contains(s.ParcelamentoId.Value))).ToList();
        var boletos = previstas.Where(c => c.OrigemMetaId == meta.Id && !c.ParcelamentoId.HasValue).ToList();
        if (parcelamentos.Count == 0 && avulsas.Count == 0 && boletos.Count == 0) return null;

        var resumos = new List<ParcelamentoDaMetaDto>();
        var valorPago = 0m;
        foreach (var p in parcelamentos.OrderBy(p => p.DataPrimeira))
        {
            var futuras = previstas.Where(c => c.ParcelamentoId == p.Id && !c.Pago).ToList();
            var banco = saidas.Where(s => s.ParcelamentoId == p.Id && s.FaturaId.HasValue)
                .Select(s => vinculos.BancoPorFaturaId.GetValueOrDefault(s.FaturaId!.Value))
                .FirstOrDefault(b => !string.IsNullOrWhiteSpace(b));
            valorPago += p.ValorTotal - futuras.Sum(c => c.Valor);
            resumos.Add(new ParcelamentoDaMetaDto
            {
                Id = p.Id, Descricao = p.DescricaoBase, Banco = banco,
                ParcelasPagas = p.TotalParcelas - futuras.Count, TotalParcelas = p.TotalParcelas,
                ValorParcela = p.ValorParcela, ValorTotal = p.ValorTotal,
                DataUltimaParcela = p.DataPrimeira.AddMonths(p.TotalParcelas - 1),
            });
        }

        valorPago += avulsas.Sum(s => s.Valor) + boletos.Where(c => c.Pago).Sum(c => c.ValorRealizado ?? c.Valor);
        return new AndamentoConquistaDto
        {
            ValorPago = valorPago,
            ValorTotal = parcelamentos.Sum(p => p.ValorTotal) + avulsas.Sum(s => s.Valor) + boletos.Sum(c => c.Valor),
            Parcelamentos = resumos,
            DataTermino = resumos.Count > 0 ? resumos.Max(r => r.DataUltimaParcela) : null,
            ValorMensal = resumos.Where(r => r.ParcelasPagas < r.TotalParcelas).Sum(r => r.ValorParcela),
        };
    }

    // "Conquistei → Já lancei essa compra": parcelamentos detectados e saídas dos últimos 90 dias
    // de todas as contas — só o que ainda não pertence a outra meta.
    public async Task<CandidatosVinculoMetaDto> ListarCandidatosVinculoAsync(Guid metaId, Guid usuarioLogadoId, string perfil)
    {
        var meta = await ObterMetaComAcessoAsync(metaId, usuarioLogadoId, perfil);
        var contas = (await _contaBancariaRepository.ListarPorClienteAsync(meta.ClienteId)).ToDictionary(c => c.Id);
        var registros = await _registroRepository.ListarPorClienteAsync(meta.ClienteId);
        var bancoPorFaturaId = await ObterBancoPorFaturaIdAsync(meta.ClienteId);
        var previstas = registros.SelectMany(r => r.ContasPagar).Where(c => c.ParcelamentoId.HasValue && !c.Pago).ToList();

        var parcelamentos = (await _parcelamentoRepository.ListarPorClienteAsync(meta.ClienteId))
            .Where(p => p.MetaId == null || p.MetaId == meta.Id)
            .OrderByDescending(p => p.DataPrimeira)
            .Select(p => new ParcelamentoCandidatoDto
            {
                Id = p.Id, Descricao = p.DescricaoBase, ContaNome = contas.GetValueOrDefault(p.ContaCartaoId)?.Nome ?? string.Empty,
                Banco = registros.SelectMany(r => r.Saidas).Where(s => s.ParcelamentoId == p.Id && s.FaturaId.HasValue)
                    .Select(s => bancoPorFaturaId.GetValueOrDefault(s.FaturaId!.Value)).FirstOrDefault(b => !string.IsNullOrWhiteSpace(b)),
                ValorParcela = p.ValorParcela, TotalParcelas = p.TotalParcelas, ValorTotal = p.ValorTotal,
                ParcelasPagas = p.TotalParcelas - previstas.Count(c => c.ParcelamentoId == p.Id), MetaId = p.MetaId,
            })
            .ToList();

        var hoje = DataLocalHelper.Hoje(_timeProvider.GetUtcNow().UtcDateTime);
        var inicio = hoje.AddDays(-DiasCandidatosVinculo);
        var lancamentos = registros
            .Where(r => r.ContaBancariaId.HasValue && contas.ContainsKey(r.ContaBancariaId.Value) && r.Data >= inicio && r.Data <= hoje)
            .SelectMany(r => r.Saidas
                .Where(s => s.TipoCusto is not (LancamentoFiltro.TipoTransferencia or LancamentoFiltro.TipoPagamentoFatura)
                    && !s.TransferenciaId.HasValue && !s.PagamentoFaturaId.HasValue && !s.OrigemMetaId.HasValue
                    && (s.MetaVinculadaId == null || s.MetaVinculadaId == meta.Id))
                .Select(s => new LancamentoCandidatoDto
                {
                    Id = s.Id, ContaBancariaId = r.ContaBancariaId!.Value, ContaNome = contas[r.ContaBancariaId.Value].Nome,
                    ContaEhCartao = contas[r.ContaBancariaId.Value].Tipo == "CartaoCredito",
                    Banco = s.FaturaId.HasValue ? bancoPorFaturaId.GetValueOrDefault(s.FaturaId.Value) : null,
                    Data = r.Data, Descricao = s.Descricao, Valor = s.Valor, Categoria = s.Categoria,
                    ParcelamentoId = s.ParcelamentoId, NumeroParcela = s.NumeroParcela, TotalParcelas = s.TotalParcelas,
                    MetaVinculadaId = s.MetaVinculadaId,
                }))
            .OrderByDescending(l => l.Data)
            .ToList();

        return new CandidatosVinculoMetaDto { Parcelamentos = parcelamentos, Lancamentos = lancamentos };
    }

    private async Task<MetaAnual> ObterMetaComAcessoAsync(Guid metaId, Guid usuarioLogadoId, string perfil)
    {
        var meta = await _metaRepository.ObterPorIdAsync(metaId)
            ?? throw new ApiException(404, CodigoRetorno.META_NAO_ENCONTRADA, "Meta não encontrada.");
        if (perfil == "cliente" && usuarioLogadoId != meta.ClienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
        return meta;
    }

    public async Task<MetaAnualDto> SalvarMetaAsync(SalvarMetaAnualDto dto, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != dto.ClienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        if (dto.ModoMeta == "metodo" && dto.DataAlvo == null)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Informe a data planejada do objetivo.");

        // "simples" (Meta de Faturamento Mensal) continua 1-por-ano, identificada por (Cliente,
        // Ano). "metodo" (objetivo) não tem mais essa identidade — cada um é uma linha própria,
        // localizada pelo Id explícito; sem Id, é sempre um objetivo novo.
        MetaAnual? existente;
        if (dto.ModoMeta == "simples")
        {
            existente = await _metaRepository.ObterMetaSimplesPorClienteEAnoAsync(dto.ClienteId, dto.Ano);
        }
        else if (dto.Id.HasValue)
        {
            existente = await _metaRepository.ObterPorIdAsync(dto.Id.Value)
                ?? throw new ApiException(404, CodigoRetorno.META_NAO_ENCONTRADA, "Objetivo não encontrado.");
            if (existente.ClienteId != dto.ClienteId)
                throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
        }
        else
        {
            existente = null;
        }

        if (existente != null)
        {
            existente.MetaReceita = dto.MetaReceita;
            existente.MetaLucro = dto.MetaLucro;
            existente.MesInicio = dto.MesInicio;
            existente.PeriodoMeses = dto.PeriodoMeses;
            existente.Sonho = dto.Sonho;
            existente.ModoMeta = dto.ModoMeta;
            existente.ValorSonho = dto.ValorSonho;
            existente.PrazoAnos = dto.PrazoAnos;
            existente.TaxaRetorno = dto.TaxaRetorno;
            existente.TotalInvestido = dto.TotalInvestido;
            existente.MargemPJ = dto.MargemPJ;
            existente.IconeSonho = dto.IconeSonho;
            existente.DataAlvo = dto.DataAlvo;
            existente.AtualizadoEm = DateTime.UtcNow;
            var salvo = await _metaRepository.SalvarAsync(existente);
            return MapToDto(salvo, await ObterFinanciamentoStatusAsync(salvo));
        }

        var nova = new MetaAnual
        {
            Id = Guid.NewGuid(),
            ClienteId = dto.ClienteId,
            Ano = dto.Ano,
            MetaReceita = dto.MetaReceita,
            MetaLucro = dto.MetaLucro,
            MesInicio = dto.MesInicio,
            PeriodoMeses = dto.PeriodoMeses,
            Sonho = dto.Sonho,
            ModoMeta = dto.ModoMeta,
            ValorSonho = dto.ValorSonho,
            PrazoAnos = dto.PrazoAnos,
            TaxaRetorno = dto.TaxaRetorno,
            TotalInvestido = dto.TotalInvestido,
            MargemPJ = dto.MargemPJ,
            IconeSonho = dto.IconeSonho,
            DataAlvo = dto.DataAlvo,
            CriadoEm = DateTime.UtcNow,
            AtualizadoEm = DateTime.UtcNow
        };
        return MapToDto(await _metaRepository.SalvarAsync(nova));
    }

    public async Task ExcluirMetaAsync(Guid id, Guid usuarioLogadoId, string perfil)
    {
        var meta = await _metaRepository.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.META_NAO_ENCONTRADA, "Meta não encontrada.");
        if (perfil == "cliente" && usuarioLogadoId != meta.ClienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        await _metaRepository.RemoverAsync(meta);
    }

    // Fase 5: "🏆 Conquistei" — à vista (1 saída), cartão (N parcelas de fatura) e boleto (N contas
    // a pagar). "Financiado" (5G) não é aceito AQUI — não dá pra montar um contrato de
    // financiamento (parcelas/taxa/sistema) a partir só do ConquistarMetaDto; esse caminho passa
    // pela tela de Financiamentos (CriarFinanciamentoDto.OrigemMetaId), que marca a meta conquistada
    // como efeito colateral quando o contrato é criado — ver FinanciamentoService.CriarAsync.
    public async Task<MetaAnualDto> ConquistarAsync(Guid metaId, Guid usuarioLogadoId, string perfil, ConquistarMetaDto dto)
    {
        var meta = await ObterMetaComAcessoAsync(metaId, usuarioLogadoId, perfil);
        if (meta.Status != "EmAndamento")
            throw new ApiException(400, CodigoRetorno.META_JA_CONQUISTADA, "Esta meta já foi conquistada ou cancelada.");
        if (dto.Modo is not ("LancarAgora" or "JaLancado"))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Modo inválido.", "modo");
        if (dto.FormaPagamento is not ("AVista" or "Cartao" or "Boleto" or "Financiado"))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Forma de pagamento inválida.", "formaPagamento");
        if (dto.FormaPagamento is "Financiado")
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS,
                "Pagamento \"Financiado\" é feito pela tela de Financiamentos — cadastre o contrato " +
                "lá vinculado a este objetivo.", "formaPagamento");

        if (dto.Modo == "JaLancado")
        {
            await VincularCompraExistenteAsync(meta, dto);
        }
        else
        {
            await LancarCompraAsync(meta, dto, usuarioLogadoId, perfil);
            meta.ContaPagamentoId = dto.ContaPagamentoId;
        }

        meta.Status = "Conquistada";
        meta.DataConquista = dto.Data;
        meta.FormaPagamento = dto.FormaPagamento;
        meta.Parcelas ??= dto.Parcelas;
        meta.DataPrimeiraParcela = dto.DataPrimeiraParcela;
        meta.Categoria = string.IsNullOrWhiteSpace(dto.Categoria) ? null : dto.Categoria;
        meta.Observacao = dto.Observacao;
        meta.AtualizadoEm = DateTime.UtcNow;

        var metaSalva = await _metaRepository.SalvarAsync(meta);
        var resultado = MapToDto(metaSalva, await ObterFinanciamentoStatusAsync(metaSalva));
        AplicarVinculos(resultado, metaSalva, await CarregarVinculosAsync(metaSalva.ClienteId));
        return resultado;
    }

    // "Já lancei essa compra / ela vai vir na fatura": a compra já existe no app (importada ou
    // manual) — só liga os parcelamentos/lançamentos escolhidos à meta. Nunca cria lançamento; o
    // único efeito colateral é criar o Parcelamento quando o usuário diz "é a parcela N de M" de um
    // lançamento que a detecção ainda não reconheceu (e aí as futuras viram previstas, igual import).
    private async Task VincularCompraExistenteAsync(MetaAnual meta, ConquistarMetaDto dto)
    {
        if (dto.ParcelamentoIds.Count == 0 && dto.Lancamentos.Count == 0)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS,
                "Escolha ao menos um parcelamento ou lançamento da compra.", "parcelamentoIds");

        var vinculados = new Dictionary<Guid, Parcelamento>();
        Guid? primeiraConta = null;

        async Task VincularParcelamentoAsync(Guid parcelamentoId)
        {
            if (vinculados.ContainsKey(parcelamentoId)) return;
            var parcelamento = await _parcelamentoRepository.ObterPorIdAsync(parcelamentoId)
                ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Parcelamento não encontrado.");
            if (parcelamento.ClienteId != meta.ClienteId)
                throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Parcelamento não pertence a este cliente.");
            if (parcelamento.MetaId.HasValue && parcelamento.MetaId != meta.Id)
                throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS,
                    $"O parcelamento \"{parcelamento.DescricaoBase}\" já está vinculado a outro objetivo.", "parcelamentoIds");
            parcelamento.MetaId = meta.Id;
            await _parcelamentoRepository.AtualizarAsync(parcelamento);
            vinculados[parcelamento.Id] = parcelamento;
            primeiraConta ??= parcelamento.ContaCartaoId;
        }

        foreach (var id in dto.ParcelamentoIds) await VincularParcelamentoAsync(id);

        var valorAvulsos = 0m;
        foreach (var item in dto.Lancamentos)
        {
            var conta = await _contaBancariaRepository.ObterPorIdAsync(item.ContaBancariaId)
                ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
            if (conta.ClienteId != meta.ClienteId)
                throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Conta bancária não pertence a este cliente.");
            var registro = await _registroRepository.ObterPorContaEDataAsync(conta.Id, item.Data)
                ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Lançamento não encontrado.");
            var saida = registro.Saidas.FirstOrDefault(s => s.Id == item.LancamentoId)
                ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Lançamento não encontrado.");
            if (saida.MetaVinculadaId.HasValue && saida.MetaVinculadaId != meta.Id)
                throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS,
                    $"\"{saida.Descricao}\" já está vinculado a outro objetivo.", "lancamentos");
            primeiraConta ??= conta.Id;

            if (!saida.ParcelamentoId.HasValue && item.NumeroParcela.HasValue && item.TotalParcelas.HasValue)
                await CriarParcelamentoAPartirDoLancamentoAsync(conta, registro, saida, item.NumeroParcela.Value, item.TotalParcelas.Value);

            // Parcela de um parcelamento: o vínculo é o parcelamento inteiro (as outras parcelas,
            // reais ou previstas, também são desta compra).
            if (saida.ParcelamentoId.HasValue && await _parcelamentoRepository.ObterPorIdAsync(saida.ParcelamentoId.Value) != null)
            {
                await VincularParcelamentoAsync(saida.ParcelamentoId.Value);
                continue;
            }

            saida.MetaVinculadaId = meta.Id;
            registro.Saidas = new List<ItemFinanceiroSaida>(registro.Saidas); // força o EF a detectar a mudança (jsonb)
            registro.SalvoEm = DateTime.UtcNow;
            await _registroRepository.AtualizarAsync(registro);
            valorAvulsos += saida.Valor;
        }

        meta.ValorPago = vinculados.Values.Sum(p => p.ValorTotal) + valorAvulsos;
        meta.Parcelas = vinculados.Count > 0 ? vinculados.Values.Max(p => p.TotalParcelas) : dto.Parcelas;
        meta.ContaPagamentoId = dto.ContaPagamentoId != Guid.Empty ? dto.ContaPagamentoId : primeiraConta;
    }

    private async Task CriarParcelamentoAPartirDoLancamentoAsync(
        ContaBancaria conta, RegistroDiario registro, ItemFinanceiroSaida saida, int numero, int total)
    {
        if (conta.Tipo != "CartaoCredito")
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS,
                "Só dá pra informar \"parcela N de M\" em lançamento de cartão de crédito.", "lancamentos");
        if (total is < 2 or > 48 || numero < 1 || numero > total)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Parcela inválida — use \"N de M\", com M entre 2 e 48.", "lancamentos");

        // Mesma base da varredura retroativa: vencimento da fatura real da compra, quando existir.
        var vencimento = registro.Data;
        if (saida.FaturaId.HasValue && await _faturaRepository.ObterPorIdAsync(saida.FaturaId.Value) is { } fatura)
            vencimento = fatura.DataVencimento;
        var comerciante = ParcelaDescricaoParser.TentarExtrair(saida.Descricao)?.Comerciante ?? saida.Descricao.Trim();

        var parcelamento = await _parcelamentoDeteccaoService.ProcessarParcelaDetectadaAsync(
            conta, vencimento, comerciante, saida.Categoria, numero, total, saida.Valor);

        saida.ParcelamentoId = parcelamento.Id;
        saida.NumeroParcela = numero;
        saida.TotalParcelas = total;
        registro.Saidas = new List<ItemFinanceiroSaida>(registro.Saidas);
        registro.SalvoEm = DateTime.UtcNow;
        await _registroRepository.AtualizarAsync(registro);
    }

    // "Ainda não está no app — lançar agora": o app cria a compra (à vista, cartão ou boleto).
    private async Task LancarCompraAsync(MetaAnual meta, ConquistarMetaDto dto, Guid usuarioLogadoId, string perfil)
    {
        if (dto.ValorPago <= 0 && !(dto.FormaPagamento == "Cartao" && dto.ValorParcela > 0))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Informe o valor pago.", "valorPago");
        if (string.IsNullOrWhiteSpace(dto.Categoria))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Informe a categoria da compra.", "categoria");

        meta.ValorPago = dto.ValorPago;

        var contaPagamento = await _contaBancariaRepository.ObterPorIdAsync(dto.ContaPagamentoId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        if (contaPagamento.ClienteId != meta.ClienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Conta bancária não pertence a este cliente.");
        if (!contaPagamento.Ativa)
            throw new ApiException(400, CodigoRetorno.CONTA_INATIVA, "A conta de pagamento está inativa.");

        var descricao = $"🏆 {meta.Sonho ?? "Meta"}";

        if (dto.FormaPagamento == "AVista")
        {
            var tipoCusto = (await _categoriaRepository.ListarTodasPorClienteAsync(meta.ClienteId))
                .ToDictionary(c => c.Nome, c => c.Tipo)
                .GetValueOrDefault(dto.Categoria);

            var (registro, novo) = await RegistroDiaHelper.ResolverOuCriarAsync(_registroRepository, contaPagamento, dto.Data);
            registro.Saidas = new List<ItemFinanceiroSaida>(registro.Saidas)
            {
                new()
                {
                    Id = Guid.NewGuid(), Descricao = descricao, Valor = dto.ValorPago,
                    Categoria = dto.Categoria, TipoCusto = tipoCusto, OrigemMetaId = meta.Id,
                },
            };
            registro.SaldoFinal -= dto.ValorPago;
            registro.SalvoEm = DateTime.UtcNow;
            await RegistroDiaHelper.PersistirAsync(_registroRepository, registro, novo);

            var registrosDaConta = await _registroRepository.ListarPorContaAsync(contaPagamento.Id);
            await RegistroService.RecalcularDiasSeguintesAsync(
                _registroRepository, contaPagamento.Id, dto.Data, registro.SaldoFinal, registrosDaConta);

            // Fase 5.3: meta vinculada a investimento — resgata o valor pago de lá pra conta de
            // pagamento (fora do DRE). Só faz sentido à vista: boleto/cartão ainda não debitaram
            // nada da conta de pagamento na hora da compra, resgatar agora seria prematuro.
            //
            // Item 2 (Money Vision): o resgate nunca pode passar do saldo REAL da conta de
            // investimento (TransferenciaService não valida isso — deixaria a reserva negativa).
            // Quando o saldo real é menor que o valor pago, resgata só o disponível; a diferença
            // sai naturalmente do saldo que a conta de pagamento já tinha antes da saída acima (é
            // o "split reserva + conta de pagamento" — nunca um erro bloqueando a conquista).
            if (meta.ContaInvestimentoId.HasValue && dto.ResgatarDoInvestimento)
            {
                var contaInvestimento = await _contaBancariaRepository.ObterPorIdAsync(meta.ContaInvestimentoId.Value);
                if (contaInvestimento != null)
                {
                    var registrosInvestimento = await _registroRepository.ListarPorContaAsync(contaInvestimento.Id);
                    var saldoRealInvestimento = ContaBancariaService.ObterSaldoAtual(contaInvestimento, registrosInvestimento);
                    var valorResgate = Math.Min(dto.ValorPago, Math.Max(0m, saldoRealInvestimento));
                    if (valorResgate > 0)
                    {
                        var resgate = await _transferenciaService.CriarAsync(
                            meta.ClienteId, meta.ContaInvestimentoId.Value, contaPagamento.Id, dto.Data, valorResgate,
                            $"Resgate para {descricao}", usuarioLogadoId, perfil);
                        meta.TransferenciaResgateId = resgate.Id;
                    }
                }
            }
        }
        else if (dto.FormaPagamento == "Cartao")
        {
            if (contaPagamento.Tipo != "CartaoCredito")
                throw new ApiException(400, CodigoRetorno.CONTA_NAO_E_CARTAO,
                    "Escolha uma conta de cartão de crédito para pagar no cartão.", "contaPagamentoId");
            var parcelas = dto.Parcelas ?? 0;
            if (parcelas < 2)
                throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS,
                    "Pagamento no cartão precisa de ao menos 2 parcelas — pra 1x, use a forma À Vista.", "parcelas");
            if (!dto.DataPrimeiraParcela.HasValue)
                throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Informe a data da 1ª parcela.", "dataPrimeiraParcela");

            // Item 3 (Money Vision): a 1ª parcela vence no vencimento do cartão escolhido
            // (dto.DataPrimeiraParcela, calculado no front). Só ela (e as já vencidas) vira
            // lançamento, na fatura do banco escolhido; as futuras ficam previstas em Contas a Pagar.
            await _faturaCartaoService.LancarComprasParceladaAsync(contaPagamento.Id, usuarioLogadoId, perfil,
                new LancarCompraParceladaDto
                {
                    Descricao = descricao, ValorTotal = dto.ValorPago, Categoria = dto.Categoria,
                    QuantidadeParcelas = parcelas, Data = dto.Data, DataPrimeiraParcela = dto.DataPrimeiraParcela.Value,
                    Banco = dto.Banco, ValorParcela = dto.ValorParcela,
                }, meta.Id);
            meta.ValorPago = FaturaCartaoService.DividirParcelas(dto.ValorParcela, dto.ValorPago, parcelas).Sum();
        }
        else // Boleto
        {
            var parcelas = dto.Parcelas ?? 1;
            if (parcelas is < 1 or > 48)
                throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Parcelas deve ser entre 1 e 48.", "parcelas");
            if (!dto.DataPrimeiraParcela.HasValue)
                throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Informe a data da 1ª parcela.", "dataPrimeiraParcela");

            // Última parcela absorve o resto do arredondamento — a soma bate exata com o valor pago.
            var valorParcela = Math.Round(dto.ValorPago / parcelas, 2);
            var somaAnterior = 0m;
            for (var i = 0; i < parcelas; i++)
            {
                var ultima = i == parcelas - 1;
                var valor = ultima ? dto.ValorPago - somaAnterior : valorParcela;
                somaAnterior += valor;
                var descParcela = parcelas > 1 ? $"{descricao} ({i + 1}/{parcelas})" : descricao;

                await _contaProvisionadaService.CriarAsync(new CriarContaProvisionadaDto
                {
                    ClienteId = meta.ClienteId, ContaBancariaId = contaPagamento.Id, Tipo = "Pagar",
                    Descricao = descParcela, Valor = valor,
                    DataVencimento = dto.DataPrimeiraParcela.Value.AddMonths(i), Categoria = dto.Categoria,
                }, usuarioLogadoId, perfil, meta.Id);
            }
        }
    }

    // Fase 5.5: "Desfazer conquista" — remove só o que ainda não foi pago/vinculado ao extrato
    // (destruir dinheiro que já se moveu de verdade seria pior que deixar a meta errada). O que
    // não pode ser removido entra em "Mantidos" pro frontend avisar o usuário.
    public async Task<DesfazerConquistaResultDto> DesfazerConquistaAsync(Guid metaId, Guid usuarioLogadoId, string perfil)
    {
        var meta = await _metaRepository.ObterPorIdAsync(metaId)
            ?? throw new ApiException(404, CodigoRetorno.META_NAO_ENCONTRADA, "Meta não encontrada.");
        if (perfil == "cliente" && usuarioLogadoId != meta.ClienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
        if (meta.Status != "Conquistada")
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Esta meta não está conquistada.");

        var resultado = new DesfazerConquistaResultDto();

        // Financiado: o contrato não é tagueado por OrigemMetaId (é OrigemFinanciamentoId, nas
        // próprias parcelas) — "desfazer" aqui só destrava o objetivo; o contrato em si continua
        // existindo e precisa ser excluído pela tela de Financiamentos (bloqueado lá se já tiver
        // parcela paga, igual qualquer outro financiamento).
        if (meta.FormaPagamento == "Financiado" && meta.FinanciamentoId.HasValue)
            resultado.Mantidos.Add("Contrato de financiamento (cancele pela tela de Financiamentos, se quiser)");
        var contas = await _contaBancariaRepository.ListarPorClienteAsync(meta.ClienteId);
        var faturasPorConta = new Dictionary<Guid, List<FaturaCartaoDto>>();
        var faturasTocadas = new HashSet<(Guid ContaId, Guid FaturaId)>();
        var registros = await _registroRepository.ListarPorClienteAsync(meta.ClienteId);

        async Task<List<FaturaCartaoDto>> FaturasDaContaAsync(Guid contaId)
        {
            if (!faturasPorConta.TryGetValue(contaId, out var faturas))
            {
                faturas = await _faturaCartaoService.ListarFaturasAsync(contaId, usuarioLogadoId, perfil);
                faturasPorConta[contaId] = faturas;
            }
            return faturas;
        }

        foreach (var registro in registros)
        {
            if (!registro.ContaBancariaId.HasValue) continue;
            var saidasDaMeta = registro.Saidas.Where(s => s.OrigemMetaId == meta.Id).ToList();
            var pagarDaMeta = registro.ContasPagar.Where(c => c.OrigemMetaId == meta.Id).ToList();
            var saidasVinculadas = registro.Saidas.Where(s => s.MetaVinculadaId == meta.Id).ToList();
            if (saidasDaMeta.Count == 0 && pagarDaMeta.Count == 0 && saidasVinculadas.Count == 0) continue;

            var contaDoRegistro = contas.FirstOrDefault(c => c.Id == registro.ContaBancariaId.Value);
            var precisaAtualizarRegistro = false;
            var saldoMudou = false;

            if (saidasDaMeta.Count > 0)
            {
                var removiveis = new List<ItemFinanceiroSaida>();
                foreach (var saida in saidasDaMeta)
                {
                    // A importação mesclou a linha real do banco neste lançamento (ganhou FitId):
                    // agora é dinheiro confirmado pelo extrato — só deixa de ser "da meta".
                    if (saida.FitId != null)
                    {
                        saida.OrigemMetaId = null;
                        registro.Saidas = new List<ItemFinanceiroSaida>(registro.Saidas);
                        precisaAtualizarRegistro = true;
                        resultado.Mantidos.Add($"{saida.Descricao} (já veio no extrato/fatura do banco)");
                        continue;
                    }

                    // Parcela de cartão cuja fatura já foi paga: a compra em si não tem um "Pago"
                    // próprio — só existe através do status da fatura inteira (a real, por FaturaId;
                    // sem FaturaId, o grupo virtual da competência inferida).
                    if (contaDoRegistro?.Tipo == "CartaoCredito")
                    {
                        var faturas = await FaturasDaContaAsync(contaDoRegistro.Id);
                        var fatura = saida.FaturaId.HasValue
                            ? faturas.FirstOrDefault(f => f.Id == saida.FaturaId)
                            : faturas.FirstOrDefault(f => f.Id == null
                                && f.Competencia == FaturaCartaoService.ObterCompetencia(registro.Data, contaDoRegistro.DiaFechamento));
                        if (fatura?.Status == "Paga")
                        {
                            resultado.Mantidos.Add($"{saida.Descricao} (fatura {fatura.Competencia} já paga)");
                            continue;
                        }
                        if (saida.FaturaId.HasValue) faturasTocadas.Add((contaDoRegistro.Id, saida.FaturaId.Value));
                    }
                    removiveis.Add(saida);
                    resultado.Removidos.Add(saida.Descricao);
                }

                if (removiveis.Count > 0)
                {
                    var totalRemovido = removiveis.Sum(s => s.Valor);
                    registro.Saidas = registro.Saidas.Where(s => !removiveis.Contains(s)).ToList();
                    registro.SaldoFinal += totalRemovido;
                    precisaAtualizarRegistro = true;
                    saldoMudou = true;
                }
            }

            // Lançamento que já existia e só foi vinculado: é dinheiro real, nunca apaga — só solta.
            if (saidasVinculadas.Count > 0)
            {
                foreach (var saida in saidasVinculadas) saida.MetaVinculadaId = null;
                registro.Saidas = new List<ItemFinanceiroSaida>(registro.Saidas);
                precisaAtualizarRegistro = true;
            }

            if (pagarDaMeta.Count > 0)
            {
                var mantidas = pagarDaMeta.Where(c => c.Pago).ToList();
                var removiveis = pagarDaMeta.Except(mantidas).ToList();
                foreach (var m in mantidas) resultado.Mantidos.Add($"{m.Descricao} (já paga)");
                foreach (var r in removiveis) resultado.Removidos.Add(r.Descricao);

                if (removiveis.Count > 0)
                {
                    registro.ContasPagar = registro.ContasPagar.Where(c => !removiveis.Contains(c)).ToList();
                    precisaAtualizarRegistro = true;
                }
            }

            if (!precisaAtualizarRegistro) continue;
            registro.SalvoEm = DateTime.UtcNow;
            await _registroRepository.AtualizarAsync(registro);

            if (saldoMudou)
            {
                var registrosDaConta = await _registroRepository.ListarPorContaAsync(registro.ContaBancariaId.Value);
                await RegistroService.RecalcularDiasSeguintesAsync(
                    _registroRepository, registro.ContaBancariaId.Value, registro.Data, registro.SaldoFinal, registrosDaConta);
            }
        }

        // Parcelamento criado pelo "Conquistei" que ficou sem nenhuma parcela (real ou prevista)
        // some junto; um que tem parcela real (veio da fatura, ou de fatura já paga) só é desvinculado.
        foreach (var parcelamento in (await _parcelamentoRepository.ListarPorClienteAsync(meta.ClienteId)).Where(p => p.MetaId == meta.Id))
        {
            var aindaTemParcela = registros.Any(r =>
                r.Saidas.Any(s => s.ParcelamentoId == parcelamento.Id) || r.ContasPagar.Any(c => c.ParcelamentoId == parcelamento.Id));
            if (aindaTemParcela)
            {
                parcelamento.MetaId = null;
                await _parcelamentoRepository.AtualizarAsync(parcelamento);
                continue;
            }
            await _parcelamentoRepository.RemoverAsync(parcelamento);
            resultado.Removidos.Add($"Parcelamento {parcelamento.DescricaoBase}");
        }

        // Fatura que só existia por causa das parcelas removidas (sem compra nem pagamento) não
        // pode ficar órfã — a fatura real do banco, quando chegar, cria/reaproveita a dela.
        foreach (var contaId in faturasTocadas.Select(f => f.ContaId).Distinct())
        {
            var comMovimento = (await _faturaCartaoService.ListarFaturasAsync(contaId, usuarioLogadoId, perfil))
                .Where(f => f.Id.HasValue).Select(f => f.Id!.Value).ToHashSet();
            foreach (var (_, faturaId) in faturasTocadas.Where(f => f.ContaId == contaId && !comMovimento.Contains(f.FaturaId)))
            {
                var fatura = await _faturaRepository.ObterPorIdAsync(faturaId);
                if (fatura == null) continue;
                await _faturaRepository.RemoverAsync(fatura);
                resultado.Removidos.Add($"Fatura {fatura.Banco} {fatura.Competencia} (vazia)");
            }
        }

        // Transferência de resgate: só remove se NENHUM dos dois lados já reconciliou com o
        // extrato real (Provisoria ainda true nos dois) — senão seria apagar dinheiro que o banco
        // já confirmou ter de fato saído do investimento.
        if (meta.TransferenciaResgateId.HasValue && meta.ContaInvestimentoId.HasValue && meta.ContaPagamentoId.HasValue
            && meta.DataConquista.HasValue)
        {
            var regOrigem = await _registroRepository.ObterPorContaEDataAsync(meta.ContaInvestimentoId.Value, meta.DataConquista.Value);
            var itemOrigem = regOrigem?.Saidas.FirstOrDefault(s => s.TransferenciaId == meta.TransferenciaResgateId.Value);
            var regDestino = await _registroRepository.ObterPorContaEDataAsync(meta.ContaPagamentoId.Value, meta.DataConquista.Value);
            var itemDestino = regDestino?.Entradas.FirstOrDefault(e => e.TransferenciaId == meta.TransferenciaResgateId.Value);

            var aindaProvisorios = (itemOrigem?.Provisoria ?? false) && (itemDestino?.Provisoria ?? false);
            if (aindaProvisorios)
            {
                await _transferenciaService.ExcluirAsync(meta.TransferenciaResgateId.Value, usuarioLogadoId, perfil);
                resultado.Removidos.Add("Resgate do investimento");
                meta.TransferenciaResgateId = null;
            }
            else
            {
                resultado.Mantidos.Add("Resgate do investimento (já reconciliado com o extrato)");
            }
        }

        meta.Status = "EmAndamento";
        meta.DataConquista = null;
        meta.ValorPago = null;
        meta.FormaPagamento = null;
        meta.Parcelas = null;
        meta.DataPrimeiraParcela = null;
        meta.ContaPagamentoId = null;
        meta.Categoria = null;
        meta.Observacao = null;
        meta.FinanciamentoId = null;
        meta.AtualizadoEm = DateTime.UtcNow;
        await _metaRepository.SalvarAsync(meta);

        return resultado;
    }

    private static MetaAnualDto MapToDto(MetaAnual m, string? financiamentoStatus = null) => new()
    {
        Id = m.Id, ClienteId = m.ClienteId, Ano = m.Ano,
        MetaReceita = m.MetaReceita, MetaLucro = m.MetaLucro,
        MesInicio = m.MesInicio, PeriodoMeses = m.PeriodoMeses, SalvoEm = m.AtualizadoEm, CriadoEm = m.CriadoEm,
        Sonho = m.Sonho, ModoMeta = m.ModoMeta,
        ValorSonho = m.ValorSonho, PrazoAnos = m.PrazoAnos,
        TaxaRetorno = m.TaxaRetorno, TotalInvestido = m.TotalInvestido,
        MargemPJ = m.MargemPJ, IconeSonho = m.IconeSonho,
        ContaInvestimentoId = m.ContaInvestimentoId,
        DataAlvo = m.DataAlvo,
        Status = m.Status, DataConquista = m.DataConquista, ValorPago = m.ValorPago,
        FormaPagamento = m.FormaPagamento, Parcelas = m.Parcelas, DataPrimeiraParcela = m.DataPrimeiraParcela,
        ContaPagamentoId = m.ContaPagamentoId, Categoria = m.Categoria, Observacao = m.Observacao,
        TransferenciaResgateId = m.TransferenciaResgateId, FinanciamentoId = m.FinanciamentoId,
        FinanciamentoStatus = financiamentoStatus,
    };
}
