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

        var contaId = await ResolverContaPadraoAsync(clienteId, contaBancariaId);
        var registro = await _registroRepository.ObterPorContaEDataAsync(contaId, data)
            ?? await _registroRepository.ObterPorClienteEDataAsync(clienteId, data)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Registro não encontrado.");

        return MapToDto(registro);
    }

    public async Task<(RegistroDto dto, bool criado)> SalvarAsync(CriarRegistroDto dto, string nomeUsuarioLogado)
    {
        if (dto.Data > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ApiException(400, CodigoRetorno.DATA_FUTURA, "Não é possível registrar data futura.", "data");

        if (dto.Saidas.Any(s => string.IsNullOrWhiteSpace(s.Categoria)))
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Toda saída deve ter uma categoria.", "categoria");

        var contaId = await ResolverContaPadraoAsync(dto.ClienteId, dto.ContaBancariaId);

        var existente = await _registroRepository.ObterPorContaEDataAsync(contaId, dto.Data)
            ?? await _registroRepository.ObterPorClienteEDataAsync(dto.ClienteId, dto.Data);
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
            existente.Entradas = dto.Entradas.Select(MapItemDto).ToList();
            existente.Saidas = dto.Saidas.Select(MapSaidaDto).ToList();
            existente.ContasReceber = AplicarBaixaAutomatica(MesclarContas(contasReceberAntes, contasReceberEntrada), dto.Data);
            existente.ContasPagar = AplicarBaixaAutomatica(MesclarContas(contasPagarAntes, contasPagarEntrada), dto.Data);
            DesmarcarDuplicatas(existente.ContasReceber, referenciasReceber);
            DesmarcarDuplicatas(existente.ContasPagar, referenciasPagar);

            var ajuste = AplicarBaixaFinanceira(contasReceberAntes, existente.ContasReceber, dto.Data, isReceber: true)
                       + AplicarBaixaFinanceira(contasPagarAntes, existente.ContasPagar, dto.Data, isReceber: false);
            existente.SaldoFinal = dto.SaldoFinal + ajuste;
            existente.SalvoEm = DateTime.UtcNow;
            existente.AtualizadoEm = DateTime.UtcNow;
            existente.UsuarioAtualizacao = nomeUsuarioLogado;

            var atualizado = await _registroRepository.AtualizarAsync(existente);
            var resultDto = MapToDto(atualizado);

            await _auditService.LogAsync(existente.ClienteId, Guid.Empty, "RegistroDiario", "Edicao",
                $"{existente.ClienteId}/{existente.Data}", dadosAntes, JsonSerializer.Serialize(resultDto));

            return (resultDto, false);
        }

        var contasReceberNovo = AplicarBaixaAutomatica(MesclarContas(new List<ContaProvisionada>(), contasReceberEntrada), dto.Data);
        var contasPagarNovo = AplicarBaixaAutomatica(MesclarContas(new List<ContaProvisionada>(), contasPagarEntrada), dto.Data);
        DesmarcarDuplicatas(contasReceberNovo, referenciasReceber);
        DesmarcarDuplicatas(contasPagarNovo, referenciasPagar);

        var ajusteNovo = AplicarBaixaFinanceira(new List<ContaProvisionada>(), contasReceberNovo, dto.Data, isReceber: true)
                       + AplicarBaixaFinanceira(new List<ContaProvisionada>(), contasPagarNovo, dto.Data, isReceber: false);

        var novo = new RegistroDiario
        {
            Id = Guid.NewGuid(),
            ClienteId = dto.ClienteId,
            ContaBancariaId = contaId,
            Data = dto.Data,
            Inicio = dto.Inicio,
            Entradas = dto.Entradas.Select(MapItemDto).ToList(),
            Saidas = dto.Saidas.Select(MapSaidaDto).ToList(),
            ContasReceber = contasReceberNovo,
            ContasPagar = contasPagarNovo,
            SaldoFinal = dto.SaldoFinal + ajusteNovo,
            CriadoEm = DateTime.UtcNow,
            SalvoEm = DateTime.UtcNow,
            UsuarioAtualizacao = nomeUsuarioLogado
        };

        var criado = await _registroRepository.AdicionarAsync(novo);
        var criadoDto = MapToDto(criado);

        await _auditService.LogAsync(novo.ClienteId, Guid.Empty, "RegistroDiario", "Criacao",
            $"{novo.ClienteId}/{novo.Data}", null, JsonSerializer.Serialize(criadoDto));

        return (criadoDto, true);
    }

    public async Task ExcluirAsync(Guid clienteId, DateOnly data, string motivo, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new ApiException(400, CodigoRetorno.MOTIVO_OBRIGATORIO, "Motivo de exclusão é obrigatório.", "motivo_exclusao");

        var contaId = await ResolverContaPadraoAsync(clienteId, null);
        var registro = await _registroRepository.ObterPorContaEDataAsync(contaId, data)
            ?? await _registroRepository.ObterPorClienteEDataAsync(clienteId, data)
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

    // Resolve a ContaBancariaId: usa a fornecida ou busca o Caixa padrão do cliente.
    private async Task<Guid> ResolverContaPadraoAsync(Guid clienteId, Guid? contaBancariaId)
    {
        if (contaBancariaId.HasValue && contaBancariaId.Value != Guid.Empty)
            return contaBancariaId.Value;

        var caixa = await _contaBancariaRepository.ObterCaixaPadraoAsync(clienteId);
        return caixa?.Id ?? Guid.Empty;
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
                nova.DataBaixa = data;
                delta += sinal * nova.Valor;
            }
            else if (!nova.Pago && pagaAntes)
            {
                nova.DataBaixa = null;
                delta -= sinal * nova.Valor;
            }
            else if (nova.Pago && pagaAntes)
            {
                nova.DataBaixa = antes[i].DataBaixa;
            }
        }

        return delta;
    }

    private static ItemFinanceiro MapItemDto(ItemFinanceiroDto d) =>
        new() { Descricao = d.Descricao, Valor = d.Valor, Categoria = d.Categoria, TipoCusto = d.TipoCusto };

    private static ItemFinanceiroSaida MapSaidaDto(ItemFinanceiroSaidaDto d) =>
        new() { Descricao = d.Descricao, Valor = d.Valor, Categoria = d.Categoria, Subcategoria = d.Subcategoria, TipoCusto = d.TipoCusto };

    private static ContaProvisionada MapContaDto(ContaProvisionadaDto d, Guid? contaBancariaId = null) =>
        new() { Descricao = d.Descricao, Valor = d.Valor, DataVencimento = d.DataVencimento, Pago = d.Pago, Categoria = d.Categoria, RecorrenciaId = d.RecorrenciaId, DataBaixa = d.DataBaixa, ContaBancariaId = d.ContaBancariaId ?? contaBancariaId };

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
                    Descricao = entrada.Descricao,
                    Valor = entrada.Valor,
                    DataVencimento = entrada.DataVencimento,
                    Pago = entrada.Pago,
                    Categoria = entrada.Categoria,
                    RecorrenciaId = entrada.RecorrenciaId,
                    DataBaixa = entrada.DataBaixa,
                    ContaBancariaId = entrada.ContaBancariaId,
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
            correspondencia.ContaBancariaId = entrada.ContaBancariaId;
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
                Descricao = entrada.Descricao,
                Valor = entrada.Valor,
                DataVencimento = entrada.DataVencimento,
                Pago = duplicada ? false : entrada.Pago,
                Categoria = entrada.Categoria,
                RecorrenciaId = entrada.RecorrenciaId,
                DataBaixa = duplicada ? null : entrada.DataBaixa,
                ContaBancariaId = entrada.ContaBancariaId,
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

    private static bool EhMesmoItem(ContaProvisionada a, ContaProvisionada b) =>
        a.Descricao == b.Descricao &&
        Math.Abs(a.Valor - b.Valor) < 0.01m &&
        a.DataVencimento == b.DataVencimento &&
        a.ContaBancariaId == b.ContaBancariaId;

    private static RegistroDto MapToDto(RegistroDiario r) => new()
    {
        Id = r.Id,
        ClienteId = r.ClienteId,
        ContaBancariaId = r.ContaBancariaId,
        Data = r.Data,
        Inicio = r.Inicio,
        Entradas = r.Entradas.Select(s => new ItemFinanceiroDto { Descricao = s.Descricao, Valor = s.Valor, Categoria = s.Categoria, TipoCusto = s.TipoCusto }).ToList(),
        Saidas = r.Saidas.Select(s => new ItemFinanceiroSaidaDto { Descricao = s.Descricao, Valor = s.Valor, Categoria = s.Categoria, Subcategoria = s.Subcategoria, TipoCusto = s.TipoCusto }).ToList(),
        ContasReceber = r.ContasReceber.Select(s => new ContaProvisionadaDto { Descricao = s.Descricao, Valor = s.Valor, DataVencimento = s.DataVencimento, Pago = s.Pago, Categoria = s.Categoria, RecorrenciaId = s.RecorrenciaId, DataBaixa = s.DataBaixa, ContaBancariaId = s.ContaBancariaId }).ToList(),
        ContasPagar = r.ContasPagar.Select(s => new ContaProvisionadaDto { Descricao = s.Descricao, Valor = s.Valor, DataVencimento = s.DataVencimento, Pago = s.Pago, Categoria = s.Categoria, RecorrenciaId = s.RecorrenciaId, DataBaixa = s.DataBaixa, ContaBancariaId = s.ContaBancariaId }).ToList(),
        SaldoFinal = r.SaldoFinal,
        SalvoEm = r.SalvoEm
    };
}
