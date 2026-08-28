using CaixaDiario.API.DTOs.ContasBancarias;
using CaixaDiario.API.DTOs.Registros;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class ContaBancariaService : IContaBancariaService
{
    private static readonly HashSet<string> TiposValidos = new() { "Caixa", "ContaCorrente", "Investimento" };

    private readonly IContaBancariaRepository _contaRepo;
    private readonly IRegistroRepository _registroRepo;
    private readonly IMetaRepository _metaRepo;

    public ContaBancariaService(IContaBancariaRepository contaRepo, IRegistroRepository registroRepo, IMetaRepository metaRepo)
    {
        _contaRepo = contaRepo;
        _registroRepo = registroRepo;
        _metaRepo = metaRepo;
    }

    public async Task<List<ContaBancariaDto>> ListarPorClienteAsync(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);
        var contas = await _contaRepo.ListarPorClienteAsync(clienteId);
        var registros = await _registroRepo.ListarPorClienteAsync(clienteId);
        var metas = await _metaRepo.ListarPorClienteAsync(clienteId);
        return contas.Select(c => MapToDto(c, registros, metas)).ToList();
    }

    public async Task<ContaBancariaDto> ObterPorIdAsync(Guid id, Guid usuarioLogadoId, string perfil)
    {
        var conta = await _contaRepo.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        VerificarAcesso(conta.ClienteId, usuarioLogadoId, perfil);
        var registros = await _registroRepo.ListarPorContaAsync(id);
        var metas = await _metaRepo.ListarPorClienteAsync(conta.ClienteId);
        return MapToDto(conta, registros, metas);
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

        conta.Nome = dto.Nome.Trim();
        conta.Tipo = dto.Tipo;
        conta.SaldoInicial = dto.SaldoInicial;
        conta.Ativa = dto.Ativa;

        var atualizada = await _contaRepo.AtualizarAsync(conta);
        var registros = await _registroRepo.ListarPorContaAsync(id);
        var metas = await _metaRepo.ListarPorClienteAsync(conta.ClienteId);
        return MapToDto(atualizada, registros, metas);
    }

    public async Task InativarAsync(Guid id, Guid usuarioLogadoId, string perfil)
    {
        var conta = await _contaRepo.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        VerificarAcesso(conta.ClienteId, usuarioLogadoId, perfil);

        conta.Ativa = false;
        await _contaRepo.AtualizarAsync(conta);
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

        var linhas = new List<(DateOnly Data, LancamentoExtratoDto Dto)>();
        decimal saldo = conta.SaldoInicial;

        foreach (var r in registros)
        {
            // Sincroniza com o início do dia armazenado, evitando deriva por edições manuais de saldo.
            saldo = r.Inicio;

            foreach (var entrada in r.Entradas)
            {
                saldo += entrada.Valor;
                linhas.Add((r.Data, new LancamentoExtratoDto
                {
                    Data = r.Data.ToString("yyyy-MM-dd"),
                    Descricao = entrada.Descricao,
                    Categoria = entrada.Categoria,
                    Valor = entrada.Valor,
                    SaldoAcumulado = saldo,
                }));
            }

            foreach (var recebido in r.ContasReceber.Where(cp => cp.Pago && cp.DataBaixa == r.Data))
            {
                saldo += recebido.Valor;
                linhas.Add((r.Data, new LancamentoExtratoDto
                {
                    Data = r.Data.ToString("yyyy-MM-dd"),
                    Descricao = $"{recebido.Descricao} (recebimento)",
                    Categoria = recebido.Categoria,
                    Valor = recebido.Valor,
                    SaldoAcumulado = saldo,
                }));
            }

            foreach (var saida in r.Saidas)
            {
                saldo -= saida.Valor;
                linhas.Add((r.Data, new LancamentoExtratoDto
                {
                    Data = r.Data.ToString("yyyy-MM-dd"),
                    Descricao = saida.Descricao,
                    Categoria = saida.Categoria,
                    Valor = -saida.Valor,
                    SaldoAcumulado = saldo,
                    PendenteCategorizacao = saida.PendenteCategorizacao,
                }));
            }

            foreach (var pago in r.ContasPagar.Where(cp => cp.Pago && cp.DataBaixa == r.Data))
            {
                saldo -= pago.Valor;
                linhas.Add((r.Data, new LancamentoExtratoDto
                {
                    Data = r.Data.ToString("yyyy-MM-dd"),
                    Descricao = $"{pago.Descricao} (pagamento)",
                    Categoria = pago.Categoria,
                    Valor = -pago.Valor,
                    SaldoAcumulado = saldo,
                }));
            }
        }

        IEnumerable<(DateOnly Data, LancamentoExtratoDto Dto)> filtradas = linhas;
        if (de.HasValue) filtradas = filtradas.Where(l => l.Data >= de.Value);
        if (ate.HasValue) filtradas = filtradas.Where(l => l.Data <= ate.Value);

        return filtradas
            .OrderByDescending(l => l.Data)
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
        if (dto.Data > DateOnly.FromDateTime(DateTime.UtcNow))
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

    private static ContaBancariaDto MapToDto(ContaBancaria c, IEnumerable<RegistroDiario> registros, IEnumerable<MetaAnual> metasDoCliente)
    {
        var regsOrdenados = registros
            .Where(r => r.ContaBancariaId == c.Id && !r.Excluido)
            .OrderByDescending(r => r.Data)
            .ToList();

        var saldoAtual = regsOrdenados.FirstOrDefault()?.SaldoFinal ?? c.SaldoInicial;

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
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
            PendentesCategorizacao = regsOrdenados.Sum(r => r.Saidas.Count(s => s.PendenteCategorizacao)),
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

            var metasVinculadas = metasDoCliente.Where(m => m.ContaInvestimentoId == c.Id).ToList();
            dto.MetasVinculadas = metasVinculadas
                .Select(m => new MetaVinculadaDto { Id = m.Id, Ano = m.Ano, Sonho = m.Sonho, ValorSonho = m.ValorSonho })
                .ToList();

            var somaMetas = metasVinculadas.Sum(m => m.ValorSonho);
            dto.ProgressoCombinadoPercentual = somaMetas > 0
                ? Math.Round(Math.Min(1m, saldoAtual / somaMetas) * 100, 1)
                : null;
        }

        return dto;
    }
}
