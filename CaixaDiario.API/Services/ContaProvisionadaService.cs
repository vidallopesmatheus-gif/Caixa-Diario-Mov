using CaixaDiario.API.DTOs.Registros;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

// Fase 0.4/0.5: CRUD de conta a pagar/receber por Id direto — sem reenviar o RegistroDiario
// inteiro. A baixa (Pago false->true) sempre resulta num lançamento real no extrato: cria uma
// Entrada/Saída na conta+data do pagamento, ou vincula a uma já existente (nunca os dois). O
// estorno desfaz só o lançamento que a própria baixa criou — nunca um que já existia e só foi
// vinculado (ver ContaProvisionada.LancamentoCriadoPelaBaixa).
public class ContaProvisionadaService : IContaProvisionadaService
{
    private readonly IRegistroRepository _registroRepo;
    private readonly IContaBancariaRepository _contaBancariaRepo;

    public ContaProvisionadaService(IRegistroRepository registroRepo, IContaBancariaRepository contaBancariaRepo)
    {
        _registroRepo = registroRepo;
        _contaBancariaRepo = contaBancariaRepo;
    }

    public async Task<ContaProvisionadaDto> CriarAsync(CriarContaProvisionadaDto dto, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != dto.ClienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
        if (dto.Tipo != "Receber" && dto.Tipo != "Pagar")
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Tipo deve ser 'Receber' ou 'Pagar'.", "tipo");

        var conta = await ObterContaDoClienteAsync(dto.ContaBancariaId, dto.ClienteId);
        var data = dto.DataVencimento ?? DataLocalHelper.Hoje();
        var (registro, novo) = await RegistroDiaHelper.ResolverOuCriarAsync(_registroRepo, conta, data);

        var item = new ContaProvisionada
        {
            Id = Guid.NewGuid(),
            Descricao = dto.Descricao,
            Valor = dto.Valor,
            DataVencimento = dto.DataVencimento,
            Categoria = dto.Categoria,
            ContaBancariaId = dto.ContaBancariaId,
            Pago = false,
        };

        if (dto.Tipo == "Receber")
            registro.ContasReceber = new List<ContaProvisionada>(registro.ContasReceber) { item };
        else
            registro.ContasPagar = new List<ContaProvisionada>(registro.ContasPagar) { item };
        registro.SalvoEm = DateTime.UtcNow;

        await RegistroDiaHelper.PersistirAsync(_registroRepo, registro, novo);
        return MapToDto(item);
    }

    public async Task<ContaProvisionadaDto> AtualizarAsync(Guid clienteId, Guid id, AtualizarContaProvisionadaDto dto, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        var registros = await _registroRepo.ListarPorClienteAsync(clienteId);
        var (registro, item, ehReceber) = LocalizarItem(registros, id)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta a pagar/receber não encontrada.");

        var pagoAntes = item.Pago;

        if (dto.Descricao != null) item.Descricao = dto.Descricao;
        if (dto.Valor.HasValue) item.Valor = dto.Valor.Value;
        if (dto.DataVencimento.HasValue) item.DataVencimento = dto.DataVencimento;
        if (dto.Categoria != null) item.Categoria = dto.Categoria;
        if (dto.ContaBancariaId.HasValue)
        {
            await ObterContaDoClienteAsync(dto.ContaBancariaId.Value, clienteId);
            item.ContaBancariaId = dto.ContaBancariaId;
        }

        if (dto.Pago == true && !pagoAntes)
            await BaixarAsync(clienteId, registros, item, ehReceber, dto);
        else if (dto.Pago == false && pagoAntes)
            await EstornarAsync(clienteId, registro, item, ehReceber);

        registro.SalvoEm = DateTime.UtcNow;
        if (ehReceber) registro.ContasReceber = new List<ContaProvisionada>(registro.ContasReceber);
        else registro.ContasPagar = new List<ContaProvisionada>(registro.ContasPagar);
        await _registroRepo.AtualizarAsync(registro);

        return MapToDto(item);
    }

