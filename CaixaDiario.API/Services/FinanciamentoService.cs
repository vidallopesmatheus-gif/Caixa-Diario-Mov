using System.Text.Json;
using CaixaDiario.API.Data;
using CaixaDiario.API.DTOs.Financiamentos;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Services;

/// <summary>
/// Fase 6: financiamento/empréstimo/consórcio/parcelamento. As parcelas são ContaProvisionada
/// (OrigemFinanciamentoId) — a "tabela de amortização" exibida é sempre derivada delas ao vivo,
/// nunca duplicada. Baixa desdobra em até 3 lançamentos (juros/amortização/encargos); amortização
/// cai em Atividades de Financiamento (fora do resultado operacional, ver LancamentoFiltro/Blocos
/// — infraestrutura já existente, reaproveitada sem mudança), juros entra no resultado operacional.
/// </summary>
public class FinanciamentoService : IFinanciamentoService
{
    private readonly IFinanciamentoRepository _financiamentoRepo;
    private readonly IContaBancariaRepository _contaRepo;
    private readonly IRegistroRepository _registroRepo;
    private readonly ICategoriaRepository _categoriaRepo;
    private readonly IMetaRepository _metaRepo;
    private readonly IAuditService _auditService;
    private readonly AppDbContext _context;

    public FinanciamentoService(
        IFinanciamentoRepository financiamentoRepo, IContaBancariaRepository contaRepo,
        IRegistroRepository registroRepo, ICategoriaRepository categoriaRepo, IMetaRepository metaRepo,
        IAuditService auditService, AppDbContext context)
    {
        _financiamentoRepo = financiamentoRepo;
        _contaRepo = contaRepo;
        _registroRepo = registroRepo;
        _categoriaRepo = categoriaRepo;
        _metaRepo = metaRepo;
        _auditService = auditService;
        _context = context;
    }

