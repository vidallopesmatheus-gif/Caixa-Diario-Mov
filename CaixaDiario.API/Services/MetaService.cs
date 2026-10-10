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

    public MetaService(
        IMetaRepository metaRepository, IMetaProgressoService metaProgressoService,
        IContaBancariaRepository contaBancariaRepository, IRegistroRepository registroRepository,
        ICategoriaRepository categoriaRepository, IContaProvisionadaService contaProvisionadaService,
        IFaturaCartaoService faturaCartaoService, ITransferenciaService transferenciaService)
    {
        _metaRepository = metaRepository;
        _metaProgressoService = metaProgressoService;
        _contaBancariaRepository = contaBancariaRepository;
        _registroRepository = registroRepository;
        _categoriaRepository = categoriaRepository;
        _contaProvisionadaService = contaProvisionadaService;
        _faturaCartaoService = faturaCartaoService;
        _transferenciaService = transferenciaService;
    }

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

        return MapToDto(meta);
    }

    public async Task<List<MetaAnualDto>> ListarMetasAsync(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        var metas = await _metaRepository.ListarPorClienteAsync(clienteId);
        await _metaProgressoService.AplicarSaldoDeContasVinculadasAsync(clienteId, metas);
        return metas.OrderByDescending(m => m.Ano).Select(MapToDto).ToList();
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
            return MapToDto(await _metaRepository.SalvarAsync(existente));
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
        var meta = await _metaRepository.ObterPorIdAsync(metaId)
            ?? throw new ApiException(404, CodigoRetorno.META_NAO_ENCONTRADA, "Meta não encontrada.");
        if (perfil == "cliente" && usuarioLogadoId != meta.ClienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
        if (meta.Status != "EmAndamento")
            throw new ApiException(400, CodigoRetorno.META_JA_CONQUISTADA, "Esta meta já foi conquistada ou cancelada.");
        if (dto.ValorPago <= 0)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Informe o valor pago.", "valorPago");
        if (string.IsNullOrWhiteSpace(dto.Categoria))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Informe a categoria da compra.", "categoria");
        if (dto.FormaPagamento is not ("AVista" or "Cartao" or "Boleto" or "Financiado"))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Forma de pagamento inválida.", "formaPagamento");
        if (dto.FormaPagamento is "Financiado")
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS,
                "Pagamento \"Financiado\" é feito pela tela de Financiamentos — cadastre o contrato " +
                "lá vinculado a este objetivo.", "formaPagamento");

        var contaPagamento = await _contaBancariaRepository.ObterPorIdAsync(dto.ContaPagamentoId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        if (contaPagamento.ClienteId != meta.ClienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Conta bancária não pertence a este cliente.");
        if (!contaPagamento.Ativa)
            throw new ApiException(400, CodigoRetorno.CONTA_INATIVA, "A conta de pagamento está inativa.");

        var descricao = $"🏆 {meta.Sonho ?? "Meta"}";

        if (dto.FormaPagamento == "AVista")
        {
            var tipoCusto = (await _categoriaRepository.ListarTodasAsync())
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
            if (meta.ContaInvestimentoId.HasValue && dto.ResgatarDoInvestimento)
            {
                var resgate = await _transferenciaService.CriarAsync(
                    meta.ClienteId, meta.ContaInvestimentoId.Value, contaPagamento.Id, dto.Data, dto.ValorPago,
                    $"Resgate para {descricao}", usuarioLogadoId, perfil);
                meta.TransferenciaResgateId = resgate.Id;
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

            await _faturaCartaoService.LancarComprasParceladaAsync(contaPagamento.Id, usuarioLogadoId, perfil,
                new LancarCompraParceladaDto
                {
                    Descricao = descricao, ValorTotal = dto.ValorPago, Categoria = dto.Categoria,
                    QuantidadeParcelas = parcelas, Data = dto.Data,
                }, meta.Id);
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

        meta.Status = "Conquistada";
        meta.DataConquista = dto.Data;
        meta.ValorPago = dto.ValorPago;
        meta.FormaPagamento = dto.FormaPagamento;
        meta.Parcelas = dto.Parcelas;
        meta.DataPrimeiraParcela = dto.DataPrimeiraParcela;
        meta.ContaPagamentoId = dto.ContaPagamentoId;
        meta.Categoria = dto.Categoria;
        meta.Observacao = dto.Observacao;
        meta.AtualizadoEm = DateTime.UtcNow;

        return MapToDto(await _metaRepository.SalvarAsync(meta));
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
        var competenciasPagasPorConta = new Dictionary<Guid, HashSet<string>>();
        var registros = await _registroRepository.ListarPorClienteAsync(meta.ClienteId);

        foreach (var registro in registros)
        {
            if (!registro.ContaBancariaId.HasValue) continue;
            var saidasDaMeta = registro.Saidas.Where(s => s.OrigemMetaId == meta.Id).ToList();
            var pagarDaMeta = registro.ContasPagar.Where(c => c.OrigemMetaId == meta.Id).ToList();
            if (saidasDaMeta.Count == 0 && pagarDaMeta.Count == 0) continue;

            var contaDoRegistro = contas.FirstOrDefault(c => c.Id == registro.ContaBancariaId.Value);
            var precisaAtualizarRegistro = false;

            if (saidasDaMeta.Count > 0)
            {
                var removiveis = new List<ItemFinanceiroSaida>();
                foreach (var saida in saidasDaMeta)
                {
                    // Parcela de cartão cuja fatura (competência) já foi paga: a compra em si não
                    // tem um "Pago" próprio — só existe através do status da fatura inteira.
                    if (contaDoRegistro?.Tipo == "CartaoCredito")
                    {
                        if (!competenciasPagasPorConta.TryGetValue(contaDoRegistro.Id, out var pagas))
                        {
                            var faturas = await _faturaCartaoService.ListarFaturasAsync(contaDoRegistro.Id, usuarioLogadoId, perfil);
                            pagas = faturas.Where(f => f.Status == "Paga").Select(f => f.Competencia).ToHashSet();
                            competenciasPagasPorConta[contaDoRegistro.Id] = pagas;
                        }
                        var competencia = FaturaCartaoService.ObterCompetencia(registro.Data, contaDoRegistro.DiaFechamento);
                        if (pagas.Contains(competencia))
                        {
                            resultado.Mantidos.Add($"{saida.Descricao} (fatura {competencia} já paga)");
                            continue;
                        }
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
                }
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

            if (saidasDaMeta.Count > 0)
            {
                var registrosDaConta = await _registroRepository.ListarPorContaAsync(registro.ContaBancariaId.Value);
                await RegistroService.RecalcularDiasSeguintesAsync(
                    _registroRepository, registro.ContaBancariaId.Value, registro.Data, registro.SaldoFinal, registrosDaConta);
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

    private static MetaAnualDto MapToDto(MetaAnual m) => new()
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
    };
}
