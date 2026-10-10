using System.Text.Json;
using CaixaDiario.API.DTOs.Registros;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class RegistroServiceTests
{
    private readonly Mock<IRegistroRepository> _repoMock = new();
    private readonly Mock<IAuditService> _auditMock = new();
    private readonly Mock<IRecorrenciaService> _recorrenciaMock = new();
    private readonly Mock<IContaBancariaRepository> _contaBancariaMock = new();
    private readonly Mock<IConciliacaoService> _conciliacaoMock = new();
    private readonly RegistroService _sut;

    public RegistroServiceTests()
    {
        // Item 3.3: SalvarAsync chama o motor de vínculo depois de salvar — por padrão, sem
        // sugestões, pra não afetar os testes que não são sobre isso especificamente.
        _conciliacaoMock.Setup(c => c.ListarSugestoesAsync(
            It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>()))
            .ReturnsAsync(new List<CaixaDiario.API.DTOs.Conciliacao.SugestaoVinculoDto>());
        _auditMock.Setup(a => a.LogAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        _recorrenciaMock.Setup(r => r.MaterializarMesAtualAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
        // Fallback padrão pros testes que não passam ContaBancariaId no dto (a maioria) — sem isso,
        // ResolverContaPadraoAsync não tem nenhuma conta pra cair de volta. Testes que querem cenários
        // específicos de resolução de conta (ver região "ResolverContaPadraoAsync") sobrescrevem isso.
        _contaBancariaMock.Setup(c => c.ListarPorClienteAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new List<ContaBancaria> { new() { Id = Guid.NewGuid(), Tipo = "Caixa", Ativa = true } });
        _sut = new RegistroService(_repoMock.Object, _auditMock.Object, _recorrenciaMock.Object, _contaBancariaMock.Object, _conciliacaoMock.Object);
    }

    private static CriarRegistroDto CriarDto(DateOnly? data = null)
    {
        return new CriarRegistroDto
        {
            ClienteId = Guid.NewGuid(),
            Data = data ?? DataLocalHelper.Hoje(),
            Inicio = 100m,
            Entradas = new List<ItemFinanceiroDto> { new() { Descricao = "Caixa", Valor = 500m } },
            Saidas = new List<ItemFinanceiroSaidaDto> { new() { Descricao = "Aluguel", Valor = 200m, Categoria = "Aluguel" } },
            ContasReceber = new(),
            ContasPagar = new(),
            SaldoFinal = 400m
        };
    }

    private static RegistroDiario CriarRegistroComContas(Guid clienteId, DateOnly data) => new()
    {
        Id = Guid.NewGuid(),
        ClienteId = clienteId,
        Data = data,
        ContasReceber = new List<ContaProvisionada>
        {
            new() { Descricao = "Venda A", Valor = 500m, DataVencimento = data, Pago = false },
        },
        ContasPagar = new List<ContaProvisionada>
        {
            new() { Descricao = "Fornecedor B", Valor = 200m, DataVencimento = data, Pago = false },
        },
        Entradas = new(),
        Saidas = new(),
        CriadoEm = DateTime.UtcNow,
        SalvoEm = DateTime.UtcNow
    };

    [Fact]
    public async Task SalvarAsync_DataFutura_LancaExcecao()
    {
        var dto = new CriarRegistroDto
        {
            ClienteId = Guid.NewGuid(),
            Data = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.SalvarAsync(dto, "admin"));
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DATA_FUTURA, ex.Codigo);
    }

    [Fact]
    public async Task SalvarAsync_RegistroExistenteComDataFutura_PermiteBaixaAntecipada()
    {
        // Cenário real do bug: RecorrenciaService materializa uma conta a receber com antecedência
        // (vencimento amanhã, dentro do mês atual) — o registro correspondente já existe com Data
        // futura. Confirmar o recebimento hoje (antes do vencimento) reenvia esse mesmo Data e não
        // pode ser barrado como se fosse um lançamento novo de Caixa no futuro.
        var clienteId = Guid.NewGuid();
        var dataFutura = DataLocalHelper.Hoje().AddDays(1);
        var registroExistente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = dataFutura,
            Entradas = new(), Saidas = new(), ContasPagar = new(),
            ContasReceber = new List<ContaProvisionada>
            {
                new() { Descricao = "Pedro Personal", Valor = 350m, DataVencimento = dataFutura, Pago = false },
            },
            SaldoFinal = 0m,
        };
        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(clienteId, dataFutura)).ReturnsAsync(registroExistente);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId, Data = dataFutura, Entradas = new(), Saidas = new(), ContasPagar = new(),
            ContasReceber = new List<ContaProvisionadaDto>
            {
                new() { Descricao = "Pedro Personal", Valor = 350m, DataVencimento = dataFutura, Pago = true },
            },
        };

        var (resultado, criado) = await _sut.SalvarAsync(dto, "admin");

        Assert.False(criado);
        Assert.True(resultado.ContasReceber[0].Pago);
    }

    // --- 0.3: upsert por Id explícito ---

    [Fact]
    public async Task SalvarAsync_ComIdExplicito_LocalizaRegistroPorIdEEdita()
    {
        var clienteId = Guid.NewGuid();
        var data = new DateOnly(2026, 6, 1);
        var registroExistente = CriarRegistroComContas(clienteId, data);

        _repoMock.Setup(r => r.ObterPorIdAsync(registroExistente.Id)).ReturnsAsync(registroExistente);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = CriarDto(data);
        dto.Id = registroExistente.Id;
        dto.ClienteId = clienteId;

        var (resultado, criado) = await _sut.SalvarAsync(dto, "admin");

        Assert.False(criado);
        Assert.Equal(registroExistente.Id, resultado.Id);
        // ObterPorContaEDataAsync/ObterPorClienteEDataSemContaAsync nunca deveriam ter sido
        // chamados — a busca por Id é autoritativa, não cai pra busca por data/conta.
        _repoMock.Verify(r => r.ObterPorContaEDataAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>()), Times.Never);
        _repoMock.Verify(r => r.ObterPorClienteEDataSemContaAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>()), Times.Never);
    }

    [Fact]
    public async Task SalvarAsync_ComIdDeOutroCliente_LancaAcessoNegado()
    {
        var registroDeOutroCliente = CriarRegistroComContas(Guid.NewGuid(), new DateOnly(2026, 6, 1));
        _repoMock.Setup(r => r.ObterPorIdAsync(registroDeOutroCliente.Id)).ReturnsAsync(registroDeOutroCliente);

        var dto = CriarDto(registroDeOutroCliente.Data);
        dto.Id = registroDeOutroCliente.Id;
        dto.ClienteId = Guid.NewGuid(); // cliente diferente do dono do registro

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.SalvarAsync(dto, "admin"));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.ACESSO_NEGADO, ex.Codigo);
    }

    [Fact]
    public async Task SalvarAsync_ComIdQueNaoExiste_LancaRegistroNaoEncontrado()
    {
        var idInexistente = Guid.NewGuid();
        _repoMock.Setup(r => r.ObterPorIdAsync(idInexistente)).ReturnsAsync((RegistroDiario?)null);

        var dto = CriarDto();
        dto.Id = idInexistente;

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.SalvarAsync(dto, "admin"));

        Assert.Equal(404, ex.StatusCode);
        Assert.Equal(CodigoRetorno.REGISTRO_NAO_ENCONTRADO, ex.Codigo);
    }

    [Fact]
    public async Task SalvarAsync_ComIdEDataDivergente_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var registroExistente = CriarRegistroComContas(clienteId, new DateOnly(2026, 6, 1));
        _repoMock.Setup(r => r.ObterPorIdAsync(registroExistente.Id)).ReturnsAsync(registroExistente);

        var dto = CriarDto(new DateOnly(2026, 6, 2)); // data diferente da do registro encontrado
        dto.Id = registroExistente.Id;
        dto.ClienteId = clienteId;

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.SalvarAsync(dto, "admin"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task SalvarAsync_ContasComVencimentoNoDia_MarcaComoPagas()
    {
        var clienteId = Guid.NewGuid();
        var hoje = DataLocalHelper.Hoje();
        var registroExistente = CriarRegistroComContas(clienteId, hoje);

        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(clienteId, hoje))
            .ReturnsAsync(registroExistente);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()))
            .ReturnsAsync((RegistroDiario r) => r);

        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId,
            Data = hoje,
            ContasReceber = registroExistente.ContasReceber
                .Select(c => new ContaProvisionadaDto { Descricao = c.Descricao, Valor = c.Valor, DataVencimento = c.DataVencimento, Pago = c.Pago })
                .ToList(),
            ContasPagar = registroExistente.ContasPagar
                .Select(c => new ContaProvisionadaDto { Descricao = c.Descricao, Valor = c.Valor, DataVencimento = c.DataVencimento, Pago = c.Pago })
                .ToList(),
            Entradas = new(),
            Saidas = new(),
        };

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        Assert.True(resultado.ContasReceber[0].Pago);
        Assert.True(resultado.ContasPagar[0].Pago);
    }

    [Fact]
    public async Task SalvarAsync_DtoSemUmaContaReceberQueExistiaAntes_RemoveEla()
    {
        // Reproduz o fluxo da tela de Contas: o front le a lista atual, remove o item que o
        // usuario quer excluir, e reenvia a lista resultante inteira.
        var clienteId = Guid.NewGuid();
        var hoje = DataLocalHelper.Hoje();
        var registroExistente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = hoje,
            Entradas = new(), Saidas = new(), ContasPagar = new(),
            ContasReceber = new List<ContaProvisionada>
            {
                new() { Descricao = "Pollye", Valor = 600m, DataVencimento = new DateOnly(2026, 9, 10), Pago = false },
            },
            SaldoFinal = 0m,
        };

        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(clienteId, hoje)).ReturnsAsync(registroExistente);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId, Data = hoje, Entradas = new(), Saidas = new(),
            ContasReceber = new(), // front manda a lista ja sem o item excluido
            ContasPagar = new(),
        };

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        Assert.Empty(resultado.ContasReceber);
        _repoMock.Verify(r => r.AtualizarAsync(It.Is<RegistroDiario>(rd => rd.ContasReceber.Count == 0)), Times.Once);
    }

    [Fact]
    public async Task SalvarAsync_ExcluirContaDeRegistroSemContaVinculada_NaoAfetaRegistroDeOutraContaNoMesmoDia()
    {
        // Bug real relatado em produção: o cliente tem DOIS RegistroDiario na mesma data — um
        // "sem conta vinculada" (onde a ocorrência recorrente "Pollye" vive, ContaBancariaId=null)
        // e outro de uma conta de verdade (Nubank, com lançamentos reais do dia). Resolver a conta
        // ANTES de localizar o registro existente (ResolverContaPadrao(dto.ContaBancariaId=null)
        // caindo na "primeira conta ativa") fazia a exclusão mirar e sobrescrever o registro do
        // Nubank — apagando as entradas/saídas reais dele — em vez do registro sem conta.
        var clienteId = Guid.NewGuid();
        var nubankId = Guid.NewGuid();
        var hoje = new DateOnly(2026, 9, 10);

        var registroNubank = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = nubankId, Data = hoje,
            Entradas = new List<ItemFinanceiro> { new() { Descricao = "Venda real", Valor = 100m, Categoria = "Vendas", TipoCusto = "Receita" } },
            Saidas = new(), ContasReceber = new(), ContasPagar = new(),
            Inicio = 481.93m, SaldoFinal = 581.93m,
        };
        var registroSemConta = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = null, Data = hoje,
            Entradas = new(), Saidas = new(), ContasPagar = new(),
            ContasReceber = new List<ContaProvisionada> { new() { Descricao = "Pollye", Valor = 600m, DataVencimento = hoje, Pago = false } },
            SaldoFinal = 0m,
        };

        _contaBancariaMock.Setup(c => c.ListarPorClienteAsync(clienteId))
            .ReturnsAsync(new List<ContaBancaria> { new() { Id = nubankId, Tipo = "ContaCorrente", Ativa = true } });
        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(clienteId, hoje)).ReturnsAsync(registroSemConta);
        _repoMock.Setup(r => r.ObterPorContaEDataAsync(nubankId, hoje)).ReturnsAsync(registroNubank);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        // Front envia o registro SEM conta (contaBancariaId ausente), já sem a Pollye na lista.
        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId, Data = hoje, Entradas = new(), Saidas = new(),
            ContasReceber = new(), ContasPagar = new(),
        };

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        Assert.Equal(registroSemConta.Id, resultado.Id);
        Assert.Empty(resultado.ContasReceber);
        // O registro do Nubank nunca é tocado — suas entradas reais continuam lá.
        _repoMock.Verify(r => r.AtualizarAsync(It.Is<RegistroDiario>(rd => rd.Id == registroNubank.Id)), Times.Never);
        Assert.Single(registroNubank.Entradas);
    }

    [Fact]
    public async Task SalvarAsync_RemoveOcorrenciaDeContaRecorrente_RegistraDispensaPraNaoSerRecriada()
    {
        // Bug real relatado em produção: excluir uma ocorrência de conta recorrente (RecorrenciaId
        // preenchido) "funcionava" (sem erro), mas a próxima vez que a lista era recarregada a
        // MaterializarMesAtualAsync recriava a mesma ocorrência, porque só via "não existe nos
        // registros atuais" — sem saber que foi uma exclusão deliberada. SalvarAsync precisa
        // avisar o RecorrenciaService pra essa ocorrência não ser recriada.
        var clienteId = Guid.NewGuid();
        var recorrenciaId = Guid.NewGuid();
        var hoje = DataLocalHelper.Hoje();
        var vencimento = new DateOnly(2026, 9, 10);
        var registroExistente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = hoje,
            Entradas = new(), Saidas = new(), ContasPagar = new(),
            ContasReceber = new List<ContaProvisionada>
            {
                new() { Descricao = "Pollye", Valor = 600m, DataVencimento = vencimento, Pago = false, RecorrenciaId = recorrenciaId },
            },
            SaldoFinal = 0m,
        };

        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(clienteId, hoje)).ReturnsAsync(registroExistente);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId, Data = hoje, Entradas = new(), Saidas = new(),
            ContasReceber = new(), ContasPagar = new(),
        };

        await _sut.SalvarAsync(dto, "admin");

        _recorrenciaMock.Verify(r => r.DispensarOcorrenciaAsync(clienteId, recorrenciaId, vencimento), Times.Once);
    }

    [Fact]
    public async Task SalvarAsync_MantemContaRecorrenteQueContinuaNaLista_NaoRegistraDispensa()
    {
        // Mesma conta recorrente, mas só editada (não excluída) — não é uma dispensa.
        var clienteId = Guid.NewGuid();
        var recorrenciaId = Guid.NewGuid();
        var hoje = DataLocalHelper.Hoje();
        var vencimento = new DateOnly(2026, 9, 10);
        var registroExistente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = hoje,
            Entradas = new(), Saidas = new(), ContasPagar = new(),
            ContasReceber = new List<ContaProvisionada>
            {
                new() { Descricao = "Pollye", Valor = 600m, DataVencimento = vencimento, Pago = false, RecorrenciaId = recorrenciaId },
            },
            SaldoFinal = 0m,
        };

        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(clienteId, hoje)).ReturnsAsync(registroExistente);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId, Data = hoje, Entradas = new(), Saidas = new(),
            // Igual ao fluxo real de edição em ClientContasPage.tsx: o front preserva recorrenciaId
            // via spread ({ ...c, descricao: novo, valor: novo }) e reenvia ele no payload.
            ContasReceber = new List<ContaProvisionadaDto>
            {
                new() { Descricao = "Pollye editada", Valor = 650m, DataVencimento = vencimento, Pago = false, RecorrenciaId = recorrenciaId },
            },
            ContasPagar = new(),
        };

        await _sut.SalvarAsync(dto, "admin");

        _recorrenciaMock.Verify(r => r.DispensarOcorrenciaAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateOnly>()), Times.Never);
    }

    [Fact]
    public async Task SalvarAsync_Novo_ChamaAuditCriacao()
    {
        var clienteId = Guid.NewGuid();
        var hoje = DataLocalHelper.Hoje();

        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(clienteId, hoje)).ReturnsAsync((RegistroDiario?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = new CriarRegistroDto { ClienteId = clienteId, Data = hoje, Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new() };
        await _sut.SalvarAsync(dto, "admin");

        _auditMock.Verify(a => a.LogAsync(
            clienteId, It.IsAny<Guid>(), "RegistroDiario", "Criacao",
            It.IsAny<string>(), null, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task SalvarAsync_ComLancamentoDeTransferencia_PreservaTransferenciaIdNoRoundTrip()
    {
        // Um dia com um lançamento de transferência já salvo (TransferenciaService) precisa
        // sobreviver a um resave normal da tela de Caixa sem perder o vínculo — senão o Estornar
        // não consegue mais achar a ponta certa pra remover (TransferenciaId some silenciosamente).
        var clienteId = Guid.NewGuid();
        var hoje = DataLocalHelper.Hoje();
        var transferenciaId = Guid.NewGuid();

        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(clienteId, hoje)).ReturnsAsync((RegistroDiario?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId,
            Data = hoje,
            Entradas = new List<ItemFinanceiroDto>
            {
                new() { Descricao = "Transferência recebida", Valor = 500m, Categoria = "Transferência", TipoCusto = "Transferencia", TransferenciaId = transferenciaId },
            },
            Saidas = new(),
            ContasReceber = new(),
            ContasPagar = new(),
        };

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        var entrada = Assert.Single(resultado.Entradas);
        Assert.Equal(transferenciaId, entrada.TransferenciaId);
    }

    [Fact]
    public async Task SalvarAsync_ContaDuplicadaNaoAjustaSaldo()
    {
        var clienteId = Guid.NewGuid();
        var hoje = DataLocalHelper.Hoje();
        var contaId = Guid.NewGuid();
        var registroExistente = new RegistroDiario
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            ContaBancariaId = contaId,
            Data = hoje.AddDays(-1),
            Entradas = new(),
            Saidas = new(),
            ContasPagar = new List<ContaProvisionada>
            {
                new() { Descricao = "Fornecedor X", Valor = 300m, DataVencimento = hoje, Pago = true, DataBaixa = hoje.AddDays(-1), ContaBancariaId = contaId },
            },
            ContasReceber = new(),
            SaldoFinal = 700m,
            CriadoEm = DateTime.UtcNow,
            SalvoEm = DateTime.UtcNow,
        };

        _contaBancariaMock.Setup(c => c.ListarPorClienteAsync(clienteId))
            .ReturnsAsync(new List<ContaBancaria> { new() { Id = contaId, ClienteId = clienteId, Tipo = "ContaCorrente", Ativa = true } });
        _repoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario> { registroExistente });
        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(clienteId, hoje)).ReturnsAsync((RegistroDiario?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId,
            Data = hoje,
            Entradas = new(),
            Saidas = new(),
            ContasReceber = new(),
            ContasPagar = new List<ContaProvisionadaDto>
            {
                new() { Descricao = "Fornecedor X", Valor = 300m, DataVencimento = hoje, Pago = true, ContaBancariaId = contaId },
            },
            Inicio = 1000m,
            SaldoFinal = 1000m,
        };

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        Assert.False(resultado.ContasPagar[0].Pago);
        Assert.Equal(1000m, resultado.SaldoFinal);
    }

    [Fact]
    public async Task ListarPorClienteAsync_ChamaRecorrencia()
    {
        var clienteId = Guid.NewGuid();
        _repoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>());

        await _sut.ListarPorClienteAsync(clienteId, clienteId, "cliente");

        _recorrenciaMock.Verify(r => r.MaterializarMesAtualAsync(clienteId), Times.Once);
    }

    [Fact]
    public async Task SalvarAsync_SaidaSemCategoria_LancaExcecao()
    {
        var dto = new CriarRegistroDto
        {
            ClienteId = Guid.NewGuid(),
            Data = DataLocalHelper.Hoje(),
            Entradas = new(),
            Saidas = new() { new ItemFinanceiroSaidaDto { Descricao = "Compra", Valor = 50m, Categoria = null! } },
            ContasReceber = new(), ContasPagar = new(), SaldoFinal = 0m,
        };
        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.SalvarAsync(dto, "tester"));
        Assert.Equal(400, ex.StatusCode);
    }

    // --- existing tests preserved below ---

    [Fact]
    public async Task Salvar_DataFutura_LancaDataFutura()
    {
        var dto = CriarDto(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.SalvarAsync(dto, "joao"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DATA_FUTURA, ex.Codigo);
    }

    [Fact]
    public async Task Salvar_RegistroNovo_RetornaCriadoTrue()
    {
        var dto = CriarDto();
        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(dto.ClienteId, dto.Data)).ReturnsAsync((RegistroDiario?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var (resultado, criado) = await _sut.SalvarAsync(dto, "joao");

        Assert.True(criado);
        Assert.Equal(dto.SaldoFinal, resultado.SaldoFinal);
        Assert.Equal(dto.ClienteId, resultado.ClienteId);
    }

    [Fact]
    public async Task Salvar_RegistroExistente_RetornaCriadoFalse()
    {
        var dto = CriarDto();
        var existente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = dto.ClienteId, Data = dto.Data,
            SaldoFinal = 0, Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow
        };
        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(dto.ClienteId, dto.Data)).ReturnsAsync(existente);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var (resultado, criado) = await _sut.SalvarAsync(dto, "joao");

        Assert.False(criado);
        Assert.Equal(dto.SaldoFinal, resultado.SaldoFinal);
    }

    [Fact]
    public async Task Excluir_SemMotivo_LancaMotivoObrigatorio()
    {
        var clienteId = Guid.NewGuid();
        var data = DataLocalHelper.Hoje();

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ExcluirAsync(clienteId, data, null, "", clienteId, "cliente"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.MOTIVO_OBRIGATORIO, ex.Codigo);
    }

    [Fact]
    public async Task Excluir_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var clienteId = Guid.NewGuid();
        var usuarioLogadoId = Guid.NewGuid();
        var data = DataLocalHelper.Hoje();

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ExcluirAsync(clienteId, data, null, "motivo", usuarioLogadoId, "cliente"));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.ACESSO_NEGADO, ex.Codigo);
    }

    [Fact]
    public async Task Listar_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var clienteId = Guid.NewGuid();
        var usuarioLogadoId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ListarPorClienteAsync(clienteId, usuarioLogadoId, "cliente"));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.ACESSO_NEGADO, ex.Codigo);
    }

    [Fact]
    public async Task Listar_Admin_RetornaLista()
    {
        var clienteId = Guid.NewGuid();
        var registros = new List<RegistroDiario>
        {
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId,
                Data = DataLocalHelper.Hoje(),
                Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
                CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow
            }
        };
        _repoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(registros);

        var resultado = await _sut.ListarPorClienteAsync(clienteId, Guid.NewGuid(), "admin");

        Assert.Single(resultado);
    }

    [Fact]
    public async Task Listar_ClienteAcessandoProprioId_RetornaLista()
    {
        var clienteId = Guid.NewGuid();
        var registros = new List<RegistroDiario>
        {
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId,
                Data = DataLocalHelper.Hoje(),
                Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
                CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow
            }
        };
        _repoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(registros);

        var resultado = await _sut.ListarPorClienteAsync(clienteId, clienteId, "cliente");

        Assert.Single(resultado);
    }

    [Fact]
    public async Task ObterPorData_Admin_RetornaRegistro()
    {
        var clienteId = Guid.NewGuid();
        var data = DataLocalHelper.Hoje();
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = data,
            Entradas = new List<ItemFinanceiro> { new() { Descricao = "Caixa", Valor = 10m } },
            Saidas = new List<ItemFinanceiroSaida> { new() { Descricao = "Saida", Valor = 10m } },
            ContasReceber = new List<ContaProvisionada> { new() { Descricao = "CR", Valor = 5m } },
            ContasPagar = new List<ContaProvisionada> { new() { Descricao = "CP", Valor = 3m } },
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow
        };
        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(clienteId, data)).ReturnsAsync(registro);

        var resultado = await _sut.ObterPorDataAsync(clienteId, data, Guid.NewGuid(), "admin");

        Assert.Equal(clienteId, resultado.ClienteId);
    }

    [Fact]
    public async Task ObterPorData_ClienteAcessandoProprioId_RetornaRegistro()
    {
        var clienteId = Guid.NewGuid();
        var data = DataLocalHelper.Hoje();
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = data,
            Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow
        };
        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(clienteId, data)).ReturnsAsync(registro);

        var resultado = await _sut.ObterPorDataAsync(clienteId, data, clienteId, "cliente");

        Assert.Equal(clienteId, resultado.ClienteId);
    }

    [Fact]
    public async Task ObterPorData_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var clienteId = Guid.NewGuid();
        var usuarioLogadoId = Guid.NewGuid();
        var data = DataLocalHelper.Hoje();

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ObterPorDataAsync(clienteId, data, usuarioLogadoId, "cliente"));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.ACESSO_NEGADO, ex.Codigo);
    }

    // Fase 1.13: não ter lançado nada ainda numa data não é mais um erro — devolve um registro
    // "vazio" (200), nunca 404. O frontend não deve mais precisar tratar 404 como controle de fluxo.
    [Fact]
    public async Task ObterPorData_RegistroNaoEncontrado_DevolveRegistroVazioComSaldoDaContaAnterior()
    {
        var clienteId = Guid.NewGuid();
        var contaId = Guid.NewGuid();
        var data = DataLocalHelper.Hoje();
        _contaBancariaMock.Setup(c => c.ListarPorClienteAsync(clienteId))
            .ReturnsAsync(new List<ContaBancaria> { new() { Id = contaId, Tipo = "ContaCorrente", Ativa = true, SaldoInicial = 500m } });
        _repoMock.Setup(r => r.ObterPorContaEDataAsync(contaId, data)).ReturnsAsync((RegistroDiario?)null);
        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(clienteId, data)).ReturnsAsync((RegistroDiario?)null);
        var registroAnterior = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data.AddDays(-1),
            Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
            SaldoFinal = 777m, CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        _repoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registroAnterior });

        var resultado = await _sut.ObterPorDataAsync(clienteId, data, clienteId, "cliente", contaId);

        Assert.Equal(Guid.Empty, resultado.Id);
        Assert.Equal(777m, resultado.Inicio);
        Assert.Equal(777m, resultado.SaldoFinal);
        Assert.Empty(resultado.Entradas);
        Assert.Empty(resultado.Saidas);
    }

    // [CRÍTICO, 4ª vez]: tela do Caixa na conta Nubank, um item redirecionado pra conta C6 (que
    // ainda não tem registro nesse dia). O GET que a tela faz pra "existe algo hoje na C6?" não
    // pode, na ausência de registro da C6, devolver o registro de OUTRA conta (Nubank) do mesmo
    // dia — isso faz o Caixa achar que esse OUTRO registro É o da C6 e mesclar tudo nele ao salvar
    // (saldo inicial errado + itens de uma conta vazando pra outra). O fallback "qualquer conta,
    // mesma data" só é válido quando NENHUMA conta específica foi pedida.
    [Fact]
    public async Task ObterPorData_ContaEspecificaSemRegistroMasOutraContaTemRegistroNaMesmaData_NaoMisturaComOutraConta()
    {
        var clienteId = Guid.NewGuid();
        var contaNubankId = Guid.NewGuid();
        var contaC6Id = Guid.NewGuid();
        var data = DataLocalHelper.Hoje();

        _contaBancariaMock.Setup(c => c.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria>
        {
            new() { Id = contaNubankId, Tipo = "ContaCorrente", Nome = "Nubank", Ativa = true, SaldoInicial = 0m },
            new() { Id = contaC6Id, Tipo = "ContaCorrente", Nome = "C6", Ativa = true, SaldoInicial = 50m },
        });

        // Nubank já tem registro hoje (2 itens) — é esse registro que o fallback ambíguo acha.
        var registroNubank = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaNubankId, Data = data, Inicio = 1000m,
            Entradas = new() { new() { Descricao = "Venda 1", Valor = 300m } },
            Saidas = new() { new() { Descricao = "Mercado", Valor = 100m, Categoria = "Insumos/Mercadoria" } },
            ContasReceber = new(), ContasPagar = new(), SaldoFinal = 1200m,
        };

        // C6 nunca teve registro — nem hoje, nem antes.
        _repoMock.Setup(r => r.ObterPorContaEDataAsync(contaC6Id, data)).ReturnsAsync((RegistroDiario?)null);
        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(clienteId, data)).ReturnsAsync(registroNubank);
        _repoMock.Setup(r => r.ListarPorContaAsync(contaC6Id)).ReturnsAsync(new List<RegistroDiario>());

        var resultado = await _sut.ObterPorDataAsync(clienteId, data, clienteId, "cliente", contaC6Id);

        Assert.Equal(Guid.Empty, resultado.Id);
        Assert.Equal(contaC6Id, resultado.ContaBancariaId);
        Assert.Equal(50m, resultado.Inicio); // saldo inicial da C6, nunca o Inicio/saldo do Nubank
        Assert.Empty(resultado.Entradas);
        Assert.Empty(resultado.Saidas);
    }

    [Fact]
    public async Task ObterPorData_RegistroNaoEncontradoESemRegistroAnterior_UsaSaldoInicialDaConta()
    {
        var clienteId = Guid.NewGuid();
        var contaId = Guid.NewGuid();
        var data = DataLocalHelper.Hoje();
        _contaBancariaMock.Setup(c => c.ListarPorClienteAsync(clienteId))
            .ReturnsAsync(new List<ContaBancaria> { new() { Id = contaId, Tipo = "ContaCorrente", Ativa = true, SaldoInicial = 100m } });
        _repoMock.Setup(r => r.ObterPorContaEDataAsync(contaId, data)).ReturnsAsync((RegistroDiario?)null);
        _repoMock.Setup(r => r.ObterPorClienteEDataAsync(clienteId, data)).ReturnsAsync((RegistroDiario?)null);
        _repoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario>());

        var resultado = await _sut.ObterPorDataAsync(clienteId, data, clienteId, "cliente", contaId);

        Assert.Equal(100m, resultado.Inicio);
    }

    [Fact]
    public async Task Excluir_RegistroNaoEncontrado_LancaRegistroNaoEncontrado()
    {
        var clienteId = Guid.NewGuid();
        var data = DataLocalHelper.Hoje();
        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(clienteId, data)).ReturnsAsync((RegistroDiario?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ExcluirAsync(clienteId, data, null, "motivo", Guid.NewGuid(), "admin"));

        Assert.Equal(404, ex.StatusCode);
        Assert.Equal(CodigoRetorno.REGISTRO_NAO_ENCONTRADO, ex.Codigo);
    }

    [Fact]
    public async Task Excluir_Admin_MarcaComoExcluido()
    {
        var clienteId = Guid.NewGuid();
        var data = DataLocalHelper.Hoje();
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = data,
            Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow
        };
        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(clienteId, data)).ReturnsAsync(registro);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        await _sut.ExcluirAsync(clienteId, data, null, "motivo teste", Guid.NewGuid(), "admin");

        _repoMock.Verify(r => r.AtualizarAsync(It.Is<RegistroDiario>(x => x.Excluido && x.MotivoExclusao == "motivo teste")), Times.Once);
    }

    [Fact]
    public async Task Excluir_ComContaBancariaId_ExcluiSoORegistroDaquelaContaNuncaOutra()
    {
        // Duas contas têm registro no mesmo dia — excluir com contaBancariaId explícita nunca pode
        // apagar (ou nem olhar para) o registro da outra conta, mesmo que ela seja a "padrão".
        var clienteId = Guid.NewGuid();
        var data = DataLocalHelper.Hoje();
        var contaCaixa = new ContaBancaria { Id = Guid.NewGuid(), Tipo = "Caixa", Ativa = true };
        var contaNubank = new ContaBancaria { Id = Guid.NewGuid(), Tipo = "ContaCorrente", Ativa = true };
        var registroNubank = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = data, ContaBancariaId = contaNubank.Id,
            Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow
        };

        _contaBancariaMock.Setup(c => c.ListarPorClienteAsync(clienteId))
            .ReturnsAsync(new List<ContaBancaria> { contaCaixa, contaNubank });
        _repoMock.Setup(r => r.ObterPorContaEDataAsync(contaNubank.Id, data)).ReturnsAsync(registroNubank);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        await _sut.ExcluirAsync(clienteId, data, contaNubank.Id, "motivo", Guid.NewGuid(), "admin");

        _repoMock.Verify(r => r.AtualizarAsync(It.Is<RegistroDiario>(x => x.Id == registroNubank.Id && x.Excluido)), Times.Once);
        _repoMock.Verify(r => r.ObterPorContaEDataAsync(contaCaixa.Id, It.IsAny<DateOnly>()), Times.Never);
    }

    // Item 2: excluir um dia não pode deixar os dias seguintes (inclusive futuros, ex.: uma conta a
    // pagar já materializada com antecedência) com Inicio/SaldoFinal baseados num dia que não existe
    // mais — tem que recalcular a cadeia a partir do último registro ativo anterior.
    [Fact]
    public async Task Excluir_RegistroComDiaSeguinteDaMesmaConta_RecalculaInicioESaldoFinalDoSeguinte()
    {
        var clienteId = Guid.NewGuid();
        var contaId = Guid.NewGuid();
        var ontem = DataLocalHelper.Hoje().AddDays(-2);
        var hoje = DataLocalHelper.Hoje().AddDays(-1);
        var amanha = DataLocalHelper.Hoje();

        _contaBancariaMock.Setup(c => c.ListarPorClienteAsync(clienteId))
            .ReturnsAsync(new List<ContaBancaria> { new() { Id = contaId, Tipo = "ContaCorrente", Ativa = true, SaldoInicial = 0m } });

        var registroAnterior = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = ontem,
            Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 1000m,
        };
        var registroExcluido = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = hoje, Inicio = 1000m,
            Entradas = new() { new() { Descricao = "Venda", Valor = 500m } },
            Saidas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 1500m,
        };
        // Conta a pagar já materializada com antecedência pro dia seguinte (futuro em relação ao
        // dia excluído) — herdou Inicio/SaldoFinal do dia que está sendo excluído.
        var registroFuturo = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = amanha, Inicio = 1500m,
            Entradas = new(), Saidas = new() { new() { Descricao = "Aluguel", Valor = 300m, Categoria = "Aluguel" } },
            ContasReceber = new(), ContasPagar = new(), SaldoFinal = 1200m,
        };

        _repoMock.Setup(r => r.ObterPorContaEDataAsync(contaId, hoje)).ReturnsAsync(registroExcluido);
        _repoMock.Setup(r => r.ListarPorContaAsync(contaId)).ReturnsAsync(new List<RegistroDiario> { registroAnterior });
        _repoMock.Setup(r => r.ListarPorClienteAsync(clienteId))
            .ReturnsAsync(new List<RegistroDiario> { registroAnterior, registroExcluido, registroFuturo });
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        await _sut.ExcluirAsync(clienteId, hoje, contaId, "motivo", Guid.NewGuid(), "admin");

        // Sem o dia excluído (que trazia 500 de entrada), o dia seguinte passa a partir de 1000
        // (saldo do dia anterior), não mais de 1500.
        Assert.Equal(1000m, registroFuturo.Inicio);
        Assert.Equal(700m, registroFuturo.SaldoFinal); // 1000 - 300 de saída
        _repoMock.Verify(r => r.AtualizarAsync(It.Is<RegistroDiario>(x => x.Id == registroFuturo.Id)), Times.Once);
    }

    // --- D7: baixa financeira ajusta o saldo automaticamente ---

    private (RegistroDiario existente, CriarRegistroDto dto) SetupBaixa(
        bool pagarPago, bool receberPago, bool existentePagarPago = false, bool existenteReceberPago = false,
        DateOnly? dataBaixaPagarExistente = null, DateOnly? dataBaixaReceberExistente = null)
    {
        var clienteId = Guid.NewGuid();
        var hoje = DataLocalHelper.Hoje();
        var existente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = hoje,
            Entradas = new(), Saidas = new(),
            ContasReceber = new List<ContaProvisionada>
            {
                new() { Descricao = "Venda A", Valor = 500m, Pago = existenteReceberPago, DataBaixa = dataBaixaReceberExistente },
            },
            ContasPagar = new List<ContaProvisionada>
            {
                new() { Descricao = "Fornecedor B", Valor = 200m, Pago = existentePagarPago, DataBaixa = dataBaixaPagarExistente },
            },
            // Sem entradas/saídas no dia: o saldo final do dia é só o Inicio em si (mais o ajuste da
            // baixa). O backend agora calcula SaldoFinal a partir de Inicio, nunca do valor que o
            // dto manda — então o Inicio precisa refletir o saldo de antes da baixa deste teste.
            Inicio = 1000m,
            SaldoFinal = 1000m,
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow
        };

        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId, Data = hoje, Entradas = new(), Saidas = new(),
            ContasReceber = new List<ContaProvisionadaDto>
            {
                new() { Descricao = "Venda A", Valor = 500m, Pago = receberPago,
                    DataBaixa = receberPago == existenteReceberPago ? dataBaixaReceberExistente : null },
            },
            ContasPagar = new List<ContaProvisionadaDto>
            {
                new() { Descricao = "Fornecedor B", Valor = 200m, Pago = pagarPago,
                    DataBaixa = pagarPago == existentePagarPago ? dataBaixaPagarExistente : null },
            },
            Inicio = 1000m,
            SaldoFinal = 1000m, // ignorado pelo backend — mantido só por realismo do payload
        };

        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(clienteId, hoje)).ReturnsAsync(existente);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        return (existente, dto);
    }

    [Fact]
    public async Task SalvarAsync_BaixarContaPagarPendente_ReduzSaldoESetaDataBaixa()
    {
        var hoje = DataLocalHelper.Hoje();
        var (_, dto) = SetupBaixa(pagarPago: true, receberPago: false);

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        Assert.Equal(800m, resultado.SaldoFinal); // 1000 - 200
        Assert.True(resultado.ContasPagar[0].Pago);
        Assert.Equal(hoje, resultado.ContasPagar[0].DataBaixa);
    }

    [Fact]
    public async Task SalvarAsync_BaixaComDataEValorDiferentes_UsaValorRealizadoERespeitaDataBaixaExplicita()
    {
        // Pagou com desconto (180 em vez de 200) numa data diferente da data do save — o backend
        // nunca pode sobrescrever a data escolhida nem usar o Valor original do título.
        var hoje = DataLocalHelper.Hoje();
        var dataPagamentoReal = hoje.AddDays(-3);
        var (_, dto) = SetupBaixa(pagarPago: true, receberPago: false);
        dto.ContasPagar[0].DataBaixa = dataPagamentoReal;
        dto.ContasPagar[0].ValorRealizado = 180m;

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        Assert.Equal(820m, resultado.SaldoFinal); // 1000 - 180 (nunca 1000 - 200)
        Assert.Equal(180m, resultado.ContasPagar[0].ValorRealizado);
        Assert.Equal(dataPagamentoReal, resultado.ContasPagar[0].DataBaixa); // nunca "hoje"
    }

    [Fact]
    public async Task SalvarAsync_EditaItemPorId_CasaMesmoComValorEDescricaoDiferentes()
    {
        // Fase 0.4: casar por Id tem que sobreviver a uma edição que muda valor E descrição ao
        // mesmo tempo — o heurístico antigo (descrição+valor+vencimento+conta) jamais reconheceria
        // isso como "o mesmo item" e criaria uma cópia em vez de atualizar.
        var clienteId = Guid.NewGuid();
        var data = new DateOnly(2026, 6, 1);
        var itemId = Guid.NewGuid();
        var existente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = data,
            Entradas = new(), Saidas = new(), ContasReceber = new(),
            ContasPagar = new List<ContaProvisionada>
            {
                new() { Id = itemId, Descricao = "Fornecedor B", Valor = 200m, DataVencimento = data, Pago = false },
            },
            SaldoFinal = 1000m,
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(clienteId, data)).ReturnsAsync(existente);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = CriarDto(data);
        dto.ClienteId = clienteId;
        dto.ContasPagar = new List<ContaProvisionadaDto>
        {
            new() { Id = itemId, Descricao = "Fornecedor B (renegociado)", Valor = 350m, DataVencimento = data, Pago = false },
        };
        dto.ContasReceber = new();

        var (resultado, criado) = await _sut.SalvarAsync(dto, "admin");

        Assert.False(criado);
        var item = Assert.Single(resultado.ContasPagar);
        Assert.Equal(itemId, item.Id);
        Assert.Equal("Fornecedor B (renegociado)", item.Descricao);
        Assert.Equal(350m, item.Valor);
    }

    [Fact]
    public async Task SalvarAsync_BaixarContaReceberPendente_AumentaSaldoESetaDataBaixa()
    {
        var hoje = DataLocalHelper.Hoje();
        var (_, dto) = SetupBaixa(pagarPago: false, receberPago: true);

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        Assert.Equal(1500m, resultado.SaldoFinal); // 1000 + 500
        Assert.True(resultado.ContasReceber[0].Pago);
        Assert.Equal(hoje, resultado.ContasReceber[0].DataBaixa);
    }

    [Fact]
    public async Task SalvarAsync_SalvarNovamenteSemMudarPago_NaoReajustaSaldo()
    {
        var ontem = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        // conta a pagar já estava paga e baixada; reenvia paga (sem transição)
        var (_, dto) = SetupBaixa(pagarPago: true, receberPago: false,
            existentePagarPago: true, dataBaixaPagarExistente: ontem);

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        Assert.Equal(1000m, resultado.SaldoFinal); // inalterado: idempotente
        Assert.True(resultado.ContasPagar[0].Pago);
        Assert.Equal(ontem, resultado.ContasPagar[0].DataBaixa); // mantém a data original
    }

    [Fact]
    public async Task SalvarAsync_DesfazerBaixaContaPagar_ReverteSaldoELimpaDataBaixa()
    {
        var ontem = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        // conta a pagar estava paga/baixada (saldo já tinha sido reduzido); agora desmarca
        var (_, dto) = SetupBaixa(pagarPago: false, receberPago: false,
            existentePagarPago: true, dataBaixaPagarExistente: ontem);

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        Assert.Equal(1200m, resultado.SaldoFinal); // 1000 + 200 (reverte a saída)
        Assert.False(resultado.ContasPagar[0].Pago);
        Assert.Null(resultado.ContasPagar[0].DataBaixa);
    }

    [Fact]
    public async Task SalvarAsync_BaixarContaReceberVinculadaALancamentoExistente_NaoDuplicaOSaldo()
    {
        var clienteId = Guid.NewGuid();
        var hoje = DataLocalHelper.Hoje();
        var lancamentoId = Guid.NewGuid();
        var existente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = hoje,
            // O dinheiro já está contado: entrada manual de 500 já bate com a "Venda A".
            Entradas = new() { new() { Id = lancamentoId, Descricao = "Pix recebido", Valor = 500m } },
            Saidas = new(),
            ContasReceber = new List<ContaProvisionada> { new() { Descricao = "Venda A", Valor = 500m, Pago = false } },
            ContasPagar = new(),
            // Inicio + entrada de 500 = 1500: o SaldoFinal do backend vem só daqui, nunca do dto.
            Inicio = 1000m,
            SaldoFinal = 1500m,
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId, Data = hoje,
            Entradas = new List<ItemFinanceiroDto> { new() { Id = lancamentoId, Descricao = "Pix recebido", Valor = 500m } },
            Saidas = new(),
            ContasReceber = new List<ContaProvisionadaDto>
            {
                new() { Descricao = "Venda A", Valor = 500m, Pago = true, LancamentoVinculadoId = lancamentoId },
            },
            ContasPagar = new(),
            Inicio = 1000m,
            SaldoFinal = 1500m, // ignorado pelo backend — mantido só por realismo do payload
        };
        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(clienteId, hoje)).ReturnsAsync(existente);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        Assert.Equal(1500m, resultado.SaldoFinal); // sem duplicar: não soma mais 500
        Assert.True(resultado.ContasReceber[0].Pago);
        Assert.Equal(lancamentoId, resultado.ContasReceber[0].LancamentoVinculadoId);
        Assert.Equal(hoje, resultado.ContasReceber[0].DataBaixa);
    }

    [Fact]
    public async Task SalvarAsync_DesfazerBaixaVinculada_NaoAlteraSaldo()
    {
        var clienteId = Guid.NewGuid();
        var hoje = DataLocalHelper.Hoje();
        var ontem = hoje.AddDays(-1);
        var lancamentoId = Guid.NewGuid();
        var existente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = hoje,
            Entradas = new() { new() { Id = lancamentoId, Descricao = "Pix recebido", Valor = 500m } },
            Saidas = new(),
            ContasReceber = new List<ContaProvisionada>
            {
                new() { Descricao = "Venda A", Valor = 500m, Pago = true, DataBaixa = ontem, LancamentoVinculadoId = lancamentoId },
            },
            ContasPagar = new(),
            Inicio = 1000m,
            SaldoFinal = 1500m,
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow,
        };
        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId, Data = hoje,
            Entradas = new List<ItemFinanceiroDto> { new() { Id = lancamentoId, Descricao = "Pix recebido", Valor = 500m } },
            Saidas = new(),
            ContasReceber = new List<ContaProvisionadaDto> { new() { Descricao = "Venda A", Valor = 500m, Pago = false } },
            ContasPagar = new(),
            Inicio = 1000m,
            SaldoFinal = 1500m,
        };
        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(clienteId, hoje)).ReturnsAsync(existente);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        Assert.Equal(1500m, resultado.SaldoFinal); // nada a reverter: a baixa vinculada nunca somou nada
        Assert.False(resultado.ContasReceber[0].Pago);
        Assert.Null(resultado.ContasReceber[0].DataBaixa);
        Assert.Null(resultado.ContasReceber[0].LancamentoVinculadoId);
    }

    [Fact]
    public async Task Salvar_NovoRegistroComContasReceberEPagar_MapeiaCorretamente()
    {
        var dto = new CriarRegistroDto
        {
            ClienteId = Guid.NewGuid(),
            Data = DataLocalHelper.Hoje(),
            Inicio = 100m,
            Entradas = new List<ItemFinanceiroDto> { new() { Descricao = "Caixa", Valor = 500m } },
            Saidas = new(),
            ContasReceber = new List<ContaProvisionadaDto> { new() { Descricao = "CR", Valor = 50m, DataVencimento = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)), Pago = false } },
            ContasPagar = new List<ContaProvisionadaDto> { new() { Descricao = "CP", Valor = 30m, Pago = true } },
            SaldoFinal = 520m
        };
        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(dto.ClienteId, dto.Data)).ReturnsAsync((RegistroDiario?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var (resultado, criado) = await _sut.SalvarAsync(dto, "admin");

        Assert.True(criado);
        Assert.Single(resultado.ContasReceber);
        Assert.Single(resultado.ContasPagar);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)), resultado.ContasReceber[0].DataVencimento);
        Assert.True(resultado.ContasPagar[0].Pago);
    }

    // --- C2: cobertura de caminhos de criação/auto-baixa/lista-variável ---

    [Fact]
    public async Task SalvarAsync_CriacaoComContaPagarJaPaga_AjustaSaldoESetaDataBaixa()
    {
        // Gap 1: registro NOVO (existente=null) com ContaPagar já paga deve aplicar baixa (−valor, DataBaixa setada)
        var clienteId = Guid.NewGuid();
        var hoje = DataLocalHelper.Hoje();

        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(clienteId, hoje)).ReturnsAsync((RegistroDiario?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId,
            Data = hoje,
            Entradas = new(),
            Saidas = new(),
            ContasReceber = new(),
            ContasPagar = new List<ContaProvisionadaDto>
            {
                new() { Descricao = "Fornecedor X", Valor = 300m, Pago = true },
            },
            Inicio = 1000m,
            SaldoFinal = 1000m,
        };

        var (resultado, criado) = await _sut.SalvarAsync(dto, "admin");

        Assert.True(criado);
        Assert.Equal(700m, resultado.SaldoFinal); // 1000 - 300
        Assert.True(resultado.ContasPagar[0].Pago);
        Assert.Equal(hoje, resultado.ContasPagar[0].DataBaixa);
    }

    [Fact]
    public async Task SalvarAsync_CriacaoComContaReceberJaPaga_AumentaSaldoESetaDataBaixa()
    {
        // Gap 1 (opcional): registro NOVO com ContaReceber já paga deve aplicar baixa (+valor, DataBaixa setada)
        var clienteId = Guid.NewGuid();
        var hoje = DataLocalHelper.Hoje();

        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(clienteId, hoje)).ReturnsAsync((RegistroDiario?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId,
            Data = hoje,
            Entradas = new(),
            Saidas = new(),
            ContasReceber = new List<ContaProvisionadaDto>
            {
                new() { Descricao = "Venda Y", Valor = 400m, Pago = true },
            },
            ContasPagar = new(),
            Inicio = 1000m,
            SaldoFinal = 1000m,
        };

        var (resultado, criado) = await _sut.SalvarAsync(dto, "admin");

        Assert.True(criado);
        Assert.Equal(1400m, resultado.SaldoFinal); // 1000 + 400
        Assert.True(resultado.ContasReceber[0].Pago);
        Assert.Equal(hoje, resultado.ContasReceber[0].DataBaixa);
    }

    [Fact]
    public async Task SalvarAsync_ResaveComContaJaPagaNoExistente_NaoReajustaSaldo()
    {
        // Gap 2: após criado com conta paga (existente retorna registro com pago=true) não deve reajustar
        var clienteId = Guid.NewGuid();
        var hoje = DataLocalHelper.Hoje();
        var dataBaixa = hoje.AddDays(-1);

        var existente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = hoje,
            Entradas = new(), Saidas = new(),
            ContasReceber = new(),
            ContasPagar = new List<ContaProvisionada>
            {
                new() { Descricao = "Fornecedor X", Valor = 300m, Pago = true, DataBaixa = dataBaixa },
            },
            Inicio = 700m,
            SaldoFinal = 700m, // já com o ajuste da baixa anterior aplicado
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow
        };

        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(clienteId, hoje)).ReturnsAsync(existente);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        // reenvia a mesma conta como paga (sem transição)
        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId,
            Data = hoje,
            Entradas = new(),
            Saidas = new(),
            ContasReceber = new(),
            ContasPagar = new List<ContaProvisionadaDto>
            {
                new() { Descricao = "Fornecedor X", Valor = 300m, Pago = true, DataBaixa = dataBaixa },
            },
            Inicio = 700m,
            SaldoFinal = 700m, // frontend reenvia saldo atual
        };

        var (resultado, criado) = await _sut.SalvarAsync(dto, "admin");

        Assert.False(criado);
        Assert.Equal(700m, resultado.SaldoFinal); // idempotente: não reduz de novo
        Assert.True(resultado.ContasPagar[0].Pago);
        Assert.Equal(dataBaixa, resultado.ContasPagar[0].DataBaixa); // preserva data original
    }

    [Fact]
    public async Task SalvarAsync_AutoBaixaPorVencimento_AjustaSaldoUmaVez()
    {
        // Gap 3 (primeiro save): ContaPagar com DataVencimento == dto.Data e pago=false.
        // AplicarBaixaAutomatica marca pago=true; AplicarBaixaFinanceira deve detectar transição (antes=false) e ajustar uma vez.
        var clienteId = Guid.NewGuid();
        var hoje = DataLocalHelper.Hoje();

        var existente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = hoje,
            Entradas = new(), Saidas = new(),
            ContasReceber = new(),
            ContasPagar = new List<ContaProvisionada>
            {
                new() { Descricao = "Boleto Z", Valor = 150m, DataVencimento = hoje, Pago = false },
            },
            Inicio = 1000m,
            SaldoFinal = 1000m,
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow
        };

        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(clienteId, hoje)).ReturnsAsync(existente);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId,
            Data = hoje,
            Entradas = new(),
            Saidas = new(),
            ContasReceber = new(),
            ContasPagar = new List<ContaProvisionadaDto>
            {
                new() { Descricao = "Boleto Z", Valor = 150m, DataVencimento = hoje, Pago = false },
            },
            Inicio = 1000m,
            SaldoFinal = 1000m,
        };

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        // AplicarBaixaAutomatica marca pago=true; ajuste financeiro deve ocorrer UMA vez (−150)
        Assert.Equal(850m, resultado.SaldoFinal); // 1000 - 150
        Assert.True(resultado.ContasPagar[0].Pago);
        Assert.Equal(hoje, resultado.ContasPagar[0].DataBaixa);
    }

    [Fact]
    public async Task SalvarAsync_AutoBaixaPorVencimento_SegundoSaveNaoReajusta()
    {
        // Gap 3 (segundo save): existente já tem pago=true; dto reenvia pago=false mas auto-baixa marcará true.
        // Não deve reajustar o saldo porque a transição real já ocorreu no save anterior.
        var clienteId = Guid.NewGuid();
        var hoje = DataLocalHelper.Hoje();

        var existente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = hoje,
            Entradas = new(), Saidas = new(),
            ContasReceber = new(),
            ContasPagar = new List<ContaProvisionada>
            {
                new() { Descricao = "Boleto Z", Valor = 150m, DataVencimento = hoje, Pago = true, DataBaixa = hoje },
            },
            Inicio = 850m,
            SaldoFinal = 850m, // já com o ajuste aplicado
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow
        };

        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(clienteId, hoje)).ReturnsAsync(existente);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        // frontend reenvia pago=false, mas a auto-baixa marca true novamente
        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId,
            Data = hoje,
            Entradas = new(),
            Saidas = new(),
            ContasReceber = new(),
            ContasPagar = new List<ContaProvisionadaDto>
            {
                new() { Descricao = "Boleto Z", Valor = 150m, DataVencimento = hoje, Pago = false },
            },
            Inicio = 850m,
            SaldoFinal = 850m,
        };

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        // auto-baixa marca pago=true; existente também era true → sem transição → sem reajuste
        Assert.Equal(850m, resultado.SaldoFinal); // idempotente
        Assert.True(resultado.ContasPagar[0].Pago);
    }

    [Fact]
    public async Task SalvarAsync_ListaTamanhoMaior_ContaPreExistentePagaNaoReajustaNovaNaoAjusta()
    {
        // Gap 4: existente tem 1 conta paga; dto tem 2 contas (primeira igual paga, segunda nova não paga).
        // A conta paga pré-existente NÃO é reajustada; a conta nova não paga não gera ajuste; sem erro de índice.
        var clienteId = Guid.NewGuid();
        var hoje = DataLocalHelper.Hoje();
        var dataBaixa = hoje.AddDays(-2);

        var existente = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = hoje,
            Entradas = new(), Saidas = new(),
            ContasReceber = new(),
            ContasPagar = new List<ContaProvisionada>
            {
                new() { Descricao = "Conta Antiga", Valor = 100m, Pago = true, DataBaixa = dataBaixa },
            },
            Inicio = 900m,
            SaldoFinal = 900m,
            CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow
        };

        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(clienteId, hoje)).ReturnsAsync(existente);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId,
            Data = hoje,
            Entradas = new(),
            Saidas = new(),
            ContasReceber = new(),
            ContasPagar = new List<ContaProvisionadaDto>
            {
                new() { Descricao = "Conta Antiga", Valor = 100m, Pago = true, DataBaixa = dataBaixa },
                new() { Descricao = "Conta Nova", Valor = 50m, Pago = false },
            },
            Inicio = 900m,
            SaldoFinal = 900m,
        };

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        Assert.Equal(900m, resultado.SaldoFinal); // sem reajuste: conta paga já estava paga; conta nova está não-paga
        Assert.Equal(2, resultado.ContasPagar.Count);
        Assert.True(resultado.ContasPagar[0].Pago);
        Assert.Equal(dataBaixa, resultado.ContasPagar[0].DataBaixa); // data original preservada
        Assert.False(resultado.ContasPagar[1].Pago);
        Assert.Null(resultado.ContasPagar[1].DataBaixa);
    }

    [Fact]
    public async Task Salvar_ComCategoriaNaSaida_MapeiaCorretamente()
    {
        var dto = new CriarRegistroDto
        {
            ClienteId = Guid.NewGuid(),
            Data = DataLocalHelper.Hoje(),
            Inicio = 0m,
            Entradas = new(),
            Saidas = new List<ItemFinanceiroSaidaDto>
            {
                new() { Descricao = "Aluguel", Valor = 1200m, Categoria = "Administrativas", Subcategoria = "Aluguel" }
            },
            ContasReceber = new(),
            ContasPagar = new(),
            SaldoFinal = 0m
        };
        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(dto.ClienteId, dto.Data))
                 .ReturnsAsync((RegistroDiario?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
                 .ReturnsAsync((RegistroDiario r) => r);

        var (resultado, _) = await _sut.SalvarAsync(dto, "joao");

        Assert.Single(resultado.Saidas);
        Assert.Equal("Administrativas", resultado.Saidas[0].Categoria);
        Assert.Equal("Aluguel", resultado.Saidas[0].Subcategoria);
    }

    // ── ResolverContaPadraoAsync (via SalvarAsync) ──────────────────────────────────────────────
    // Cobre o bug: baixa de conta a pagar/receber, ou qualquer save sem ContaBancariaId explícito,
    // não pode mais exigir uma conta do tipo "Caixa" especificamente — Caixa é só uma preferência
    // de fallback quando existe; qualquer conta ativa do cliente deve servir.

    [Fact]
    public async Task SalvarAsync_SemContaBancariaIdEClienteSemContaCaixa_UsaOutraContaAtivaComoFallback()
    {
        var clienteId = Guid.NewGuid();
        var hoje = DataLocalHelper.Hoje();
        var c6Bank = new ContaBancaria { Id = Guid.NewGuid(), ClienteId = clienteId, Nome = "C6 Bank", Tipo = "ContaCorrente", Ativa = true };
        _contaBancariaMock.Setup(c => c.ListarPorClienteAsync(clienteId))
            .ReturnsAsync(new List<ContaBancaria> { c6Bank });

        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(clienteId, hoje)).ReturnsAsync((RegistroDiario?)null);
        RegistroDiario? criado = null;
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()))
            .Callback<RegistroDiario>(r => criado = r).ReturnsAsync((RegistroDiario r) => r);

        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId, Data = hoje, Entradas = new(), Saidas = new(),
            ContasReceber = new List<ContaProvisionadaDto> { new() { Descricao = "Pedro Personal", Valor = 350m, Pago = true } },
            ContasPagar = new(),
        };

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        Assert.Equal(c6Bank.Id, resultado.ContaBancariaId);
        Assert.NotNull(criado);
        Assert.Equal(c6Bank.Id, criado!.ContaBancariaId);
    }

    [Fact]
    public async Task SalvarAsync_ClienteSemNenhumaContaBancaria_LancaDadosInvalidosSemMencionarCaixa()
    {
        var clienteId = Guid.NewGuid();
        var hoje = DataLocalHelper.Hoje();
        _contaBancariaMock.Setup(c => c.ListarPorClienteAsync(clienteId))
            .ReturnsAsync(new List<ContaBancaria>());

        var dto = new CriarRegistroDto { ClienteId = clienteId, Data = hoje, Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new() };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.SalvarAsync(dto, "admin"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
        Assert.DoesNotContain("Caixa", ex.Message);
    }

    [Fact]
    public async Task SalvarAsync_SemContaBancariaIdEClienteComContaCaixaEOutras_PrefereACaixa()
    {
        var clienteId = Guid.NewGuid();
        var hoje = DataLocalHelper.Hoje();
        var contaCorrente = new ContaBancaria { Id = Guid.NewGuid(), ClienteId = clienteId, Nome = "Nubank", Tipo = "ContaCorrente", Ativa = true };
        var caixa = new ContaBancaria { Id = Guid.NewGuid(), ClienteId = clienteId, Nome = "Caixa", Tipo = "Caixa", Ativa = true };
        _contaBancariaMock.Setup(c => c.ListarPorClienteAsync(clienteId))
            .ReturnsAsync(new List<ContaBancaria> { contaCorrente, caixa });

        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(clienteId, hoje)).ReturnsAsync((RegistroDiario?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = new CriarRegistroDto { ClienteId = clienteId, Data = hoje, Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new() };
        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        Assert.Equal(caixa.Id, resultado.ContaBancariaId);
    }

    [Fact]
    public async Task SalvarAsync_ContaBancariaIdExplicitoPertenceAoClienteEAtiva_UsaAConta()
    {
        var clienteId = Guid.NewGuid();
        var hoje = DataLocalHelper.Hoje();
        var contaEscolhida = new ContaBancaria { Id = Guid.NewGuid(), ClienteId = clienteId, Nome = "C6 Bank", Tipo = "ContaCorrente", Ativa = true };
        _contaBancariaMock.Setup(c => c.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria> { contaEscolhida });

        _repoMock.Setup(r => r.ObterPorContaEDataAsync(contaEscolhida.Id, hoje)).ReturnsAsync((RegistroDiario?)null);
        _repoMock.Setup(r => r.ObterPorClienteEDataSemContaAsync(clienteId, hoje)).ReturnsAsync((RegistroDiario?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId, ContaBancariaId = contaEscolhida.Id, Data = hoje,
            Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
        };

        var (resultado, _) = await _sut.SalvarAsync(dto, "admin");

        Assert.Equal(contaEscolhida.Id, resultado.ContaBancariaId);
    }

    [Fact]
    public async Task SalvarAsync_ContaBancariaIdExplicitoDeOutroCliente_LancaAcessoNegado()
    {
        // Payload adulterado trocando a conta por uma que não pertence ao cliente da requisição.
        var clienteId = Guid.NewGuid();
        var hoje = DataLocalHelper.Hoje();
        var contaDeOutroCliente = Guid.NewGuid();
        _contaBancariaMock.Setup(c => c.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria>());

        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId, ContaBancariaId = contaDeOutroCliente, Data = hoje,
            Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.SalvarAsync(dto, "admin"));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.ACESSO_NEGADO, ex.Codigo);
        _repoMock.Verify(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }

    [Fact]
    public async Task SalvarAsync_ContaBancariaIdExplicitoInativa_LancaContaInativa()
    {
        var clienteId = Guid.NewGuid();
        var hoje = DataLocalHelper.Hoje();
        var contaInativa = new ContaBancaria { Id = Guid.NewGuid(), ClienteId = clienteId, Nome = "Conta Encerrada", Tipo = "ContaCorrente", Ativa = false };
        _contaBancariaMock.Setup(c => c.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria> { contaInativa });

        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId, ContaBancariaId = contaInativa.Id, Data = hoje,
            Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.SalvarAsync(dto, "admin"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.CONTA_INATIVA, ex.Codigo);
    }

    [Fact]
    public async Task SalvarAsync_BaixaComContaBancariaIdDeOutroCliente_LancaAcessoNegado()
    {
        // Bug corrigido: a ContaBancariaId de um item de ContasReceber/ContasPagar (baixa escolhendo
        // conta diferente da vinculada ao título, ex. modal "Confirmar recebimento") não era validada
        // contra o cliente da requisição.
        var clienteId = Guid.NewGuid();
        var hoje = DataLocalHelper.Hoje();
        var contaDoCliente = new ContaBancaria { Id = Guid.NewGuid(), ClienteId = clienteId, Nome = "Nubank", Tipo = "ContaCorrente", Ativa = true };
        var contaDeOutroCliente = Guid.NewGuid();
        _contaBancariaMock.Setup(c => c.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria> { contaDoCliente });

        var dto = new CriarRegistroDto
        {
            ClienteId = clienteId, Data = hoje, Entradas = new(), Saidas = new(),
            ContasReceber = new List<ContaProvisionadaDto>
            {
                new() { Descricao = "Cliente X", Valor = 500m, Pago = true, ContaBancariaId = contaDeOutroCliente },
            },
            ContasPagar = new(),
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.SalvarAsync(dto, "admin"));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.ACESSO_NEGADO, ex.Codigo);
        _repoMock.Verify(r => r.AdicionarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }

    // Item 3.3: o motor de vínculo roda depois de salvar o Caixa, igual já roda depois de importar.
    [Fact]
    public async Task SalvarAsync_RegistroNovo_PreencheTotalSugestoesVinculoDoMotorDeConciliacao()
    {
        var dto = CriarDto();
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        _conciliacaoMock.Setup(c => c.ListarSugestoesAsync(
            dto.ClienteId, dto.Data, dto.Data, dto.ClienteId, "cliente", It.IsAny<Guid?>()))
            .ReturnsAsync(new List<CaixaDiario.API.DTOs.Conciliacao.SugestaoVinculoDto>
            {
                new() { ContaProvisionadaId = Guid.NewGuid() },
                new() { ContaProvisionadaId = Guid.NewGuid() },
            });

        var (resultado, _) = await _sut.SalvarAsync(dto, "cliente");

        Assert.Equal(2, resultado.TotalSugestoesVinculo);
    }

    [Fact]
    public async Task SalvarAsync_MotorDeConciliacaoFalha_NaoDerrubaOSalvamento()
    {
        var dto = CriarDto();
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        _conciliacaoMock.Setup(c => c.ListarSugestoesAsync(
            It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>()))
            .ThrowsAsync(new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "erro qualquer"));

        var (resultado, criado) = await _sut.SalvarAsync(dto, "cliente");

        Assert.True(criado);
        Assert.Equal(0, resultado.TotalSugestoesVinculo);
    }
}