    public async Task<FinanciamentoDto> CriarAsync(Guid clienteId, Guid usuarioLogadoId, string perfil, CriarFinanciamentoDto dto)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);
        if (dto.Tipo is not ("Financiamento" or "Emprestimo" or "Consorcio" or "Parcelamento"))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Tipo inválido.", "tipo");
        if (dto.Sistema is not ("Price" or "SAC" or "SemJuros"))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Sistema inválido.", "sistema");
        if (dto.NumeroParcelas < 1)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Número de parcelas deve ser maior que zero.", "numeroParcelas");

        var conta = await _contaRepo.ObterPorIdAsync(dto.ContaBancariaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        if (conta.ClienteId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Conta bancária não pertence a este cliente.");
        if (!conta.Ativa)
            throw new ApiException(400, CodigoRetorno.CONTA_INATIVA, "A conta deve estar ativa.");

        var valorFinanciado = dto.ValorBem - dto.ValorEntrada;
        if (valorFinanciado <= 0)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS,
                "O valor financiado (bem − entrada) deve ser maior que zero.", "valorEntrada");

        decimal taxa;
        if (dto.Sistema == "SemJuros")
            taxa = 0m;
        else if (dto.TaxaJurosMensal.HasValue)
            taxa = dto.TaxaJurosMensal.Value;
        else if (dto.Sistema == "Price" && dto.ValorParcela.HasValue)
            taxa = CalculadoraAmortizacao.ResolverTaxaImplicitaPrice(valorFinanciado, dto.ValorParcela.Value, dto.NumeroParcelas);
        else
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS,
                "Informe a taxa de juros mensal ou (sistema Price) o valor da parcela, pra calcular a taxa implícita.", "taxaJurosMensal");

        var tabela = dto.Sistema switch
        {
            "SAC" => CalculadoraAmortizacao.GerarTabelaSac(valorFinanciado, taxa, dto.NumeroParcelas, dto.DataPrimeiraParcela),
            "SemJuros" => CalculadoraAmortizacao.GerarTabelaSemJuros(valorFinanciado, dto.NumeroParcelas, dto.DataPrimeiraParcela),
            _ => CalculadoraAmortizacao.GerarTabelaPrice(valorFinanciado, taxa, dto.NumeroParcelas, dto.DataPrimeiraParcela),
        };

        // Parcela contratada maior que a calculada (seguro/tarifa embutidos) — a diferença, igual
        // em toda parcela, nunca é juros nem amortização: vira Encargos (item 6.1).
        var encargoPorParcela = dto.ValorParcela.HasValue
            ? Math.Max(0, dto.ValorParcela.Value - tabela[0].Valor)
            : 0m;

        var financiamento = new Financiamento
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Descricao = dto.Descricao, Tipo = dto.Tipo,
            ValorBem = dto.ValorBem, ValorEntrada = dto.ValorEntrada, ValorFinanciado = valorFinanciado,
            NumeroParcelas = dto.NumeroParcelas, ValorParcela = Math.Round(tabela[0].Valor + encargoPorParcela, 2),
            TaxaJurosMensal = taxa, Sistema = dto.Sistema, DataPrimeiraParcela = dto.DataPrimeiraParcela,
            ContaBancariaId = dto.ContaBancariaId, BemVinculado = dto.BemVinculado, Status = "Ativo",
            OrigemMetaId = dto.OrigemMetaId, CriadoEm = DateTime.UtcNow, AtualizadoEm = DateTime.UtcNow,
        };
        var criado = await _financiamentoRepo.AdicionarAsync(financiamento);

        foreach (var linha in tabela)
        {
            var (registro, novo) = await RegistroDiaHelper.ResolverOuCriarAsync(_registroRepo, conta, linha.Vencimento);
            registro.ContasPagar = new List<ContaProvisionada>(registro.ContasPagar)
            {
                new()
                {
                    Id = Guid.NewGuid(), Descricao = $"{dto.Descricao} ({linha.Numero}/{dto.NumeroParcelas})",
                    Valor = linha.Valor + encargoPorParcela, DataVencimento = linha.Vencimento, Pago = false,
                    ContaBancariaId = dto.ContaBancariaId, OrigemFinanciamentoId = criado.Id,
                    ValorJuros = linha.Juros, ValorAmortizacao = linha.Amortizacao,
                    ValorEncargos = encargoPorParcela > 0 ? encargoPorParcela : null,
                },
            };
            registro.SalvoEm = DateTime.UtcNow;
            await RegistroDiaHelper.PersistirAsync(_registroRepo, registro, novo);
        }

        await _auditService.LogAsync(clienteId, usuarioLogadoId, "Financiamento", "Criacao",
            criado.Id.ToString(), null, JsonSerializer.Serialize(new { dto.Descricao, valorFinanciado, dto.NumeroParcelas, taxa }));

        // Fase 5G: este financiamento nasceu do "🏆 Conquistei" (forma "Financiado") — marca o
        // objetivo como conquistado agora que o contrato de verdade existe (ValorPago = valor do
        // bem, igual às outras formas de pagamento tratam o preço total da compra).
        if (dto.OrigemMetaId.HasValue)
        {
            var meta = await _metaRepo.ObterPorIdAsync(dto.OrigemMetaId.Value);
            if (meta != null && meta.ClienteId == clienteId && meta.Status == "EmAndamento")
            {
                meta.Status = "Conquistada";
                meta.DataConquista = DataLocalHelper.Hoje();
                meta.ValorPago = dto.ValorBem;
                meta.FormaPagamento = "Financiado";
                meta.ContaPagamentoId = dto.ContaBancariaId;
                meta.FinanciamentoId = criado.Id;
                meta.AtualizadoEm = DateTime.UtcNow;
                await _metaRepo.SalvarAsync(meta);
            }
        }

        var parcelasCriadas = await ObterParcelasAsync(clienteId, criado.Id);
        return MontarDto(criado, conta.Nome, parcelasCriadas);
    }

    public async Task<List<FinanciamentoDto>> ListarAsync(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);
        var financiamentos = await _financiamentoRepo.ListarPorClienteAsync(clienteId);
        var contas = await _contaRepo.ListarPorClienteAsync(clienteId);
        var registros = await _registroRepo.ListarPorClienteAsync(clienteId);

        var resultado = new List<FinanciamentoDto>();
        foreach (var f in financiamentos)
        {
            var parcelas = registros.SelectMany(r => r.ContasPagar).Where(c => c.OrigemFinanciamentoId == f.Id).ToList();
            var contaNome = contas.FirstOrDefault(c => c.Id == f.ContaBancariaId)?.Nome ?? "—";
            resultado.Add(MontarDto(f, contaNome, parcelas));
        }
        return resultado.OrderByDescending(f => f.Status == "Ativo").ThenByDescending(f => f.Id).ToList();
    }

    public async Task<List<ParcelaFinanciamentoDto>> ListarParcelasAsync(Guid financiamentoId, Guid usuarioLogadoId, string perfil)
    {
        var financiamento = await ObterOuFalharAsync(financiamentoId);
        VerificarAcesso(financiamento.ClienteId, usuarioLogadoId, perfil);

        var parcelas = await ObterParcelasAsync(financiamento.ClienteId, financiamentoId);
        var hoje = DataLocalHelper.Hoje();
        var saldo = financiamento.ValorFinanciado;
        var resultado = new List<ParcelaFinanciamentoDto>();
        var numero = 0;

        foreach (var p in parcelas)
        {
            numero++;
            saldo -= p.ValorAmortizacao ?? p.Valor;
            resultado.Add(new ParcelaFinanciamentoDto
            {
                Id = p.Id, Numero = numero, Vencimento = p.DataVencimento ?? financiamento.DataPrimeiraParcela,
                Valor = p.Valor, Juros = p.ValorJuros ?? 0, Amortizacao = p.ValorAmortizacao ?? p.Valor,
                Encargos = p.ValorEncargos ?? 0, SaldoDevedor = Math.Round(saldo, 2), Pago = p.Pago,
                DataBaixa = p.DataBaixa, Atrasada = !p.Pago && p.DataVencimento.HasValue && p.DataVencimento.Value < hoje,
            });
        }
        return resultado;
    }

    public async Task<ParcelaFinanciamentoDto> BaixarParcelaAsync(
        Guid financiamentoId, Guid usuarioLogadoId, string perfil, BaixarParcelaFinanciamentoDto dto)
    {
        var financiamento = await ObterOuFalharAsync(financiamentoId);
        VerificarAcesso(financiamento.ClienteId, usuarioLogadoId, perfil);

        var registros = await _registroRepo.ListarPorClienteAsync(financiamento.ClienteId);
        var (registro, parcela) = LocalizarParcela(registros, financiamentoId, dto.ParcelaId)
            ?? throw new ApiException(404, CodigoRetorno.PARCELA_NAO_ENCONTRADA, "Parcela não encontrada.");
        if (parcela.Pago)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Esta parcela já foi paga.");

        var conta = await _contaRepo.ObterPorIdAsync(financiamento.ContaBancariaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");

        var valorEfetivo = dto.ValorPago ?? parcela.Valor;
        if (valorEfetivo < parcela.Valor)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS,
                "O valor pago não pode ser menor que o valor da parcela.", "valorPago");

        // Atraso/multa (valor pago > parcela) entra junto com os juros — não é amortização nem
        // encargo contratual (item 6.4: "diferença por atraso → Juros/Multa"; aqui simplificado
        // pra dentro do mesmo lançamento de Juros, sem categoria própria).
        var extra = valorEfetivo - parcela.Valor;
        var jurosLancado = (parcela.ValorJuros ?? 0) + extra;
        var amortizacaoLancada = parcela.ValorAmortizacao ?? parcela.Valor;
        var encargosLancado = parcela.ValorEncargos ?? 0;

        var categoriasPorNome = (await _categoriaRepo.ListarTodasAsync()).ToDictionary(c => c.Nome, c => c.Tipo);
        var (regDestino, novo) = await RegistroDiaHelper.ResolverOuCriarAsync(_registroRepo, conta, dto.DataPagamento);
        var novasSaidas = new List<ItemFinanceiroSaida>(regDestino.Saidas);
        var totalLancado = 0m;

        if (jurosLancado > 0)
        {
            var id = Guid.NewGuid();
            novasSaidas.Add(new ItemFinanceiroSaida
            {
                Id = id, Descricao = $"Juros — {financiamento.Descricao}", Valor = jurosLancado,
                Categoria = "Juros", TipoCusto = categoriasPorNome.GetValueOrDefault("Juros"),
                OrigemFinanciamentoId = financiamentoId,
            });
            parcela.LancamentoJurosId = id;
            totalLancado += jurosLancado;
        }

        var idAmortizacao = Guid.NewGuid();
        novasSaidas.Add(new ItemFinanceiroSaida
        {
            Id = idAmortizacao, Descricao = $"Amortização — {financiamento.Descricao}", Valor = amortizacaoLancada,
            // TipoCusto fixo (não resolvido pela categoria) — garante que cai em Atividades de
            // Financiamento sempre, mesmo que o Plano de Contas não tenha essa categoria configurada.
            Categoria = CategoriaAmortizacao(financiamento.Tipo), TipoCusto = LancamentoFiltro.TipoFinanciamento,
            OrigemFinanciamentoId = financiamentoId,
        });
        parcela.LancamentoAmortizacaoId = idAmortizacao;
        totalLancado += amortizacaoLancada;

        if (encargosLancado > 0)
        {
            var id = Guid.NewGuid();
            novasSaidas.Add(new ItemFinanceiroSaida
            {
                Id = id, Descricao = $"Encargos — {financiamento.Descricao}", Valor = encargosLancado,
                Categoria = "Tarifas bancárias", TipoCusto = categoriasPorNome.GetValueOrDefault("Tarifas bancárias"),
                OrigemFinanciamentoId = financiamentoId,
            });
            parcela.LancamentoEncargosId = id;
            totalLancado += encargosLancado;
        }

        regDestino.Saidas = novasSaidas;
        regDestino.SaldoFinal -= totalLancado;
        regDestino.SalvoEm = DateTime.UtcNow;
        await RegistroDiaHelper.PersistirAsync(_registroRepo, regDestino, novo);

        var registrosDaConta = await _registroRepo.ListarPorContaAsync(conta.Id);
        await RegistroService.RecalcularDiasSeguintesAsync(_registroRepo, conta.Id, dto.DataPagamento, regDestino.SaldoFinal, registrosDaConta);

        parcela.Pago = true;
        parcela.DataBaixa = dto.DataPagamento;
        parcela.ValorRealizado = valorEfetivo;
        registro.ContasPagar = new List<ContaProvisionada>(registro.ContasPagar);
        registro.SalvoEm = DateTime.UtcNow;
        await _registroRepo.AtualizarAsync(registro);

        var todasParcelas = await ObterParcelasAsync(financiamento.ClienteId, financiamentoId);
        if (todasParcelas.Count > 0 && todasParcelas.All(p => p.Pago))
        {
            financiamento.Status = "Quitado";
            financiamento.AtualizadoEm = DateTime.UtcNow;
            await _financiamentoRepo.AtualizarAsync(financiamento);
        }

        await _auditService.LogAsync(financiamento.ClienteId, usuarioLogadoId, "ContaProvisionada", "BaixaParcelaFinanciamento",
            parcela.Id.ToString(), null, JsonSerializer.Serialize(new { jurosLancado, amortizacaoLancada, encargosLancado }));

        return new ParcelaFinanciamentoDto
        {
            Id = parcela.Id, Vencimento = parcela.DataVencimento ?? dto.DataPagamento, Valor = parcela.Valor,
            Juros = parcela.ValorJuros ?? 0, Amortizacao = parcela.ValorAmortizacao ?? parcela.Valor,
            Encargos = parcela.ValorEncargos ?? 0, Pago = true, DataBaixa = parcela.DataBaixa,
        };
    }

    public async Task EstornarParcelaAsync(Guid financiamentoId, Guid parcelaId, Guid usuarioLogadoId, string perfil)
    {
        var financiamento = await ObterOuFalharAsync(financiamentoId);
        VerificarAcesso(financiamento.ClienteId, usuarioLogadoId, perfil);

        var registros = await _registroRepo.ListarPorClienteAsync(financiamento.ClienteId);
        var (registro, parcela) = LocalizarParcela(registros, financiamentoId, parcelaId)
            ?? throw new ApiException(404, CodigoRetorno.PARCELA_NAO_ENCONTRADA, "Parcela não encontrada.");
        if (!parcela.Pago)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Esta parcela ainda não foi paga.");

        var dataBaixa = parcela.DataBaixa ?? parcela.DataVencimento ?? DataLocalHelper.Hoje();
        var regPagamento = await _registroRepo.ObterPorContaEDataAsync(financiamento.ContaBancariaId, dataBaixa);
        if (regPagamento != null)
        {
            var idsRemover = new[] { parcela.LancamentoJurosId, parcela.LancamentoAmortizacaoId, parcela.LancamentoEncargosId }
                .Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();
            var removidos = regPagamento.Saidas.Where(s => idsRemover.Contains(s.Id)).ToList();
            if (removidos.Count > 0)
            {
                regPagamento.Saidas = regPagamento.Saidas.Where(s => !idsRemover.Contains(s.Id)).ToList();
                regPagamento.SaldoFinal += removidos.Sum(s => s.Valor);
                regPagamento.SalvoEm = DateTime.UtcNow;
                await _registroRepo.AtualizarAsync(regPagamento);

                var registrosDaConta = await _registroRepo.ListarPorContaAsync(financiamento.ContaBancariaId);
                await RegistroService.RecalcularDiasSeguintesAsync(
                    _registroRepo, financiamento.ContaBancariaId, dataBaixa, regPagamento.SaldoFinal, registrosDaConta);
            }
        }

        parcela.Pago = false;
        parcela.DataBaixa = null;
        parcela.ValorRealizado = null;
        parcela.LancamentoJurosId = null;
        parcela.LancamentoAmortizacaoId = null;
        parcela.LancamentoEncargosId = null;
        registro.ContasPagar = new List<ContaProvisionada>(registro.ContasPagar);
        registro.SalvoEm = DateTime.UtcNow;
        await _registroRepo.AtualizarAsync(registro);

        if (financiamento.Status == "Quitado")
        {
            financiamento.Status = "Ativo";
            financiamento.AtualizadoEm = DateTime.UtcNow;
            await _financiamentoRepo.AtualizarAsync(financiamento);
        }

        await _auditService.LogAsync(financiamento.ClienteId, usuarioLogadoId, "ContaProvisionada", "EstornoParcelaFinanciamento",
            parcela.Id.ToString(), null, null);
    }

    public async Task<AdiantamentoResultDto> AdiantarAsync(
        Guid financiamentoId, Guid usuarioLogadoId, string perfil, AdiantarFinanciamentoDto dto)
    {
        var financiamento = await ObterOuFalharAsync(financiamentoId);
        VerificarAcesso(financiamento.ClienteId, usuarioLogadoId, perfil);
        if (financiamento.Status == "Quitado")
            throw new ApiException(400, CodigoRetorno.FINANCIAMENTO_JA_QUITADO, "Este financiamento já está quitado.");
        if (dto.Valor <= 0)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Informe um valor de adiantamento maior que zero.", "valor");
        if (dto.Modo is not ("ReduzirPrazo" or "ReduzirParcela"))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Modo inválido.", "modo");

        var registros = await _registroRepo.ListarPorClienteAsync(financiamento.ClienteId);
        var naoPagas = registros
            .SelectMany(r => r.ContasPagar
                .Where(c => c.OrigemFinanciamentoId == financiamentoId && !c.Pago)
                .Select(p => (Registro: r, Parcela: p)))
            .OrderBy(x => x.Parcela.DataVencimento)
            .ToList();

        if (naoPagas.Count == 0)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Não há parcelas em aberto pra adiantar.");

        var saldoAntes = naoPagas.Sum(x => x.Parcela.ValorAmortizacao ?? x.Parcela.Valor);
        var jurosRestantesAntes = naoPagas.Sum(x => x.Parcela.ValorJuros ?? 0);
        if (dto.Valor >= saldoAntes)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS,
                "O valor do adiantamento não pode ser maior ou igual ao saldo devedor — pra isso, quite o contrato.", "valor");

        var saldoDepois = saldoAntes - dto.Valor;
        var taxa = financiamento.TaxaJurosMensal ?? 0m;
        var pmtAtual = naoPagas[0].Parcela.Valor;

        var novaTabela = dto.Modo == "ReduzirPrazo"
            ? RecalcularMantendoPmt(saldoDepois, taxa, pmtAtual)
            : CalculadoraAmortizacao
                .GerarTabelaPrice(saldoDepois, taxa, naoPagas.Count, naoPagas[0].Parcela.DataVencimento ?? dto.Data)
                .Select(l => (l.Valor, l.Juros, l.Amortizacao)).ToList();

        var jurosRestantesDepois = novaTabela.Sum(l => l.Juros);
        var resultado = new AdiantamentoResultDto
        {
            SaldoAntes = Math.Round(saldoAntes, 2), SaldoDepois = Math.Round(saldoDepois, 2),
            ParcelasAntes = naoPagas.Count, ParcelasDepois = novaTabela.Count,
            JurosRestantesAntes = Math.Round(jurosRestantesAntes, 2), JurosRestantesDepois = Math.Round(jurosRestantesDepois, 2),
            EconomiaJuros = Math.Round(jurosRestantesAntes - jurosRestantesDepois, 2),
        };

        if (dto.Simular) return resultado;

        for (var i = 0; i < naoPagas.Count; i++)
        {
            var (registro, parcela) = naoPagas[i];
            if (i < novaTabela.Count)
            {
                var (valor, juros, amortizacao) = novaTabela[i];
                parcela.Valor = valor;
                parcela.ValorJuros = juros;
                parcela.ValorAmortizacao = amortizacao;
            }
            else
            {
                registro.ContasPagar = registro.ContasPagar.Where(c => c.Id != parcela.Id).ToList();
            }
            registro.SalvoEm = DateTime.UtcNow;
            await _registroRepo.AtualizarAsync(registro);
        }

        var conta = await _contaRepo.ObterPorIdAsync(financiamento.ContaBancariaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        var (regAdiantamento, novoReg) = await RegistroDiaHelper.ResolverOuCriarAsync(_registroRepo, conta, dto.Data);
        regAdiantamento.Saidas = new List<ItemFinanceiroSaida>(regAdiantamento.Saidas)
        {
            new()
            {
                Id = Guid.NewGuid(), Descricao = $"Amortização extraordinária — {financiamento.Descricao}", Valor = dto.Valor,
                Categoria = CategoriaAmortizacao(financiamento.Tipo), TipoCusto = LancamentoFiltro.TipoFinanciamento,
                OrigemFinanciamentoId = financiamentoId,
            },
        };
        regAdiantamento.SaldoFinal -= dto.Valor;
        regAdiantamento.SalvoEm = DateTime.UtcNow;
        await RegistroDiaHelper.PersistirAsync(_registroRepo, regAdiantamento, novoReg);
        var registrosDaConta = await _registroRepo.ListarPorContaAsync(conta.Id);
        await RegistroService.RecalcularDiasSeguintesAsync(_registroRepo, conta.Id, dto.Data, regAdiantamento.SaldoFinal, registrosDaConta);

        financiamento.ValorAdiantado += dto.Valor;
        financiamento.EconomiaJurosAcumulada += Math.Max(0, resultado.EconomiaJuros);
        financiamento.AtualizadoEm = DateTime.UtcNow;
        await _financiamentoRepo.AtualizarAsync(financiamento);

        await _auditService.LogAsync(financiamento.ClienteId, usuarioLogadoId, "Financiamento", "Adiantamento",
            financiamentoId.ToString(), null, JsonSerializer.Serialize(resultado));

        return resultado;
    }

    public async Task ExcluirAsync(Guid financiamentoId, Guid usuarioLogadoId, string perfil)
    {
        var financiamento = await ObterOuFalharAsync(financiamentoId);
        VerificarAcesso(financiamento.ClienteId, usuarioLogadoId, perfil);

        var registros = await _registroRepo.ListarPorClienteAsync(financiamento.ClienteId);
        var parcelas = registros
            .Select(r => (Registro: r, Parcelas: r.ContasPagar.Where(c => c.OrigemFinanciamentoId == financiamentoId).ToList()))
            .Where(x => x.Parcelas.Count > 0)
            .ToList();

        if (parcelas.Any(x => x.Parcelas.Any(p => p.Pago)))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS,
                "Este contrato já tem parcela paga — estorne todas antes de excluir.");

        // Transacional: remover as parcelas e o contrato em si têm que valer juntos — senão uma
        // falha no meio deixa o contrato "fantasma" (sem parcela nenhuma, mas ainda "Ativo" na
        // lista), que foi exatamente o bug relatado (item A1 do bundle D6JWEAJa).
        await using var transacao = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;

        foreach (var (registro, _) in parcelas)
        {
            registro.ContasPagar = registro.ContasPagar.Where(c => c.OrigemFinanciamentoId != financiamentoId).ToList();
            registro.SalvoEm = DateTime.UtcNow;
            await _registroRepo.AtualizarAsync(registro);
        }

        await _financiamentoRepo.RemoverAsync(financiamento);
        if (transacao != null) await transacao.CommitAsync();

        await _auditService.LogAsync(financiamento.ClienteId, usuarioLogadoId, "Financiamento", "Exclusao",
            financiamentoId.ToString(), JsonSerializer.Serialize(financiamento), null);
    }

    // Grupo "Consórcios" tem sua própria categoria (item 6.7); os demais tipos usam a categoria
    // genérica de "Empréstimos/Financiamentos" — ambos os grupos caem no mesmo bloco (Atividades de
    // Financiamento) via TipoCusto fixo acima, então isso só afeta a quebra por categoria na tela.
    private static string CategoriaAmortizacao(string tipo) =>
        tipo == "Consorcio" ? "Parcela de consórcio" : "Amortização de financiamento";

    private async Task<Financiamento> ObterOuFalharAsync(Guid id) =>
        await _financiamentoRepo.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.FINANCIAMENTO_NAO_ENCONTRADO, "Financiamento não encontrado.");

    private async Task<List<ContaProvisionada>> ObterParcelasAsync(Guid clienteId, Guid financiamentoId) =>
        (await _registroRepo.ListarPorClienteAsync(clienteId))
            .SelectMany(r => r.ContasPagar)
            .Where(c => c.OrigemFinanciamentoId == financiamentoId)
            .OrderBy(c => c.DataVencimento)
            .ToList();

    private static (RegistroDiario Registro, ContaProvisionada Parcela)? LocalizarParcela(
        List<RegistroDiario> registros, Guid financiamentoId, Guid parcelaId)
    {
        foreach (var registro in registros)
        {
            var parcela = registro.ContasPagar.FirstOrDefault(c => c.Id == parcelaId && c.OrigemFinanciamentoId == financiamentoId);
            if (parcela != null) return (registro, parcela);
        }
        return null;
    }

    // Mantém a parcela (pmt) fixa e recalcula quantas restam até zerar o saldo — a última absorve
    // o resto (sobra menor que pmt). Mesma lógica de "amortização extraordinária reduzindo prazo".
    private static List<(decimal Valor, decimal Juros, decimal Amortizacao)> RecalcularMantendoPmt(
        decimal saldoInicial, decimal taxa, decimal pmt)
    {
        var resultado = new List<(decimal, decimal, decimal)>();
        var saldo = saldoInicial;
        var guarda = 0;
        while (saldo > 0.005m && guarda < 1200)
        {
            guarda++;
            var juros = Math.Round(saldo * taxa, 2);
            var ultima = saldo - (pmt - juros) <= 0.005m;
            decimal amortizacao, valor;
            if (ultima)
            {
                amortizacao = saldo;
                valor = amortizacao + juros;
            }
            else
            {
                amortizacao = pmt - juros;
                valor = pmt;
            }
            saldo -= amortizacao;
            resultado.Add((valor, juros, amortizacao));
        }
        return resultado;
    }

    private static FinanciamentoDto MontarDto(Financiamento f, string contaNome, List<ContaProvisionada> parcelas)
    {
        var pagas = parcelas.Where(p => p.Pago).ToList();
        var naoPagas = parcelas.Where(p => !p.Pago).ToList();
        var saldoDevedor = naoPagas.Sum(p => p.ValorAmortizacao ?? p.Valor);
        var jurosPagos = pagas.Sum(p => p.ValorJuros ?? 0);
        var jurosAPagar = naoPagas.Sum(p => p.ValorJuros ?? 0);
        var proxima = naoPagas.OrderBy(p => p.DataVencimento).FirstOrDefault();
        var ultima = parcelas.OrderBy(p => p.DataVencimento).LastOrDefault();

        return new FinanciamentoDto
        {
            Id = f.Id, Descricao = f.Descricao, Tipo = f.Tipo, ValorBem = f.ValorBem, ValorEntrada = f.ValorEntrada,
            ValorFinanciado = f.ValorFinanciado, NumeroParcelas = f.NumeroParcelas, ValorParcela = f.ValorParcela,
            TaxaJurosMensal = f.TaxaJurosMensal, Sistema = f.Sistema, DataPrimeiraParcela = f.DataPrimeiraParcela,
            ContaBancariaId = f.ContaBancariaId, ContaBancariaNome = contaNome, BemVinculado = f.BemVinculado,
            Status = f.Status,
            SaldoDevedor = Math.Round(saldoDevedor, 2),
            TotalParcelasAtual = parcelas.Count,
            ParcelasPagas = pagas.Count,
            PercentualParcelasPagas = parcelas.Count > 0 ? Math.Round(100m * pagas.Count / parcelas.Count, 1) : 0,
            PercentualDividaQuitada = f.ValorFinanciado > 0 ? Math.Round(100m * (f.ValorFinanciado - saldoDevedor) / f.ValorFinanciado, 1) : 0,
            JurosPagos = Math.Round(jurosPagos, 2),
            JurosAPagar = Math.Round(jurosAPagar, 2),
            JurosTotais = Math.Round(jurosPagos + jurosAPagar, 2),
            ProximaParcelaValor = proxima?.Valor,
            ProximaParcelaVencimento = proxima?.DataVencimento,
            DataPrevistaQuitacao = ultima?.DataVencimento,
            ValorAdiantado = f.ValorAdiantado,
            EconomiaJurosAcumulada = f.EconomiaJurosAcumulada,
        };
    }

    private static void VerificarAcesso(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
    }
}
