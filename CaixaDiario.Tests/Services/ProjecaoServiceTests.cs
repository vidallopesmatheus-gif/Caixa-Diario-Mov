using CaixaDiario.API.Models;
using CaixaDiario.API.Services;

namespace CaixaDiario.Tests.Services;

public class ProjecaoServiceTests
{
    private readonly ProjecaoService _sut = new();
    private static readonly DateOnly Hoje = DataLocalHelper.Hoje();

    private static RegistroDiario CriarRegistro(Guid contaId, DateOnly data, decimal saldoFinal) => new()
    {
        Id = Guid.NewGuid(),
        ClienteId = Guid.NewGuid(),
        ContaBancariaId = contaId,
        Data = data,
        Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
        SaldoFinal = saldoFinal,
        CriadoEm = DateTime.UtcNow,
        SalvoEm = DateTime.UtcNow,
    };

    [Fact]
    public void Calcular_SemFiltroDeConta_SomaUltimoSaldoDeCadaContaDistinta()
    {
        var contaA = Guid.NewGuid();
        var contaB = Guid.NewGuid();
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(contaA, Hoje.AddDays(-2), 800m),
            CriarRegistro(contaA, Hoje.AddDays(-1), 1000m), // mais recente da conta A
            CriarRegistro(contaB, Hoje.AddDays(-2), 500m),  // única/mais recente da conta B
        };

        var resultado = _sut.Calcular(registros, new List<ContaRecorrente>(), 5, null);