    public async Task ExcluirAsync(Guid clienteId, Guid id, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        var registros = await _registroRepo.ListarPorClienteAsync(clienteId);
        var (registro, item, ehReceber) = LocalizarItem(registros, id)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta a pagar/receber não encontrada.");

        if (item.Pago)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Estorne a baixa antes de excluir este título.");

        if (ehReceber) registro.ContasReceber = registro.ContasReceber.Where(c => c.Id != id).ToList();
        else registro.ContasPagar = registro.ContasPagar.Where(c => c.Id != id).ToList();
        registro.SalvoEm = DateTime.UtcNow;
        await _registroRepo.AtualizarAsync(registro);
    }

    // Sempre resulta num lançamento real: cria uma Entrada/Saída na conta+data do pagamento, ou
    // vincula a uma já existente (nunca os dois — ver dto.LancamentoVinculadoId).
    private async Task BaixarAsync(Guid clienteId, List<RegistroDiario> registros, ContaProvisionada item, bool ehReceber, AtualizarContaProvisionadaDto dto)
    {
        var dataPagamento = dto.DataPagamento ?? DataLocalHelper.Hoje();
        var valorEfetivo = dto.ValorRealizado ?? item.Valor;

        item.Pago = true;
        item.DataBaixa = dataPagamento;
        item.ValorRealizado = dto.ValorRealizado;

        if (dto.LancamentoVinculadoId.HasValue)
        {
            // Dinheiro já contado pelo lançamento vinculado — nada a ajustar no saldo, e o Estorno
            // nunca pode apagar esse lançamento (não foi esta baixa que o criou).
            item.LancamentoVinculadoId = dto.LancamentoVinculadoId;
            item.LancamentoCriadoPelaBaixa = false;
            // Item 2.5: quem chama vincular (ex.: aceitar uma sugestão de conciliação) normalmente
            // não manda ValorRealizado — sem buscar o valor real do lançamento vinculado aqui,
            // ValorRealizado ficava sempre null nesse caminho, e a tela nunca mostrava o previsto
            // riscado quando o valor de fato pago/recebido era diferente do provisionado.
            if (!dto.ValorRealizado.HasValue)
                item.ValorRealizado = BuscarValorDoLancamento(registros, dto.LancamentoVinculadoId.Value, ehReceber);
            return;
        }

        var contaDestinoId = item.ContaBancariaId
            ?? throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Conta bancária é obrigatória para dar baixa.", "contaBancariaId");
        var contaDestino = await ObterContaDoClienteAsync(contaDestinoId, clienteId);
        var (regDestino, novoDestino) = await RegistroDiaHelper.ResolverOuCriarAsync(_registroRepo, contaDestino, dataPagamento);
        var novoLancamentoId = Guid.NewGuid();

        if (ehReceber)
        {
            regDestino.Entradas = new List<ItemFinanceiro>(regDestino.Entradas)
            {
                new() { Id = novoLancamentoId, Descricao = item.Descricao, Valor = valorEfetivo, Categoria = item.Categoria, TipoCusto = "Receita" },
            };
        }
        else
        {
            regDestino.Saidas = new List<ItemFinanceiroSaida>(regDestino.Saidas)
            {
                new() { Id = novoLancamentoId, Descricao = item.Descricao, Valor = valorEfetivo, Categoria = item.Categoria ?? "Administrativas", TipoCusto = "CustoFixo" },
            };
        }
        regDestino.SaldoFinal = RegistroService.CalcularSaldoFinal(regDestino.Inicio, regDestino.Entradas, regDestino.Saidas, 0m);
        regDestino.SalvoEm = DateTime.UtcNow;

        item.LancamentoVinculadoId = novoLancamentoId;
        item.LancamentoCriadoPelaBaixa = true;

        await RegistroDiaHelper.PersistirAsync(_registroRepo, regDestino, novoDestino);
        var registrosAtualizados = await _registroRepo.ListarPorClienteAsync(clienteId);
        await RegistroService.RecalcularDiasSeguintesAsync(_registroRepo, contaDestino.Id, dataPagamento, regDestino.SaldoFinal, registrosAtualizados);
    }

