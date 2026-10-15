using CaixaDiario.API.Models;
using CaixaDiario.API.Services;

namespace CaixaDiario.Tests.Services;

public class ProjecaoServiceTests
{
    private readonly ProjecaoService _sut = new();
    private static readonly DateOnly Hoje = DataLocalHelper.Hoje();
    // Bloco 2 da rodada "Visão de Futuro": cenário Provável (sem ajuste) e sem contas/faturas de
    // cartão — equivalente ao comportamento de antes do bloco, pros testes que não testam
    // especificamente cenário/reserva/fatura.
    private static readonly ProjecaoOpcoes OpcoesPadrao = new(new List<ContaBancaria>(), new List<PagamentoFatura>());
    // Índice (0-based) do mês calendário de `data` na tabela de 12 meses de CalcularMeuFuturo
    // (mês 0 = mês calendário atual) — calculado, nunca hardcoded, pra testes não dependerem de em
    // que dia do mês real eles rodam.
    private static int MesIndexDe(DateOnly data) => (data.Year - Hoje.Year) * 12 + (data.Month - Hoje.Month);

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

        var resultado = _sut.Calcular(registros, new List<ContaRecorrente>(), 5, null, OpcoesPadrao);

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

        var resultado = _sut.Calcular(registros, new List<ContaRecorrente>(), 5, contaA, OpcoesPadrao);

