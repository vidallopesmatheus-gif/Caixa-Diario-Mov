using System.Text.Json;
using CaixaDiario.API.DTOs.Registros;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class RegistroService : IRegistroService
{
    private readonly IRegistroRepository _registroRepository;
    private readonly IAuditService _auditService;
    private readonly IRecorrenciaService _recorrenciaService;
    private readonly IContaBancariaRepository _contaBancariaRepository;

    public RegistroService(
        IRegistroRepository registroRepository,
        IAuditService auditService,
        IRecorrenciaService recorrenciaService,
        IContaBancariaRepository contaBancariaRepository)
    {
        _registroRepository = registroRepository;
        _auditService = auditService;
        _recorrenciaService = recorrenciaService;
        _contaBancariaRepository = contaBancariaRepository;
    }

    public async Task<List<RegistroDto>> ListarPorClienteAsync(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        await _recorrenciaService.MaterializarMesAtualAsync(clienteId);
        var registros = await _registroRepository.ListarPorClienteAsync(clienteId);
        return registros.Select(MapToDto).ToList();
    }

    public async Task<RegistroDto> ObterPorDataAsync(Guid clienteId, DateOnly data, Guid usuarioLogadoId, string perfil, Guid? contaBancariaId = null)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        var contas = await _contaBancariaRepository.ListarPorClienteAsync(clienteId);
        var contaId = ResolverContaPadrao(contas, contaBancariaId);
        var registro = await _registroRepository.ObterPorContaEDataAsync(contaId, data)
            ?? await _registroRepository.ObterPorClienteEDataAsync(clienteId, data)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Registro não encontrado.");

        return MapToDto(registro);
    }

    public async Task<(RegistroDto dto, bool criado)> SalvarAsync(CriarRegistroDto dto, string nomeUsuarioLogado)
    {
        if (dto.Saidas.Any(s => string.IsNullOrWhiteSpace(s.Categoria)))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Toda saída deve ter uma categoria.", "categoria");

        var contas = await _contaBancariaRepository.ListarPorClienteAsync(dto.ClienteId);

        // Cada item de ContasReceber/ContasPagar pode trazer sua própria ContaBancariaId (ex.: modal
        // "Confirmar recebimento" escolhendo uma conta diferente da conta padrão do registro) — sem essa
        // checagem, um payload adulterado conseguia gravar baixa numa conta de outro cliente ou inativa.
        foreach (var item in dto.ContasReceber.Concat(dto.ContasPagar))
            ValidarContaVinculada(contas, item.ContaBancariaId);

        // Id explícito é a forma mais segura de apontar pra UM registro específico — não depende de
        // (ClienteId, Data, ContaBancariaId) baterem exatamente. Quando vem preenchido, é autoritativo:
        // não cai pra busca por data/conta nem silenciosamente ignora um Id que não existe mais.
        RegistroDiario? existente;
        if (dto.Id.HasValue)
        {
            existente = await _registroRepository.ObterPorIdAsync(dto.Id.Value)
                ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Registro não encontrado.");
            if (existente.ClienteId != dto.ClienteId)
                throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
            // A cascata de recálculo dos dias seguintes (RecalcularDiasSeguintesAsync) usa dto.Data —
            // se divergisse da data real do registro encontrado, recalcularia a partir do dia errado.
            if (existente.Data != dto.Data)
                throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Data não corresponde ao registro informado.", "data");
        }
        else
        {
            // Localiza o registro já existente pela conta EXATA informada (inclusive ausente) — nunca
            // resolvendo pra uma "conta padrão" aqui. Um registro sem conta vinculada (ex.: ocorrência
            // de recorrência materializada sem ContaBancariaId) é um registro DIFERENTE do registro da
            // conta padrão naquele mesmo dia; resolver a conta antes de buscar fazia a edição de um
            // mirar (e sobrescrever) o registro errado sempre que ContaBancariaId vinha null.
            existente = dto.ContaBancariaId.HasValue
                ? await _registroRepository.ObterPorContaEDataAsync(dto.ContaBancariaId.Value, dto.Data)
                : await _registroRepository.ObterPorClienteEDataSemContaAsync(dto.ClienteId, dto.Data);
        }

        // Só resolve pra uma conta padrão quando precisamos de ALGUMA conta concreta: pra criar um
        // registro novo, ou pra atribuir aos itens de ContasReceber/ContasPagar que não trazem a
        // própria conta. Preservar o ContaBancariaId do registro já existente, quando há um.
        var contaId = existente?.ContaBancariaId ?? ResolverContaPadrao(contas, dto.ContaBancariaId);

        // Só bloqueia data futura pra registro NOVO — um lançamento manual de Caixa num dia que
        // ainda não aconteceu. Um registro já EXISTENTE pode legitimamente ter Data futura:
        // RecorrenciaService materializa contas a pagar/receber com antecedência pro mês inteiro
        // (ver MaterializarMesAtualAsync). Resalvar esse registro — baixa, edição ou exclusão de
        // uma pendência — não é "lançar no futuro", é continuar algo que já existe; barrar isso
        // impedia até confirmar recebimento antecipado de uma conta com vencimento amanhã.
        if (existente == null && dto.Data > DataLocalHelper.Hoje())
            throw new ApiException(400, CodigoRetorno.DATA_FUTURA, "Não é possível registrar data futura.", "data");

        var registrosCliente = (await _registroRepository.ListarPorClienteAsync(dto.ClienteId)) ?? new List<RegistroDiario>();
        var dadosAntes = existente != null ? JsonSerializer.Serialize(MapToDto(existente)) : null;
        var referenciasReceber = registrosCliente
            .Where(r => r.Id != existente?.Id && r.Data != dto.Data)
            .SelectMany(r => r.ContasReceber)
            .ToList();
        var referenciasPagar = registrosCliente
            .Where(r => r.Id != existente?.Id && r.Data != dto.Data)
            .SelectMany(r => r.ContasPagar)
            .ToList();

        var contasReceberEntrada = FiltrarDuplicadas(dto.ContasReceber.Select(d => MapContaDto(d, contaId)).ToList(), referenciasReceber);
        var contasPagarEntrada = FiltrarDuplicadas(dto.ContasPagar.Select(d => MapContaDto(d, contaId)).ToList(), referenciasPagar);

        if (existente != null)
        {
            var contasReceberAntes = existente.ContasReceber;
            var contasPagarAntes = existente.ContasPagar;

            existente.Inicio = dto.Inicio;
            existente.Entradas = DeduplicarPorId(dto.Entradas.Select(MapItemDto).ToList(), e => e.Id);
            existente.Saidas = DeduplicarPorId(dto.Saidas.Select(MapSaidaDto).ToList(), s => s.Id);
            existente.ContasReceber = AplicarBaixaAutomatica(MesclarContas(contasReceberAntes, contasReceberEntrada), dto.Data);
            existente.ContasPagar = AplicarBaixaAutomatica(MesclarContas(contasPagarAntes, contasPagarEntrada), dto.Data);
            DesmarcarDuplicatas(existente.ContasReceber, referenciasReceber);
            DesmarcarDuplicatas(existente.ContasPagar, referenciasPagar);

            var ajuste = AplicarBaixaFinanceira(contasReceberAntes, existente.ContasReceber, dto.Data, isReceber: true)
                       + AplicarBaixaFinanceira(contasPagarAntes, existente.ContasPagar, dto.Data, isReceber: false);
            existente.SaldoFinal = CalcularSaldoFinal(dto.Inicio, existente.Entradas, existente.Saidas, ajuste);
            existente.SalvoEm = DateTime.UtcNow;
            existente.AtualizadoEm = DateTime.UtcNow;
            existente.UsuarioAtualizacao = nomeUsuarioLogado;

            // Antes de persistir: alguma ocorrência de recorrência que estava aqui e sumiu da
            // lista final foi excluída pelo cliente — registra a dispensa, senão a próxima
            // materialização (toda vez que a lista é recarregada) recria ela na hora.
            var removidas = OcorrenciasRecorrentesRemovidas(contasReceberAntes, existente.ContasReceber)
                .Concat(OcorrenciasRecorrentesRemovidas(contasPagarAntes, existente.ContasPagar));
            foreach (var (recorrenciaId, dataVencimento) in removidas)
                await _recorrenciaService.DispensarOcorrenciaAsync(existente.ClienteId, recorrenciaId, dataVencimento);

            var atualizado = await _registroRepository.AtualizarAsync(existente);
            var resultDto = MapToDto(atualizado);

            await _auditService.LogAsync(existente.ClienteId, Guid.Empty, "RegistroDiario", "Edicao",
                $"{existente.ClienteId}/{existente.Data}", dadosAntes, JsonSerializer.Serialize(resultDto));

            await RecalcularDiasSeguintesAsync(_registroRepository, contaId, dto.Data, atualizado.SaldoFinal, registrosCliente);

            return (resultDto, false);
        }

        var contasReceberNovo = AplicarBaixaAutomatica(MesclarContas(new List<ContaProvisionada>(), contasReceberEntrada), dto.Data);
        var contasPagarNovo = AplicarBaixaAutomatica(MesclarContas(new List<ContaProvisionada>(), contasPagarEntrada), dto.Data);
        DesmarcarDuplicatas(contasReceberNovo, referenciasReceber);
        DesmarcarDuplicatas(contasPagarNovo, referenciasPagar);

        var ajusteNovo = AplicarBaixaFinanceira(new List<ContaProvisionada>(), contasReceberNovo, dto.Data, isReceber: true)
                       + AplicarBaixaFinanceira(new List<ContaProvisionada>(), contasPagarNovo, dto.Data, isReceber: false);

        var entradasNovo = DeduplicarPorId(dto.Entradas.Select(MapItemDto).ToList(), e => e.Id);
        var saidasNovo = DeduplicarPorId(dto.Saidas.Select(MapSaidaDto).ToList(), s => s.Id);

        var novo = new RegistroDiario
        {
            Id = Guid.NewGuid(),
            ClienteId = dto.ClienteId,
            ContaBancariaId = contaId,
            Data = dto.Data,
            Inicio = dto.Inicio,
            Entradas = entradasNovo,
            Saidas = saidasNovo,
            ContasReceber = contasReceberNovo,
            ContasPagar = contasPagarNovo,
            SaldoFinal = CalcularSaldoFinal(dto.Inicio, entradasNovo, saidasNovo, ajusteNovo),
            CriadoEm = DateTime.UtcNow,
            SalvoEm = DateTime.UtcNow,
            UsuarioAtualizacao = nomeUsuarioLogado
        };

        var criado = await _registroRepository.AdicionarAsync(novo);
        var criadoDto = MapToDto(criado);

        await _auditService.LogAsync(novo.ClienteId, Guid.Empty, "RegistroDiario", "Criacao",
            $"{novo.ClienteId}/{novo.Data}", null, JsonSerializer.Serialize(criadoDto));

        await RecalcularDiasSeguintesAsync(_registroRepository, contaId, dto.Data, criado.SaldoFinal, registrosCliente);

        return (criadoDto, true);
    }

    // Nunca confia no "SaldoFinal" que o cliente mandou (na prática o valor digitado em "Confirmar
    // saldo", só pra conferência visual) — o saldo gravado é sempre a matemática real do dia, senão
    // um valor digitado errado (de propósito ou não) corrompe o livro-caixa. Arredonda em decimal
    // (nunca double) pra bater centavo a centavo com o que a tela mostra.
    // internal: também usado por ContaProvisionadaService (Fase 0.4/0.5) — nunca duplicar essa
    // conta em outro lugar.
    internal static decimal CalcularSaldoFinal(decimal inicio, List<ItemFinanceiro> entradas, List<ItemFinanceiroSaida> saidas, decimal ajuste)
    {
        var calculado = inicio + entradas.Sum(e => e.Valor) - saidas.Sum(s => s.Valor);
        return Math.Round(calculado, 2, MidpointRounding.AwayFromZero) + ajuste;
    }

    // Quando o SaldoFinal de um dia muda, todo dia seguinte da MESMA conta que já existia (ex.:
    // ocorrências de recorrência materializadas com antecedência) ficou com um Inicio desatualizado.
    // Propaga a diferença em cadeia — soma o mesmo delta no Inicio e no SaldoFinal de cada um, na
    // ordem, preservando as entradas/saídas/baixas de cada dia (que não mudaram). Para na primeira
    // divergência zerada: se aquele dia já está consistente, os de depois também estarão.
    // internal static (recebe o repositório por parâmetro): também usado por ContaProvisionadaService.
    internal static async Task RecalcularDiasSeguintesAsync(
        IRegistroRepository registroRepository, Guid contaId, DateOnly data, decimal saldoFinalDoDia, List<RegistroDiario> registrosCliente)
    {
        var saldoCorrente = saldoFinalDoDia;
        var seguintes = registrosCliente
            .Where(r => r.ContaBancariaId == contaId && r.Data > data)
            .OrderBy(r => r.Data);

        foreach (var proximo in seguintes)
        {
            var delta = saldoCorrente - proximo.Inicio;
            if (delta == 0m) break;
            proximo.Inicio += delta;
            proximo.SaldoFinal += delta;
            saldoCorrente = proximo.SaldoFinal;
            await registroRepository.AtualizarAsync(proximo);
        }
    }

    public async Task ExcluirAsync(Guid clienteId, DateOnly data, Guid? contaBancariaId, string motivo, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new ApiException(400, CodigoRetorno.MOTIVO_OBRIGATORIO, "Motivo de exclusão é obrigatório.", "motivo_exclusao");

        var contas = await _contaBancariaRepository.ListarPorClienteAsync(clienteId);
        ValidarContaVinculada(contas, contaBancariaId);

        // Localiza pela conta EXATA informada (inclusive ausente) — nunca resolvendo pra uma "conta
        // padrão". Com mais de uma conta tendo registro no mesmo dia, resolver pra uma conta padrão
        // sempre apagava o registro dela (ou o primeiro encontrado), mesmo quando o usuário queria
        // excluir o de outra conta.
        var registro = (contaBancariaId.HasValue
                ? await _registroRepository.ObterPorContaEDataAsync(contaBancariaId.Value, data)
                : await _registroRepository.ObterPorClienteEDataSemContaAsync(clienteId, data))
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Registro não encontrado.");

        var dadosAntes = JsonSerializer.Serialize(MapToDto(registro));
        registro.Excluido = true;
        registro.MotivoExclusao = motivo;
        registro.AtualizadoEm = DateTime.UtcNow;
        registro.UsuarioAtualizacao = usuarioLogadoId.ToString();

        await _registroRepository.AtualizarAsync(registro);
        await _auditService.LogAsync(clienteId, usuarioLogadoId, "RegistroDiario", "Exclusao",
            $"{clienteId}/{data}", dadosAntes, null);
    }

    // Resolve a ContaBancariaId: usa a fornecida ou, na ausência dela (ex.: baixa de conta a
    // pagar/receber cujo registro nunca teve conta vinculada), cai pra alguma conta do cliente.
    // Caixa é só uma PREFERÊNCIA de fallback (mantém o comportamento histórico de quem sempre
    // lançou tudo lá) — não é mais exigida: qualquer conta ativa do cliente serve. Nunca devolve
    // Guid.Empty — RegistroDiario.ContaBancariaId tem FK pra contas_bancarias, e gravar
    // Guid.Empty ali derruba a query com violação de FK (500 genérico pro usuário, sem pista
    // nenhuma do que houve). Só falha se o cliente não tiver NENHUMA conta bancária cadastrada.
    //
    // Quando contaBancariaId é fornecida (explícita no registro ou numa baixa), ela precisa
    // pertencer ao cliente e estar ativa — do contrário um payload adulterado conseguiria gravar
    // lançamentos numa conta de outro cliente, barrado só pela FK do banco (sem mensagem clara).
    private static Guid ResolverContaPadrao(List<ContaBancaria> contas, Guid? contaBancariaId)
    {
        if (contaBancariaId.HasValue && contaBancariaId.Value != Guid.Empty)
            return ValidarContaVinculada(contas, contaBancariaId.Value);

        var padrao = contas.FirstOrDefault(c => c.Tipo == "Caixa" && c.Ativa)
            ?? contas.FirstOrDefault(c => c.Ativa)
            ?? contas.FirstOrDefault();
        if (padrao != null)
            return padrao.Id;

        throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS,
            "Este cliente ainda não tem nenhuma conta bancária cadastrada. Cadastre uma em Configurações antes de continuar.");
    }

    private static Guid ValidarContaVinculada(List<ContaBancaria> contas, Guid? contaBancariaId)
    {
        if (!contaBancariaId.HasValue || contaBancariaId.Value == Guid.Empty)
            return Guid.Empty;

        var conta = contas.FirstOrDefault(c => c.Id == contaBancariaId.Value)
            ?? throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Conta bancária não pertence a este cliente.");
        if (!conta.Ativa)
            throw new ApiException(400, CodigoRetorno.CONTA_INATIVA, "A conta bancária selecionada está inativa.");

        return conta.Id;
    }

    private static List<ContaProvisionada> AplicarBaixaAutomatica(List<ContaProvisionada> contas, DateOnly data)
    {
        foreach (var c in contas)
        {
            if (c.DataVencimento.HasValue && c.DataVencimento.Value == data && !c.Pago)
                c.Pago = true;
        }
        return contas;
    }

    // D7: detecta transições de pago comparando estado anterior com o novo, ajusta DataBaixa.
    private static decimal AplicarBaixaFinanceira(
        List<ContaProvisionada> antes, List<ContaProvisionada> depois, DateOnly data, bool isReceber)
    {
        decimal delta = 0m;
        var sinal = isReceber ? 1m : -1m;

        for (int i = 0; i < depois.Count; i++)
        {
            var nova = depois[i];
            var pagaAntes = i < antes.Count && antes[i].Pago;

            if (nova.Pago && !pagaAntes)
            {
                // Respeita a data de pagamento explícita (MesclarContas já a carregou aqui) — só cai
                // pra "data do save" quando o cliente não informou nenhuma.
                nova.DataBaixa ??= data;
                // ValorRealizado cobre juros/multa/desconto: o que de fato moveu pode ser diferente do
                // Valor do título (que nunca muda — é a referência usada pra casar o item depois).
                var valorEfetivo = nova.ValorRealizado ?? nova.Valor;
                // Vinculada a um lançamento já existente: aquele dinheiro já está contado no saldo
                // via o lançamento em si — aplicar o delta aqui duplicaria o valor.
                if (!nova.LancamentoVinculadoId.HasValue)
                    delta += sinal * valorEfetivo;
            }
            else if (!nova.Pago && pagaAntes)
            {
                nova.DataBaixa = null;
                // Só reverte o delta se a baixa anterior realmente o aplicou (não vinculada).
                if (!antes[i].LancamentoVinculadoId.HasValue)
                    delta -= sinal * (antes[i].ValorRealizado ?? antes[i].Valor);
                nova.LancamentoVinculadoId = null;
                nova.ValorRealizado = null;
            }
            else if (nova.Pago && pagaAntes)
            {
                nova.DataBaixa = antes[i].DataBaixa;
            }
        }

        return delta;
    }

    // Protege contra o mesmo item sendo enviado duas vezes na mesma lista (ex.: um item já salvo
    // sendo reconcatenado com a lista existente no frontend). Id == Guid.Empty nunca é deduplicado:
    // lançamentos manuais antigos nunca tiveram id estável, então vários com Id vazio são itens
    // distintos de verdade, não duplicatas.
    private static List<T> DeduplicarPorId<T>(List<T> itens, Func<T, Guid> idDe)
    {
        var vistos = new HashSet<Guid>();
        var resultado = new List<T>();
        foreach (var item in itens)
        {
            var id = idDe(item);
            if (id != Guid.Empty && !vistos.Add(id)) continue;
            resultado.Add(item);
        }
        return resultado;
    }

    private static ItemFinanceiro MapItemDto(ItemFinanceiroDto d) =>
        new()
        {
            Id = d.Id, Descricao = d.Descricao, Valor = d.Valor, Categoria = d.Categoria, TipoCusto = d.TipoCusto,
            TransferenciaId = d.TransferenciaId, FitId = d.FitId, PendenteCategorizacao = d.PendenteCategorizacao,
        };

    private static ItemFinanceiroSaida MapSaidaDto(ItemFinanceiroSaidaDto d) =>
        new()
        {
            Id = d.Id, Descricao = d.Descricao, Valor = d.Valor, Categoria = d.Categoria, Subcategoria = d.Subcategoria,
            TipoCusto = d.TipoCusto, TransferenciaId = d.TransferenciaId, FitId = d.FitId, PendenteCategorizacao = d.PendenteCategorizacao,
        };

    private static ContaProvisionada MapContaDto(ContaProvisionadaDto d, Guid? contaBancariaId = null) =>
        new()
        {
            // Todo item novo (Id vazio) ganha um Id de verdade aqui — nunca mais precisa regravar
            // o registro inteiro pra editar/excluir esse item especificamente (Fase 0.4).
            Id = d.Id == Guid.Empty ? Guid.NewGuid() : d.Id,
            Descricao = d.Descricao, Valor = d.Valor, DataVencimento = d.DataVencimento, Pago = d.Pago, Categoria = d.Categoria,
            RecorrenciaId = d.RecorrenciaId, DataBaixa = d.DataBaixa, ValorRealizado = d.ValorRealizado, ContaBancariaId = d.ContaBancariaId ?? contaBancariaId,
            LancamentoVinculadoId = d.LancamentoVinculadoId,
        };

    private static List<ContaProvisionada> MesclarContas(IReadOnlyCollection<ContaProvisionada> existentes, IReadOnlyCollection<ContaProvisionada> entradas)
    {
        var resultado = new List<ContaProvisionada>();

        foreach (var entrada in entradas)
        {
            var correspondencia = resultado.FirstOrDefault(c => EhMesmoItem(c, entrada))
                ?? existentes.FirstOrDefault(c => EhMesmoItem(c, entrada));

            if (correspondencia is null)
            {
                resultado.Add(new ContaProvisionada
                {
                    Id = entrada.Id,
                    Descricao = entrada.Descricao,
                    Valor = entrada.Valor,
                    DataVencimento = entrada.DataVencimento,
                    Pago = entrada.Pago,
                    Categoria = entrada.Categoria,
                    RecorrenciaId = entrada.RecorrenciaId,
                    DataBaixa = entrada.DataBaixa,
                    ValorRealizado = entrada.ValorRealizado,
                    ContaBancariaId = entrada.ContaBancariaId,
                    LancamentoVinculadoId = entrada.LancamentoVinculadoId,
                });
                continue;
            }

            correspondencia.Descricao = entrada.Descricao;
            correspondencia.Valor = entrada.Valor;
            correspondencia.DataVencimento = entrada.DataVencimento;
            correspondencia.Pago = entrada.Pago || correspondencia.Pago;
            correspondencia.Categoria = entrada.Categoria;
            correspondencia.RecorrenciaId = entrada.RecorrenciaId;
            correspondencia.DataBaixa = correspondencia.DataBaixa ?? entrada.DataBaixa;
            correspondencia.ValorRealizado = correspondencia.ValorRealizado ?? entrada.ValorRealizado;
            correspondencia.ContaBancariaId = entrada.ContaBancariaId;
            correspondencia.LancamentoVinculadoId = entrada.LancamentoVinculadoId ?? correspondencia.LancamentoVinculadoId;
            resultado.Add(correspondencia);
        }

        return resultado;
    }

    private static List<ContaProvisionada> FiltrarDuplicadas(IReadOnlyCollection<ContaProvisionada> entradas, IReadOnlyCollection<ContaProvisionada>? referencias)
    {
        if (referencias is null || referencias.Count == 0)
            return entradas.ToList();

        return entradas.Select(entrada =>
        {
            var duplicada = referencias.Any(referencia => EhMesmoItem(referencia, entrada));
            return new ContaProvisionada
            {
                Id = entrada.Id,
                Descricao = entrada.Descricao,
                Valor = entrada.Valor,
                DataVencimento = entrada.DataVencimento,
                Pago = duplicada ? false : entrada.Pago,
                Categoria = entrada.Categoria,
                RecorrenciaId = entrada.RecorrenciaId,
                DataBaixa = duplicada ? null : entrada.DataBaixa,
                ValorRealizado = duplicada ? null : entrada.ValorRealizado,
                ContaBancariaId = entrada.ContaBancariaId,
                LancamentoVinculadoId = duplicada ? null : entrada.LancamentoVinculadoId,
            };
        }).ToList();
    }

    private static void DesmarcarDuplicatas(List<ContaProvisionada> contas, IReadOnlyCollection<ContaProvisionada>? referencias)
    {
        if (referencias is null || referencias.Count == 0)
            return;

        foreach (var conta in contas)
        {
            var duplicada = referencias.Any(referencia => EhMesmoItem(referencia, conta));
            if (duplicada)
            {
                conta.Pago = false;
                conta.DataBaixa = null;
            }
        }
    }

    // Prefere casar por Id quando os dois lados têm um de verdade — único jeito seguro de
    // reconhecer o mesmo item quando valor/descrição mudam (ex.: baixa com desconto/juros, ou
    // edição). Itens legados (Id vazio de qualquer um dos lados) caem no heurístico antigo.
    private static bool EhMesmoItem(ContaProvisionada a, ContaProvisionada b) =>
        a.Id != Guid.Empty && b.Id != Guid.Empty
            ? a.Id == b.Id
            : a.Descricao == b.Descricao &&
              Math.Abs(a.Valor - b.Valor) < 0.01m &&
              a.DataVencimento == b.DataVencimento &&
              a.ContaBancariaId == b.ContaBancariaId;

    // Ocorrências de conta recorrente (RecorrenciaId preenchido) que estavam na lista antes do
    // merge e não estão mais depois — o cliente excluiu essa ocorrência específica.
    private static IEnumerable<(Guid RecorrenciaId, DateOnly DataVencimento)> OcorrenciasRecorrentesRemovidas(
        List<ContaProvisionada> antes, List<ContaProvisionada> depois) =>
        antes
            .Where(a => a.RecorrenciaId.HasValue && a.DataVencimento.HasValue)
            .Where(a => !depois.Any(d => d.RecorrenciaId == a.RecorrenciaId && d.DataVencimento == a.DataVencimento))
            .Select(a => (a.RecorrenciaId!.Value, a.DataVencimento!.Value))
            .Distinct();

    private static RegistroDto MapToDto(RegistroDiario r) => new()
    {
        Id = r.Id,
        ClienteId = r.ClienteId,
        ContaBancariaId = r.ContaBancariaId,
        Data = r.Data,
        Inicio = r.Inicio,
        Entradas = r.Entradas.Select(s => new ItemFinanceiroDto
        {
            Id = s.Id, Descricao = s.Descricao, Valor = s.Valor, Categoria = s.Categoria, TipoCusto = s.TipoCusto,
            TransferenciaId = s.TransferenciaId, FitId = s.FitId, PendenteCategorizacao = s.PendenteCategorizacao,
            ClassificadoPeloCliente = s.ClassificadoPeloCliente,
        }).ToList(),
        Saidas = r.Saidas.Select(s => new ItemFinanceiroSaidaDto
        {
            Id = s.Id, Descricao = s.Descricao, Valor = s.Valor, Categoria = s.Categoria, Subcategoria = s.Subcategoria,
            TipoCusto = s.TipoCusto, TransferenciaId = s.TransferenciaId, FitId = s.FitId, PendenteCategorizacao = s.PendenteCategorizacao,
            ClassificadoPeloCliente = s.ClassificadoPeloCliente,
        }).ToList(),
        ContasReceber = r.ContasReceber.Select(s => new ContaProvisionadaDto { Id = s.Id, Descricao = s.Descricao, Valor = s.Valor, DataVencimento = s.DataVencimento, Pago = s.Pago, Categoria = s.Categoria, RecorrenciaId = s.RecorrenciaId, DataBaixa = s.DataBaixa, ValorRealizado = s.ValorRealizado, ContaBancariaId = s.ContaBancariaId, LancamentoVinculadoId = s.LancamentoVinculadoId }).ToList(),
        ContasPagar = r.ContasPagar.Select(s => new ContaProvisionadaDto { Id = s.Id, Descricao = s.Descricao, Valor = s.Valor, DataVencimento = s.DataVencimento, Pago = s.Pago, Categoria = s.Categoria, RecorrenciaId = s.RecorrenciaId, DataBaixa = s.DataBaixa, ValorRealizado = s.ValorRealizado, ContaBancariaId = s.ContaBancariaId, LancamentoVinculadoId = s.LancamentoVinculadoId }).ToList(),
        SaldoFinal = r.SaldoFinal,
        SalvoEm = r.SalvoEm
    };
}