    // Desfaz só o lançamento que a própria baixa criou. Se foi vinculado a algo que já existia,
    // esse lançamento é dinheiro real que de fato moveu — nunca apagado, só desvinculado.
    private async Task EstornarAsync(Guid clienteId, RegistroDiario registroOrigem, ContaProvisionada item, bool ehReceber)
    {
        if (item.LancamentoVinculadoId.HasValue && item.LancamentoCriadoPelaBaixa)
        {
            var lancamentoId = item.LancamentoVinculadoId.Value;
            var dataLancamento = item.DataBaixa ?? registroOrigem.Data;
            var contaLancamentoId = item.ContaBancariaId ?? registroOrigem.ContaBancariaId;

            if (contaLancamentoId.HasValue)
            {
                var regLancamento = await _registroRepo.ObterPorContaEDataAsync(contaLancamentoId.Value, dataLancamento);
                if (regLancamento != null)
                {
                    if (ehReceber) regLancamento.Entradas = regLancamento.Entradas.Where(e => e.Id != lancamentoId).ToList();
                    else regLancamento.Saidas = regLancamento.Saidas.Where(s => s.Id != lancamentoId).ToList();

                    regLancamento.SaldoFinal = RegistroService.CalcularSaldoFinal(regLancamento.Inicio, regLancamento.Entradas, regLancamento.Saidas, 0m);
                    regLancamento.SalvoEm = DateTime.UtcNow;
                    await _registroRepo.AtualizarAsync(regLancamento);

                    var registrosAtualizados = await _registroRepo.ListarPorClienteAsync(clienteId);
                    await RegistroService.RecalcularDiasSeguintesAsync(_registroRepo, contaLancamentoId.Value, dataLancamento, regLancamento.SaldoFinal, registrosAtualizados);
                }
            }
        }

        item.Pago = false;
        item.DataBaixa = null;
        item.ValorRealizado = null;
        item.LancamentoVinculadoId = null;
        item.LancamentoCriadoPelaBaixa = false;
    }

    private static decimal? BuscarValorDoLancamento(List<RegistroDiario> registros, Guid lancamentoId, bool ehReceber)
    {
        foreach (var r in registros)
        {
            if (ehReceber)
            {
                var e = r.Entradas.FirstOrDefault(x => x.Id == lancamentoId);
                if (e != null) return e.Valor;
            }
            else
            {
                var s = r.Saidas.FirstOrDefault(x => x.Id == lancamentoId);
                if (s != null) return s.Valor;
            }
        }
        return null;
    }

    private static (RegistroDiario registro, ContaProvisionada item, bool ehReceber)? LocalizarItem(List<RegistroDiario> registros, Guid id)
    {
        foreach (var registro in registros)
        {
            var receber = registro.ContasReceber.FirstOrDefault(c => c.Id == id);
            if (receber != null) return (registro, receber, true);
            var pagar = registro.ContasPagar.FirstOrDefault(c => c.Id == id);
            if (pagar != null) return (registro, pagar, false);
        }
        return null;
    }

    private async Task<ContaBancaria> ObterContaDoClienteAsync(Guid contaBancariaId, Guid clienteId)
    {
        var conta = await _contaBancariaRepo.ObterPorIdAsync(contaBancariaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        if (conta.ClienteId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Conta bancária não pertence a este cliente.");
        return conta;
    }

    private static ContaProvisionadaDto MapToDto(ContaProvisionada c) => new()
    {
        Id = c.Id, Descricao = c.Descricao, Valor = c.Valor, DataVencimento = c.DataVencimento, Pago = c.Pago,
        Categoria = c.Categoria, RecorrenciaId = c.RecorrenciaId, DataBaixa = c.DataBaixa,
        ValorRealizado = c.ValorRealizado, ContaBancariaId = c.ContaBancariaId, LancamentoVinculadoId = c.LancamentoVinculadoId,
    };
}