        Assert.Equal(1000m, resultado.SaldoAtual);
    }

    [Fact]
    public void Calcular_ComContaBancariaIdVazio_TrataComoSemFiltro()
    {
        var contaA = Guid.NewGuid();
        var registros = new List<RegistroDiario> { CriarRegistro(contaA, Hoje.AddDays(-1), 1000m) };

        var resultado = _sut.Calcular(registros, new List<ContaRecorrente>(), 3, Guid.Empty, OpcoesPadrao);

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

        var resultado = _sut.Calcular(new List<RegistroDiario> { registro }, new List<ContaRecorrente>(), 5, null, OpcoesPadrao);

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

        var resultado = _sut.Calcular(new List<RegistroDiario> { registro }, new List<ContaRecorrente>(), 5, null, OpcoesPadrao);

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

        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<ContaRecorrente> { recorrencia }, 5, null, OpcoesPadrao);

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

        var resultado = _sut.Calcular(new List<RegistroDiario> { registro }, new List<ContaRecorrente> { recorrencia }, 5, null, OpcoesPadrao);

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

        var resultado = _sut.Calcular(new List<RegistroDiario> { historico }, new List<ContaRecorrente> { recorrencia }, 5, null, OpcoesPadrao);

        var dia = resultado.Dias.Single(d => d.Data == dataOcorrencia);
        // Média das 3 últimas pagas (100+110+120)/3 = 110, não o Valor cadastrado (100).
        Assert.Equal(110m, dia.TotalSaidas);
        Assert.Equal("Recorrente", Assert.Single(dia.Saidas).Origem);
    }

    [Fact]
    public void Calcular_RetornaTodosOsDiasMesmoSemMovimento()
    {
        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<ContaRecorrente>(), 15, null, OpcoesPadrao);

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

        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<ContaRecorrente> { recorrencia }, 5, null, OpcoesPadrao);

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

        var resultado = _sut.Calcular(new List<RegistroDiario> { registro }, new List<ContaRecorrente>(), 5, null, OpcoesPadrao);

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

        var resultado = _sut.Calcular(new List<RegistroDiario> { registro }, new List<ContaRecorrente>(), 5, null, OpcoesPadrao);

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

        var resultado = _sut.Calcular(new List<RegistroDiario> { registro }, new List<ContaRecorrente>(), 5, null, OpcoesPadrao);

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

        var resultado = _sut.Calcular(historico, new List<ContaRecorrente>(), 30, null, OpcoesPadrao);

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

        var resultado = _sut.Calcular(historico, new List<ContaRecorrente> { recorrencia }, 30, null, OpcoesPadrao);

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

        var resultado = _sut.CalcularTrajetoria(registros, new List<ContaRecorrente>(), 6, 6, null, OpcoesPadrao);

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

        var resultado = _sut.CalcularTrajetoria(registros, new List<ContaRecorrente>(), 6, 6, contaA, OpcoesPadrao);

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

        var resultado = _sut.CalcularTrajetoria(registros, new List<ContaRecorrente>(), 6, 6, contaA, OpcoesPadrao);

        // Só 2 meses de dado real (mês passado + mês corrente), mesmo pedindo 6.
        Assert.Equal(2, resultado.MesesHistoricoDisponiveis);
        Assert.Equal(2, resultado.Historico.Count);
    }

    [Fact]
    public void CalcularTrajetoria_SemNenhumRegistro_HistoricoVazio()
    {
        var resultado = _sut.CalcularTrajetoria(new List<RegistroDiario>(), new List<ContaRecorrente>(), 6, 6, null, OpcoesPadrao);

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

        var resultado = _sut.CalcularTrajetoria(new List<RegistroDiario> { registro }, new List<ContaRecorrente> { recorrencia }, 6, 3, null, OpcoesPadrao);

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

        var resultado = _sut.CalcularTrajetoria(registros, new List<ContaRecorrente>(), 6, 1, contaA, OpcoesPadrao);

        Assert.Equal(500m, resultado.VariacaoRealizada); // 1000 (atual) - 500 (mês anterior)
        Assert.Equal(300m, resultado.VariacaoProjetada); // 1300 (daqui a 1 mês) - 1000 (atual)
    }

    // ── Bloco 2 da rodada "Visão de Futuro": cenário, reserva de investimento, fatura de cartão ──

    [Fact]
    public void Calcular_CenarioConservador_AplicaFatorNaReceitaRecorrenteEstimada()
    {
        var recorrencia = new ContaRecorrente
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Descricao = "Cliente recorrente", Valor = 1000m,
            Tipo = "Receber", Ativo = true, Periodicidade = "Mensal", DataInicio = Hoje.AddDays(3), CriadoEm = DateTime.UtcNow,
        };
        var opcoes = new ProjecaoOpcoes(new List<ContaBancaria>(), new List<PagamentoFatura>(), "Conservador");

        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<ContaRecorrente> { recorrencia }, 5, null, opcoes);

        var dia = resultado.Dias.Single(d => d.Data == Hoje.AddDays(3));
        Assert.Equal(700m, dia.TotalEntradas); // 1000 × 0,7 (fator conservador de receita)
    }

    [Fact]
    public void Calcular_CenarioConservador_AplicaFatorNaDespesaRecorrenteEstimada()
    {
        var recorrencia = new ContaRecorrente
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Descricao = "Aluguel", Valor = 1000m,
            Tipo = "Pagar", Ativo = true, Periodicidade = "Mensal", DataInicio = Hoje.AddDays(3), CriadoEm = DateTime.UtcNow,
        };
        var opcoes = new ProjecaoOpcoes(new List<ContaBancaria>(), new List<PagamentoFatura>(), "Conservador");

        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<ContaRecorrente> { recorrencia }, 5, null, opcoes);

        var dia = resultado.Dias.Single(d => d.Data == Hoje.AddDays(3));
        Assert.Equal(1150m, dia.TotalSaidas); // 1000 × 1,15 (fator conservador de despesa)
    }

    [Fact]
    public void Calcular_CenarioConservador_AplicaFatorNaDespesaFixaNaoCadastrada()
    {
        var contaA = Guid.NewGuid();
        var historico = new List<RegistroDiario>();
        for (int i = 1; i <= 3; i++)
        {
            var reg = CriarRegistro(contaA, Hoje.AddMonths(-i), 1000m);
            reg.Saidas.Add(new ItemFinanceiroSaida { Descricao = "Aluguel manual", Valor = 400m, TipoCusto = "CustoFixo" });
            historico.Add(reg);
        }
        var opcoes = new ProjecaoOpcoes(new List<ContaBancaria>(), new List<PagamentoFatura>(), "Conservador");

        var resultado = _sut.Calcular(historico, new List<ContaRecorrente>(), 30, null, opcoes);

        var ultimoDia = resultado.Dias.Single(d => d.Data == Hoje.AddDays(30));
        var item = Assert.Single(ultimoDia.Saidas);
        Assert.Equal(460m, item.Valor); // 400 × 1,15
    }

    [Fact]
    public void Calcular_ContaInvestimento_FicaDeForaDoSaldoPorPadrao()
    {
        var contaCorrente = Guid.NewGuid();
        var contaInvestimento = Guid.NewGuid();
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(contaCorrente, Hoje.AddDays(-1), 1000m),
            CriarRegistro(contaInvestimento, Hoje.AddDays(-1), 5000m),
        };
        var contas = new List<ContaBancaria>
        {
            new() { Id = contaCorrente, Tipo = "ContaCorrente", Ativa = true },
            new() { Id = contaInvestimento, Tipo = "Investimento", Ativa = true },
        };
        var opcoes = new ProjecaoOpcoes(contas, new List<PagamentoFatura>());

        var resultado = _sut.Calcular(registros, new List<ContaRecorrente>(), 5, null, opcoes);

        Assert.Equal(1000m, resultado.SaldoAtual);
    }

    [Fact]
    public void Calcular_ComIncluirReserva_SomaContaInvestimentoTambem()
    {
        var contaCorrente = Guid.NewGuid();
        var contaInvestimento = Guid.NewGuid();
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(contaCorrente, Hoje.AddDays(-1), 1000m),
            CriarRegistro(contaInvestimento, Hoje.AddDays(-1), 5000m),
        };
        var contas = new List<ContaBancaria>
        {
            new() { Id = contaCorrente, Tipo = "ContaCorrente", Ativa = true },
            new() { Id = contaInvestimento, Tipo = "Investimento", Ativa = true },
        };
        var opcoes = new ProjecaoOpcoes(contas, new List<PagamentoFatura>(), IncluirReserva: true);

        var resultado = _sut.Calcular(registros, new List<ContaRecorrente>(), 5, null, opcoes);

        Assert.Equal(6000m, resultado.SaldoAtual);
    }

    [Fact]
    public void Calcular_ContaEspecificaDeInvestimento_IgnoraExclusaoEUsaSaldoDaPropriaConta()
    {
        // Selecionar uma conta específica é um recorte explícito do usuário — a exclusão de
        // Investimento do saldo só vale pro modo "todas as contas".
        var contaInvestimento = Guid.NewGuid();
        var registro = CriarRegistro(contaInvestimento, Hoje.AddDays(-1), 5000m);
        var contas = new List<ContaBancaria> { new() { Id = contaInvestimento, Tipo = "Investimento", Ativa = true } };
        var opcoes = new ProjecaoOpcoes(contas, new List<PagamentoFatura>());

        var resultado = _sut.Calcular(new List<RegistroDiario> { registro }, new List<ContaRecorrente>(), 5, contaInvestimento, opcoes);

        Assert.Equal(5000m, resultado.SaldoAtual);
    }

    [Fact]
    public void Calcular_ContaCartaoCredito_FicaDeForaDoSaldo()
    {
        var contaCorrente = Guid.NewGuid();
        var contaCartao = Guid.NewGuid();
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(contaCorrente, Hoje.AddDays(-1), 1000m),
            CriarRegistro(contaCartao, Hoje.AddDays(-1), -300m), // dívida do cartão
        };
        var contas = new List<ContaBancaria>
        {
            new() { Id = contaCorrente, Tipo = "ContaCorrente", Ativa = true },
            new() { Id = contaCartao, Tipo = "CartaoCredito", Ativa = true },
        };
        var opcoes = new ProjecaoOpcoes(contas, new List<PagamentoFatura>());

        var resultado = _sut.Calcular(registros, new List<ContaRecorrente>(), 5, null, opcoes);

        Assert.Equal(1000m, resultado.SaldoAtual); // -300 do cartão não entra
    }

    [Fact]
    public void Calcular_ComFaturaDeCartaoEmAberto_EntraComoSaidaProjetadaNoVencimento()
    {
        // DiaFechamento/DiaVencimento deixados em branco (null) de propósito: assim o fechamento
        // cai sempre no ÚLTIMO dia do mês corrente (sempre ainda não passou) e o vencimento sempre
        // no mês SEGUINTE (sempre no futuro) — o teste nunca depende de em que dia do mês ele roda.
        var contaCartao = new ContaBancaria { Id = Guid.NewGuid(), Nome = "Cartão", Tipo = "CartaoCredito", Ativa = true };
        var registroCompra = CriarRegistro(contaCartao.Id, Hoje, 0m);
        registroCompra.Saidas.Add(new ItemFinanceiroSaida { Descricao = "Compra", Valor = 300m, TipoCusto = "CustoVariavel" });

        var faturaEsperada = FaturaCartaoService.CalcularFaturas(
            contaCartao, new List<RegistroDiario> { registroCompra }, new List<PagamentoFatura>(), new List<Fatura>(), Hoje)
            .Single(f => f.Status != "Paga");
        Assert.True(faturaEsperada.DataVencimento > Hoje); // garantia da escolha de datas acima

        var contaCorrente = Guid.NewGuid();
        var registroCorrente = CriarRegistro(contaCorrente, Hoje.AddDays(-1), 1000m);
        var registros = new List<RegistroDiario> { registroCompra, registroCorrente };
        var contas = new List<ContaBancaria> { contaCartao, new() { Id = contaCorrente, Tipo = "ContaCorrente", Ativa = true } };
        var opcoes = new ProjecaoOpcoes(contas, new List<PagamentoFatura>());

        var dias = (int)(faturaEsperada.DataVencimento.DayNumber - Hoje.DayNumber) + 5;
        var resultado = _sut.Calcular(registros, new List<ContaRecorrente>(), dias, null, opcoes);

        var diaFatura = resultado.Dias.Single(d => d.Data == faturaEsperada.DataVencimento);
        var item = Assert.Single(diaFatura.Saidas, s => s.Origem == "Fatura");
        Assert.Equal(faturaEsperada.SaldoDevedor, item.Valor);
    }

    // Item 7 (Bloco 5, opcional — Money Vision): compra parcelada identificada no extrato ("<comerciante>
    // - Parcela N/M") projeta as parcelas futuras (M-N meses adiante), cada uma na fatura futura
    // correspondente — mesmo sem a fatura real daquele mês ainda ter sido importada.
    [Fact]
    public void Calcular_ComCompraParceladaNoExtrato_ProjetaAsParcelasFuturasNasFaturasCorrespondentes()
    {
        var contaCartao = new ContaBancaria
        {
            Id = Guid.NewGuid(), Nome = "Cartão", Tipo = "CartaoCredito", Ativa = true,
            DiaFechamento = 1, DiaVencimento = 10,
        };
        var registroCompra = CriarRegistro(contaCartao.Id, Hoje, 0m);
        registroCompra.Saidas.Add(new ItemFinanceiroSaida
        {
            Descricao = "Trocafone - Parcela 1/3", Valor = 100m, Categoria = "Eletrônicos", NumeroParcela = 1, TotalParcelas = 3,
        });

        var contaCorrente = Guid.NewGuid();
        var registroCorrente = CriarRegistro(contaCorrente, Hoje.AddDays(-1), 1000m);
        var registros = new List<RegistroDiario> { registroCompra, registroCorrente };
        var contas = new List<ContaBancaria> { contaCartao, new() { Id = contaCorrente, Tipo = "ContaCorrente", Ativa = true } };
        var opcoes = new ProjecaoOpcoes(contas, new List<PagamentoFatura>());

        var resultado = _sut.Calcular(registros, new List<ContaRecorrente>(), 150, null, opcoes);

        var projetadas = resultado.Dias.SelectMany(d => d.Saidas.Where(s => s.Origem == "Fatura" && s.Descricao.Contains("(previsto)")))
            .ToList();
        Assert.Equal(2, projetadas.Count); // parcelas 2/3 e 3/3
        Assert.All(projetadas, s => Assert.Equal(100m, s.Valor));
        Assert.Contains(projetadas, s => s.Descricao.Contains("Parcela 2/3"));
        Assert.Contains(projetadas, s => s.Descricao.Contains("Parcela 3/3"));
    }

    // Fatura de ciclo já encerrado há meses e totalmente paga — tem que ficar de fora tanto pelo
    // filtro de data (vencimento no passado) quanto pelo status "Paga".
    [Fact]
    public void Calcular_ComFaturaDeCartaoAntigaJaPaga_NaoEntraComoSaidaProjetada()
    {
        var contaCartao = new ContaBancaria { Id = Guid.NewGuid(), Nome = "Cartão", Tipo = "CartaoCredito", Ativa = true };
        var dataCompra = Hoje.AddMonths(-3);
        var registroCompra = CriarRegistro(contaCartao.Id, dataCompra, 0m);
        registroCompra.Saidas.Add(new ItemFinanceiroSaida { Descricao = "Compra", Valor = 300m, TipoCusto = "CustoVariavel" });

        var competencia = FaturaCartaoService.ObterCompetencia(dataCompra, contaCartao.DiaFechamento);
        var pagamento = new PagamentoFatura { Id = Guid.NewGuid(), ContaCartaoId = contaCartao.Id, Competencia = competencia, ValorPago = 300m };

        var contas = new List<ContaBancaria> { contaCartao };
        var opcoes = new ProjecaoOpcoes(contas, new List<PagamentoFatura> { pagamento });

        var resultado = _sut.Calcular(new List<RegistroDiario> { registroCompra }, new List<ContaRecorrente>(), 90, null, opcoes);

        Assert.DoesNotContain(resultado.Dias.SelectMany(d => d.Saidas), s => s.Origem == "Fatura");
    }

    [Fact]
    public void CalcularDespesasEstimadasPorCategoria_ComCategoriaSemRecorrenciaCadastrada_RetornaMediaDosUltimos3Meses()
    {
        var contaA = Guid.NewGuid();
        var historico = new List<RegistroDiario>();
        for (int i = 1; i <= 3; i++)
        {
            var reg = CriarRegistro(contaA, Hoje.AddMonths(-i), 1000m);
            reg.Saidas.Add(new ItemFinanceiroSaida { Descricao = "Internet", Valor = 100m + i * 10, Categoria = "Energia/Água/Internet", TipoCusto = "CustoFixo" });
            historico.Add(reg);
        }

        var resultado = _sut.CalcularDespesasEstimadasPorCategoria(historico, new List<ContaRecorrente>(), Hoje);

        var item = Assert.Single(resultado);
        Assert.Equal("Energia/Água/Internet", item.Categoria);
        Assert.Equal(120m, item.ValorEstimado); // média de 110, 120, 130
    }

    [Fact]
    public void CalcularDespesasEstimadasPorCategoria_ComRecorrenciaJaCobrindoACategoria_NaoARetorna()
    {
        var contaA = Guid.NewGuid();
        var recorrencia = new ContaRecorrente
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Descricao = "Internet", Valor = 200m, Categoria = "Energia/Água/Internet",
            Tipo = "Pagar", Ativo = true, Periodicidade = "Mensal", DataInicio = Hoje.AddDays(-40), CriadoEm = DateTime.UtcNow,
        };
        var historico = new List<RegistroDiario>();
        for (int i = 1; i <= 3; i++)
        {
            var reg = CriarRegistro(contaA, Hoje.AddMonths(-i), 1000m);
            reg.Saidas.Add(new ItemFinanceiroSaida { Descricao = "Internet", Valor = 100m, Categoria = "Energia/Água/Internet", TipoCusto = "CustoFixo" });
            historico.Add(reg);
        }

        var resultado = _sut.CalcularDespesasEstimadasPorCategoria(historico, new List<ContaRecorrente> { recorrencia }, Hoje);

        Assert.Empty(resultado);
    }

    // ── Bloco 3 da rodada "Visão de Futuro": CalcularMeuFuturo ───────────────────────────────────

    [Fact]
    public void CalcularMeuFuturo_Retorna12MesesComSaldoInicialDoPrimeiroMesIgualAoSaldoAtual()
    {
        var contaA = Guid.NewGuid();
        var registro = CriarRegistro(contaA, Hoje.AddDays(-1), 1000m);
        var contas = new List<ContaBancaria> { new() { Id = contaA, Tipo = "ContaCorrente", Ativa = true } };
        var opcoes = new ProjecaoOpcoes(contas, new List<PagamentoFatura>());

        var resultado = _sut.CalcularMeuFuturo(new List<RegistroDiario> { registro }, new List<ContaRecorrente>(), opcoes, reservaMinima: 0m);

        Assert.Equal(12, resultado.Meses.Count);
        Assert.Equal(1000m, resultado.Meses[0].SaldoInicial);
    }

    [Fact]
    public void CalcularMeuFuturo_ComContaAPagarFutura_ReduzSaldoDoMesCorrespondenteEDosSeguintes()
    {
        var contaA = Guid.NewGuid();
        var vencimento = Hoje.AddDays(45);
        var registro = CriarRegistro(contaA, Hoje.AddDays(-1), 5000m);
        registro.ContasPagar.Add(new ContaProvisionada { Descricao = "Fornecedor", Valor = 1000m, Pago = false, DataVencimento = vencimento });
        var contas = new List<ContaBancaria> { new() { Id = contaA, Tipo = "ContaCorrente", Ativa = true } };
        var opcoes = new ProjecaoOpcoes(contas, new List<PagamentoFatura>());

        var resultado = _sut.CalcularMeuFuturo(new List<RegistroDiario> { registro }, new List<ContaRecorrente>(), opcoes, reservaMinima: 0m);

        var idx = MesIndexDe(vencimento);
        Assert.Equal(1000m, resultado.Meses[idx].APagar);
        Assert.Equal(4000m, resultado.Meses[idx].SaldoFinal);
        Assert.Equal(4000m, resultado.Meses[^1].SaldoFinal); // carrega até o fim; nada mais muda depois
    }

    [Fact]
    public void CalcularMeuFuturo_SaldoAbaixoDaReservaMinima_MarcaAtencaoEPrecisaReceber()
    {
        var contaA = Guid.NewGuid();
        var registro = CriarRegistro(contaA, Hoje.AddDays(-1), 500m);
        var contas = new List<ContaBancaria> { new() { Id = contaA, Tipo = "ContaCorrente", Ativa = true } };
        var opcoes = new ProjecaoOpcoes(contas, new List<PagamentoFatura>());

        var resultado = _sut.CalcularMeuFuturo(new List<RegistroDiario> { registro }, new List<ContaRecorrente>(), opcoes, reservaMinima: 2000m);

        Assert.Equal("Atencao", resultado.Meses[0].Status);
        Assert.Equal(1500m, resultado.Meses[0].PrecisaReceber);
    }

    [Fact]
    public void CalcularMeuFuturo_ComContaAPagarMaiorQueOSaldo_MarcaMesComoRisco()
    {
        var contaA = Guid.NewGuid();
        var vencimento = Hoje.AddDays(3);
        var registro = CriarRegistro(contaA, Hoje.AddDays(-1), 500m);
        registro.ContasPagar.Add(new ContaProvisionada { Descricao = "Fornecedor", Valor = 800m, Pago = false, DataVencimento = vencimento });
        var contas = new List<ContaBancaria> { new() { Id = contaA, Tipo = "ContaCorrente", Ativa = true } };
        var opcoes = new ProjecaoOpcoes(contas, new List<PagamentoFatura>());

        var resultado = _sut.CalcularMeuFuturo(new List<RegistroDiario> { registro }, new List<ContaRecorrente>(), opcoes, reservaMinima: 0m);

        var idx = MesIndexDe(vencimento);
        Assert.Equal("Risco", resultado.Meses[idx].Status);
        Assert.Equal(-300m, resultado.Meses[idx].SaldoFinal);
    }

    // "E se...?": os dois ajustes são pós-processamento sobre a tabela mensal (item 3 do plano).

    [Fact]
    public void CalcularMeuFuturo_ComAjusteReceitaPercent_EscalaAReceberDoMes()
    {
        var contaA = Guid.NewGuid();
        var recebimento = Hoje.AddDays(2);
        var registro = CriarRegistro(contaA, Hoje.AddDays(-1), 0m);
        registro.ContasReceber.Add(new ContaProvisionada { Descricao = "Cliente", Valor = 1000m, Pago = false, DataVencimento = recebimento });
        var contas = new List<ContaBancaria> { new() { Id = contaA, Tipo = "ContaCorrente", Ativa = true } };
        var opcoes = new ProjecaoOpcoes(contas, new List<PagamentoFatura>());

        var resultado = _sut.CalcularMeuFuturo(
            new List<RegistroDiario> { registro }, new List<ContaRecorrente>(), opcoes, reservaMinima: 0m, ajusteReceitaPercent: -20m);

        var idx = MesIndexDe(recebimento);
        Assert.Equal(800m, resultado.Meses[idx].AReceber); // 1000 × 0,8
    }

    [Fact]
    public void CalcularMeuFuturo_ComAtrasoDe30Dias_DeslocaAReceberParaOMesSeguinte()
    {
        var contaA = Guid.NewGuid();
        var recebimento = Hoje.AddDays(2);
        var registro = CriarRegistro(contaA, Hoje.AddDays(-1), 0m);
        registro.ContasReceber.Add(new ContaProvisionada { Descricao = "Cliente", Valor = 1000m, Pago = false, DataVencimento = recebimento });
        var contas = new List<ContaBancaria> { new() { Id = contaA, Tipo = "ContaCorrente", Ativa = true } };
        var opcoes = new ProjecaoOpcoes(contas, new List<PagamentoFatura>());

        var resultado = _sut.CalcularMeuFuturo(
            new List<RegistroDiario> { registro }, new List<ContaRecorrente>(), opcoes, reservaMinima: 0m, atrasoRecebimentosDias: 30);

        var idx = MesIndexDe(recebimento);
        Assert.Equal(0m, resultado.Meses[idx].AReceber);
        Assert.Equal(1000m, resultado.Meses[idx + 1].AReceber);
    }

    [Fact]
    public void CalcularMeuFuturo_ComMesEmRisco_GeraConselhoCitandoSaldoNegativo()
    {
        var contaA = Guid.NewGuid();
        var vencimento = Hoje.AddDays(3);
        var registro = CriarRegistro(contaA, Hoje.AddDays(-1), 100m);
        registro.ContasPagar.Add(new ContaProvisionada { Descricao = "Fornecedor", Valor = 500m, Pago = false, DataVencimento = vencimento });
        var contas = new List<ContaBancaria> { new() { Id = contaA, Tipo = "ContaCorrente", Ativa = true } };
        var opcoes = new ProjecaoOpcoes(contas, new List<PagamentoFatura>());

        var resultado = _sut.CalcularMeuFuturo(new List<RegistroDiario> { registro }, new List<ContaRecorrente>(), opcoes, reservaMinima: 0m);

        Assert.InRange(resultado.Conselhos.Count, 1, 3);
        Assert.Contains(resultado.Conselhos, c => c.Contains("saldo negativo"));
    }

    [Fact]
    public void CalcularMeuFuturo_Com12MesesTranquilos_GeraUmUnicoConselhoPositivo()
    {
        var contaA = Guid.NewGuid();
        var registro = CriarRegistro(contaA, Hoje.AddDays(-1), 100000m);
        var contas = new List<ContaBancaria> { new() { Id = contaA, Tipo = "ContaCorrente", Ativa = true } };
        var opcoes = new ProjecaoOpcoes(contas, new List<PagamentoFatura>());

        var resultado = _sut.CalcularMeuFuturo(new List<RegistroDiario> { registro }, new List<ContaRecorrente>(), opcoes, reservaMinima: 0m);

        Assert.All(resultado.Meses, m => Assert.Equal("Tranquilo", m.Status));
        var conselho = Assert.Single(resultado.Conselhos);
        Assert.Contains("saudável", conselho);
    }
}