        Assert.Equal(1500m, resultado.SaldoAtual);
        Assert.Equal(5, resultado.TotalDias);
        Assert.Equal(5, resultado.Dias.Count);
    }

    [Fact]
    public void Calcular_ComFiltroDeConta_UsaApenasSaldoDaContaInformada()
    {
        var contaA = Guid.NewGuid();
        var contaB = Guid.NewGuid();
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(contaA, Hoje.AddDays(-1), 1000m),
            CriarRegistro(contaB, Hoje.AddDays(-1), 500m),
        };

        var resultado = _sut.Calcular(registros, new List<ContaRecorrente>(), 5, contaA);

        Assert.Equal(1000m, resultado.SaldoAtual);
    }

    [Fact]
    public void Calcular_ComContaBancariaIdVazio_TrataComoSemFiltro()
    {
        var contaA = Guid.NewGuid();
        var registros = new List<RegistroDiario> { CriarRegistro(contaA, Hoje.AddDays(-1), 1000m) };

        var resultado = _sut.Calcular(registros, new List<ContaRecorrente>(), 3, Guid.Empty);

        Assert.Equal(1000m, resultado.SaldoAtual);
    }

    [Fact]
    public void Calcular_ComProvisionadoFuturo_ApareceNoDiaCorretoEAtualizaSaldo()
    {
        var contaA = Guid.NewGuid();
        var registro = CriarRegistro(contaA, Hoje.AddDays(-1), 1000m);
        registro.ContasReceber.Add(new ContaProvisionada
        {
            Descricao = "Cliente X", Valor = 300m, Categoria = "Vendas", Pago = false, DataVencimento = Hoje.AddDays(3),
        });
        registro.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Fornecedor Y", Valor = 100m, Categoria = "Insumos", Pago = false, DataVencimento = Hoje.AddDays(3),
        });

        var resultado = _sut.Calcular(new List<RegistroDiario> { registro }, new List<ContaRecorrente>(), 5, null);

        var diaMovimento = resultado.Dias.Single(d => d.Data == Hoje.AddDays(3));
        Assert.Equal(300m, diaMovimento.TotalEntradas);
        Assert.Equal(100m, diaMovimento.TotalSaidas);
        Assert.Equal("Cliente X", Assert.Single(diaMovimento.Entradas).Descricao);
        Assert.Equal("Provisionado", diaMovimento.Entradas[0].Origem);
        Assert.Equal(1200m, diaMovimento.SaldoFim); // 1000 + 300 - 100
        Assert.False(diaMovimento.SaldoNegativo);
    }

    [Fact]
    public void Calcular_ComProvisionadoQueLevaSaldoNegativo_MarcaSaldoNegativo()
    {
        var contaA = Guid.NewGuid();
        var registro = CriarRegistro(contaA, Hoje.AddDays(-1), 100m);
        registro.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Fornecedor", Valor = 500m, Pago = false, DataVencimento = Hoje.AddDays(2),
        });

        var resultado = _sut.Calcular(new List<RegistroDiario> { registro }, new List<ContaRecorrente>(), 5, null);

        var dia = resultado.Dias.Single(d => d.Data == Hoje.AddDays(2));
        Assert.True(dia.SaldoNegativo);
        Assert.Equal(-400m, dia.SaldoFim);
    }

    [Fact]
    public void Calcular_ComRecorrenciaAtiva_ProjetaNosDiasQueOcorre()
    {
        var recorrencia = new ContaRecorrente
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Descricao = "Aluguel", Valor = 200m,
            Tipo = "Pagar", Ativo = true, Periodicidade = "Mensal", DataInicio = Hoje.AddDays(4), CriadoEm = DateTime.UtcNow,
        };

        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<ContaRecorrente> { recorrencia }, 5, null);

        var dia = resultado.Dias.Single(d => d.Data == Hoje.AddDays(4));
        Assert.Equal(200m, dia.TotalSaidas);
        Assert.Equal("Recorrente", Assert.Single(dia.Saidas).Origem);
    }

    [Fact]
    public void Calcular_ComRecorrenciaJaMaterializadaComoProvisionado_NaoDuplicaLancamento()
    {
        var contaA = Guid.NewGuid();
        var recorrenciaId = Guid.NewGuid();
        var dataOcorrencia = Hoje.AddDays(4);

        var recorrencia = new ContaRecorrente
        {
            Id = recorrenciaId, ClienteId = Guid.NewGuid(), Descricao = "Aluguel", Valor = 200m,
            Tipo = "Pagar", Ativo = true, Periodicidade = "Mensal", DataInicio = dataOcorrencia, CriadoEm = DateTime.UtcNow,
        };

        var registro = CriarRegistro(contaA, Hoje.AddDays(-1), 1000m);
        registro.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Aluguel", Valor = 200m, Pago = false, DataVencimento = dataOcorrencia, RecorrenciaId = recorrenciaId,
        });

        var resultado = _sut.Calcular(new List<RegistroDiario> { registro }, new List<ContaRecorrente> { recorrencia }, 5, null);

        var dia = resultado.Dias.Single(d => d.Data == dataOcorrencia);
        // Apenas o provisionado manual deve contar; a recorrência já materializada é ignorada.
        var lancamento = Assert.Single(dia.Saidas);
        Assert.Equal("Provisionado", lancamento.Origem);
        Assert.Equal(200m, dia.TotalSaidas);
    }

    [Fact]
    public void Calcular_ComRecorrenciaValorVariavel_ProjetaComMediaDasUltimas3PagasNaoComValorCadastrado()
    {
        var contaA = Guid.NewGuid();
        var recorrenciaId = Guid.NewGuid();
        var dataOcorrencia = Hoje.AddDays(4);

        var recorrencia = new ContaRecorrente
        {
            Id = recorrenciaId, ClienteId = Guid.NewGuid(), Descricao = "Energia", Valor = 100m,
            Tipo = "Pagar", Ativo = true, Periodicidade = "Mensal", DataInicio = dataOcorrencia,
            ContaBancariaId = contaA, ValorVariavel = true, CriadoEm = DateTime.UtcNow,
        };

        var historico = CriarRegistro(contaA, Hoje.AddDays(-1), 1000m);
        historico.ContasPagar.AddRange(new[]
        {
            new ContaProvisionada { RecorrenciaId = recorrenciaId, Valor = 100m, Pago = true, DataBaixa = Hoje.AddMonths(-3) },
            new ContaProvisionada { RecorrenciaId = recorrenciaId, Valor = 110m, Pago = true, DataBaixa = Hoje.AddMonths(-2) },
            new ContaProvisionada { RecorrenciaId = recorrenciaId, Valor = 120m, Pago = true, DataBaixa = Hoje.AddMonths(-1) },
        });

        var resultado = _sut.Calcular(new List<RegistroDiario> { historico }, new List<ContaRecorrente> { recorrencia }, 5, null);

        var dia = resultado.Dias.Single(d => d.Data == dataOcorrencia);
        // Média das 3 últimas pagas (100+110+120)/3 = 110, não o Valor cadastrado (100).
        Assert.Equal(110m, dia.TotalSaidas);
        Assert.Equal("Recorrente", Assert.Single(dia.Saidas).Origem);
    }

    [Fact]
    public void Calcular_RetornaTodosOsDiasMesmoSemMovimento()
    {
        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<ContaRecorrente>(), 15, null);

        Assert.Equal(15, resultado.Dias.Count);
        for (int d = 1; d <= 15; d++)
            Assert.Contains(resultado.Dias, dia => dia.Data == Hoje.AddDays(d));
    }

    [Fact]
    public void Calcular_RecorrenciaInativa_NaoEntraNaProjecao()
    {
        var recorrencia = new ContaRecorrente
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Descricao = "Assinatura", Valor = 50m,
            Tipo = "Pagar", Ativo = false, Periodicidade = "Mensal", DataInicio = Hoje.AddDays(2), CriadoEm = DateTime.UtcNow,
        };

        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<ContaRecorrente> { recorrencia }, 5, null);

        Assert.All(resultado.Dias, d => Assert.Equal(0m, d.TotalSaidas));
    }

    [Fact]
    public void Calcular_ComContaPagarAtrasada_EntraNoPrimeiroDiaDaProjecao()
    {
        var contaA = Guid.NewGuid();
        var registro = CriarRegistro(contaA, Hoje.AddDays(-10), 1000m);
        registro.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Fornecedor atrasado", Valor = 300m, Pago = false, DataVencimento = Hoje.AddDays(-5),
        });

        var resultado = _sut.Calcular(new List<RegistroDiario> { registro }, new List<ContaRecorrente>(), 5, null);

        var primeiroDia = resultado.Dias.Single(d => d.Data == Hoje.AddDays(1));
        var item = Assert.Single(primeiroDia.Saidas);
        Assert.Equal("Atrasado", item.Origem);
        Assert.Equal(300m, primeiroDia.TotalSaidas);
        Assert.Equal(700m, primeiroDia.SaldoFim);
    }

    [Fact]
    public void Calcular_ComContaReceberAtrasada_EntraNoPrimeiroDiaDaProjecao()
    {
        var contaA = Guid.NewGuid();
        var registro = CriarRegistro(contaA, Hoje.AddDays(-10), 1000m);
        registro.ContasReceber.Add(new ContaProvisionada
        {
            Descricao = "Cliente atrasado", Valor = 150m, Pago = false, DataVencimento = Hoje.AddDays(-1),
        });

        var resultado = _sut.Calcular(new List<RegistroDiario> { registro }, new List<ContaRecorrente>(), 5, null);

        var primeiroDia = resultado.Dias.Single(d => d.Data == Hoje.AddDays(1));
        var item = Assert.Single(primeiroDia.Entradas);
        Assert.Equal("Atrasado", item.Origem);
        Assert.Equal(150m, primeiroDia.TotalEntradas);
    }

    [Fact]
    public void Calcular_ComContaPaga_NaoEntraComoAtrasada()
    {
        var contaA = Guid.NewGuid();
        var registro = CriarRegistro(contaA, Hoje.AddDays(-10), 1000m);
        registro.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Já paga", Valor = 300m, Pago = true, DataVencimento = Hoje.AddDays(-5),
        });

        var resultado = _sut.Calcular(new List<RegistroDiario> { registro }, new List<ContaRecorrente>(), 5, null);

        var primeiroDia = resultado.Dias.Single(d => d.Data == Hoje.AddDays(1));
        Assert.Empty(primeiroDia.Saidas);
    }

    [Fact]
    public void Calcular_ComDespesaFixaHistoricaSemRecorrenciaCadastrada_AdicionaEstimativaNoUltimoDia()
    {
        var contaA = Guid.NewGuid();
        var historico = new List<RegistroDiario>();
        for (int i = 1; i <= 3; i++)
        {
            var reg = CriarRegistro(contaA, Hoje.AddMonths(-i), 1000m);
            reg.Saidas.Add(new ItemFinanceiroSaida { Descricao = "Aluguel manual", Valor = 400m, TipoCusto = "CustoFixo" });
            historico.Add(reg);
        }

        var resultado = _sut.Calcular(historico, new List<ContaRecorrente>(), 30, null);

        var ultimoDia = resultado.Dias.Single(d => d.Data == Hoje.AddDays(30));
        var item = Assert.Single(ultimoDia.Saidas);
        Assert.Equal("Estimativa", item.Origem);
        Assert.Equal(400m, item.Valor);
    }

    [Fact]
    public void Calcular_ComDespesaFixaJaCobertaPelaRecorrencia_NaoDuplicaEstimativa()
    {
        var contaA = Guid.NewGuid();
        var recorrencia = new ContaRecorrente
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Descricao = "Aluguel", Valor = 400m,
            Tipo = "Pagar", Ativo = true, Periodicidade = "Mensal", DataInicio = Hoje.AddDays(-40), CriadoEm = DateTime.UtcNow,
        };

        var historico = new List<RegistroDiario>();
        for (int i = 1; i <= 3; i++)
        {
            var reg = CriarRegistro(contaA, Hoje.AddMonths(-i), 1000m);
            reg.Saidas.Add(new ItemFinanceiroSaida { Descricao = "Aluguel manual", Valor = 400m, TipoCusto = "CustoFixo" });
            historico.Add(reg);
        }

        var resultado = _sut.Calcular(historico, new List<ContaRecorrente> { recorrencia }, 30, null);

        // A recorrência "Aluguel" já cobre os 400 de despesa fixa histórica — nada extra deve
        // ser adicionado (senão contaria a mesma despesa duas vezes).
        var ultimoDia = resultado.Dias.Single(d => d.Data == Hoje.AddDays(30));
        Assert.DoesNotContain(ultimoDia.Saidas, s => s.Origem == "Estimativa");
    }

    // ── CalcularTrajetoria: histórico realizado + projeção na mesma linha do tempo ──────────────

    [Fact]
    public void CalcularTrajetoria_SemFiltroDeConta_ConsolidaSaldoDeTodasAsContasPorMes()
    {
        var contaA = Guid.NewGuid();
        var contaB = Guid.NewGuid();
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(contaA, Hoje.AddMonths(-5), 1000m),
            CriarRegistro(contaB, Hoje.AddMonths(-5), 500m),
            CriarRegistro(contaA, Hoje.AddDays(-1), 1200m), // A mais recente
            CriarRegistro(contaB, Hoje.AddMonths(-3), 600m), // B não mexe mais depois disso — carrega pro resto
        };

        var resultado = _sut.CalcularTrajetoria(registros, new List<ContaRecorrente>(), 6, 6, null);

        // Mês corrente: A=1200 (mais recente) + B=600 (carregado do último mês em que teve atividade)
        var mesAtual = resultado.Historico.Last();
        Assert.Equal(1800m, mesAtual.Saldo);
        Assert.Equal(1800m, resultado.SaldoAtual);
    }

    [Fact]
    public void CalcularTrajetoria_ComFiltroDeConta_IgnoraSaldoDasOutrasContas()
    {
        var contaA = Guid.NewGuid();
        var contaB = Guid.NewGuid();
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(contaA, Hoje.AddDays(-1), 1200m),
            CriarRegistro(contaB, Hoje.AddDays(-1), 999m),
        };

        var resultado = _sut.CalcularTrajetoria(registros, new List<ContaRecorrente>(), 6, 6, contaA);

        Assert.Equal(1200m, resultado.SaldoAtual);
    }

    [Fact]
    public void CalcularTrajetoria_MenosDeSeisMesesDeHistorico_RetornaSoOsMesesDisponiveis()
    {
        var contaA = Guid.NewGuid();
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(contaA, Hoje.AddMonths(-1), 500m),
            CriarRegistro(contaA, Hoje.AddDays(-1), 800m),
        };

        var resultado = _sut.CalcularTrajetoria(registros, new List<ContaRecorrente>(), 6, 6, contaA);

        // Só 2 meses de dado real (mês passado + mês corrente), mesmo pedindo 6.
        Assert.Equal(2, resultado.MesesHistoricoDisponiveis);
        Assert.Equal(2, resultado.Historico.Count);
    }

    [Fact]
    public void CalcularTrajetoria_SemNenhumRegistro_HistoricoVazio()
    {
        var resultado = _sut.CalcularTrajetoria(new List<RegistroDiario>(), new List<ContaRecorrente>(), 6, 6, null);

        Assert.Equal(0, resultado.MesesHistoricoDisponiveis);
        Assert.Empty(resultado.Historico);
    }

    [Fact]
    public void CalcularTrajetoria_ComRecorrenciaMensal_ProjetaSaldoNoFimDeCadaMes()
    {
        var contaA = Guid.NewGuid();
        var registro = CriarRegistro(contaA, Hoje.AddDays(-1), 1000m);
        var recorrencia = new ContaRecorrente
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Descricao = "Aluguel", Valor = 200m,
            Tipo = "Pagar", Ativo = true, Periodicidade = "Mensal", DataInicio = Hoje.AddDays(5), CriadoEm = DateTime.UtcNow,
        };

        var resultado = _sut.CalcularTrajetoria(new List<RegistroDiario> { registro }, new List<ContaRecorrente> { recorrencia }, 6, 3, null);

        Assert.Equal(3, resultado.Projetado.Count);
        // Depois de 1 mês, já pagou 1 aluguel; depois de 3 meses, já pagou 3.
        Assert.True(resultado.Projetado[0].Saldo < 1000m);
        Assert.True(resultado.Projetado[2].Saldo < resultado.Projetado[0].Saldo);
    }

    [Fact]
    public void CalcularTrajetoria_CalculaVariacaoRealizadaEProjetadaParaComparacao()
    {
        var contaA = Guid.NewGuid();
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(contaA, Hoje.AddMonths(-1), 500m),
            CriarRegistro(contaA, Hoje.AddDays(-1), 1000m),
        };
        registros[1].ContasReceber.Add(new ContaProvisionada
        {
            Descricao = "Cliente", Valor = 300m, Pago = false, DataVencimento = Hoje.AddDays(10),
        });

        var resultado = _sut.CalcularTrajetoria(registros, new List<ContaRecorrente>(), 6, 1, contaA);

        Assert.Equal(500m, resultado.VariacaoRealizada); // 1000 (atual) - 500 (mês anterior)
        Assert.Equal(300m, resultado.VariacaoProjetada); // 1300 (daqui a 1 mês) - 1000 (atual)
    }
}
