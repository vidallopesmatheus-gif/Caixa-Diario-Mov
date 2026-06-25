using CaixaDiario.API.DTOs.Importacao;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services.Parsers;
using Microsoft.AspNetCore.Http;

namespace CaixaDiario.API.Services;

public class ImportacaoService : IImportacaoService
{
    private readonly IContaBancariaRepository _contaRepo;
    private readonly ITransacaoImportadaRepository _importRepo;
    private readonly IRegistroRepository _registroRepo;

    public ImportacaoService(
        IContaBancariaRepository contaRepo,
        ITransacaoImportadaRepository importRepo,
        IRegistroRepository registroRepo)
    {
        _contaRepo = contaRepo;
        _importRepo = importRepo;
        _registroRepo = registroRepo;
    }

    // ── Importar arquivo ──────────────────────────────────────────────────────
    public async Task<List<TransacaoImportadaDto>> ImportarArquivoAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil, IFormFile arquivo)
    {
        var conta = await ObterContaComAcesso(contaBancariaId, usuarioLogadoId, perfil);

        var ext = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        if (ext is not ".ofx" and not ".csv")
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS,
                "Formato inválido. Envie um arquivo .ofx ou .csv.");

        List<TransacaoImportada> novas;

        using var stream = arquivo.OpenReadStream();
        try
        {
            novas = ext == ".ofx"
                ? await ParsearOfx(stream, conta)
                : await ParsearCsv(stream, conta);
        }
        catch (InvalidOperationException ex)
        {
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, ex.Message);
        }

        if (novas.Count == 0)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS,
                "Nenhuma transação encontrada no arquivo. Verifique o formato.");

        // Carrega registros existentes para marcar duplicatas
        var registros = await _registroRepo.ListarPorContaAsync(contaBancariaId);
        var dtos = novas.Select(t => MapToDto(t, registros)).ToList();

        await _importRepo.AdicionarLoteAsync(novas);
        return dtos;
    }

    // ── Listar pendentes ──────────────────────────────────────────────────────
    public async Task<List<TransacaoImportadaDto>> ListarPendentesAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil)
    {
        await ObterContaComAcesso(contaBancariaId, usuarioLogadoId, perfil);
        var pendentes = await _importRepo.ListarPendentesPorContaAsync(contaBancariaId);
        var registros = await _registroRepo.ListarPorContaAsync(contaBancariaId);
        return pendentes.Select(t => MapToDto(t, registros)).ToList();
    }

    // ── Confirmar / Ignorar ───────────────────────────────────────────────────
    public async Task ConfirmarTransacoesAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil, ConfirmarTransacoesDto dto)
    {
        await ObterContaComAcesso(contaBancariaId, usuarioLogadoId, perfil);

        var ids = dto.Transacoes.Select(t => t.Id).ToList();
        var transacoes = await _importRepo.ObterPorIdsAsync(ids);

        // Valida que todas pertencem à conta solicitada
        if (transacoes.Any(t => t.ContaBancariaId != contaBancariaId))
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado a uma ou mais transações.");

        // Carrega registros diários da conta ordenados por data
        var registros = await _registroRepo.ListarPorContaAsync(contaBancariaId);
        var registrosPorData = registros.ToDictionary(r => r.Data);

        // Agrupa transações confirmadas por data para processar dia a dia
        var confirmadas = dto.Transacoes
            .Join(transacoes, item => item.Id, t => t.Id, (item, t) => (item, t))
            .Where(x => !x.item.Ignorar && x.t.Status == "Pendente")
            .ToList();

        var ignoradas = dto.Transacoes
            .Join(transacoes, item => item.Id, t => t.Id, (item, t) => (item, t))
            .Where(x => x.item.Ignorar && x.t.Status == "Pendente")
            .ToList();

        // Agrupa confirmadas por data
        var porData = confirmadas.GroupBy(x => x.t.Data).OrderBy(g => g.Key);

        foreach (var grupo in porData)
        {
            var data = grupo.Key;

            // Obtém ou cria RegistroDiario para esse dia
            RegistroDiario registro;
            bool criando = false;
            if (registrosPorData.TryGetValue(data, out var existente))
            {
                registro = existente;
            }
            else
            {
                // Saldo início = SaldoFinal do dia anterior mais recente
                var prev = registros
                    .Where(r => r.Data < data)
                    .OrderByDescending(r => r.Data)
                    .FirstOrDefault();

                // Usa SaldoFinal do dia anterior; se não houver, usa saldo do registro mais recente
                decimal inicio = prev?.SaldoFinal ?? 0m;

                registro = new RegistroDiario
                {
                    Id = Guid.NewGuid(),
                    ClienteId = contaBancariaId,  // será corrigido abaixo
                    ContaBancariaId = contaBancariaId,
                    Data = data,
                    Inicio = inicio,
                    Entradas = new List<ItemFinanceiro>(),
                    Saidas = new List<ItemFinanceiroSaida>(),
                    ContasReceber = new List<ContaProvisionada>(),
                    ContasPagar = new List<ContaProvisionada>(),
                    SaldoFinal = inicio,
                    CriadoEm = DateTime.UtcNow,
                    SalvoEm = DateTime.UtcNow,
                    UsuarioAtualizacao = "importacao",
                };

                // Corrije o ClienteId usando a conta
                var contaInfo = await _contaRepo.ObterPorIdAsync(contaBancariaId);
                registro.ClienteId = contaInfo!.ClienteId;
                criando = true;
            }

            // Aplica cada transação do grupo
            foreach (var (item, t) in grupo)
            {
                t.Categoria = item.Categoria ?? t.Categoria;

                if (t.Tipo == "Entrada")
                {
                    registro.Entradas.Add(new ItemFinanceiro
                    {
                        Descricao = t.Descricao,
                        Valor = t.Valor,
                        Categoria = t.Categoria,
                    });
                    registro.SaldoFinal += t.Valor;
                }
                else
                {
                    registro.Saidas.Add(new ItemFinanceiroSaida
                    {
                        Descricao = t.Descricao,
                        Valor = t.Valor,
                        Categoria = t.Categoria ?? "Importado",
                        Subcategoria = string.Empty,
                    });
                    registro.SaldoFinal -= t.Valor;
                }

                t.Status = "Confirmada";
            }

            registro.SalvoEm = DateTime.UtcNow;
            registro.AtualizadoEm = DateTime.UtcNow;

            if (criando)
                await _registroRepo.AdicionarAsync(registro);
            else
                await _registroRepo.AtualizarAsync(registro);
        }

        // Marca ignoradas
        foreach (var (_, t) in ignoradas)
            t.Status = "Ignorada";

        // Atualiza status das transações
        var toUpdate = confirmadas.Select(x => x.t).Concat(ignoradas.Select(x => x.t)).ToList();
        await _importRepo.AtualizarLoteAsync(toUpdate);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private async Task<ContaBancaria> ObterContaComAcesso(Guid contaBancariaId, Guid usuarioId, string perfil)
    {
        var conta = await _contaRepo.ObterPorIdAsync(contaBancariaId)
            ?? throw new ApiException(404, CodigoRetorno.REGISTRO_NAO_ENCONTRADO, "Conta bancária não encontrada.");
        if (perfil == "cliente" && usuarioId != conta.ClienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
        return conta;
    }

    private async Task<List<TransacaoImportada>> ParsearOfx(Stream stream, ContaBancaria conta)
    {
        var transacoes = OfxParser.Parse(stream);
        var resultado = new List<TransacaoImportada>();

        foreach (var t in transacoes)
        {
            // Dedup por FitId
            if (t.FitId != null && await _importRepo.ExisteFitIdAsync(conta.Id, t.FitId))
                continue;

            resultado.Add(new TransacaoImportada
            {
                Id = Guid.NewGuid(),
                ContaBancariaId = conta.Id,
                ClienteId = conta.ClienteId,
                Data = t.Data,
                Valor = t.Valor,
                Descricao = t.Descricao,
                Tipo = t.Tipo,
                FitId = t.FitId,
                Status = "Pendente",
                ImportadoEm = DateTime.UtcNow,
            });
        }

        return resultado;
    }

    private async Task<List<TransacaoImportada>> ParsearCsv(Stream stream, ContaBancaria conta)
    {
        var transacoes = CsvParser.Parse(stream);
        // Dedup: carrega importações já existentes para mesma conta
        var existentes = await _importRepo.ListarPendentesPorContaAsync(conta.Id);

        return transacoes
            .Where(t => !ExisteDuplicataImport(t, existentes))
            .Select(t => new TransacaoImportada
            {
                Id = Guid.NewGuid(),
                ContaBancariaId = conta.Id,
                ClienteId = conta.ClienteId,
                Data = t.Data,
                Valor = t.Valor,
                Descricao = t.Descricao,
                Tipo = t.Tipo,
                Status = "Pendente",
                ImportadoEm = DateTime.UtcNow,
            })
            .ToList();
    }

    // Verifica se já existe transação importada com mesma data+valor+descrição similar
    private static bool ExisteDuplicataImport(TransacaoCsv nova, List<TransacaoImportada> existentes) =>
        existentes.Any(e =>
            e.Data == nova.Data &&
            Math.Abs(e.Valor - nova.Valor) < 0.01m &&
            e.Tipo == nova.Tipo &&
            DescricaoSimilar(e.Descricao, nova.Descricao));

    private static bool DescricaoSimilar(string a, string b)
    {
        var la = a.ToUpperInvariant().Length >= 20 ? a[..20].ToUpperInvariant() : a.ToUpperInvariant();
        var lb = b.ToUpperInvariant().Length >= 20 ? b[..20].ToUpperInvariant() : b.ToUpperInvariant();
        return la == lb;
    }

    // Verifica se transação já existe nos registros diários reais
    private static bool EhDuplicataEmRegistros(TransacaoImportada t, List<RegistroDiario> registros)
    {
        var doMesmoDia = registros.Where(r => r.Data == t.Data).ToList();
        if (t.Tipo == "Entrada")
            return doMesmoDia.Any(r => r.Entradas.Any(e =>
                Math.Abs(e.Valor - t.Valor) < 0.01m &&
                DescricaoSimilar(e.Descricao, t.Descricao)));
        else
            return doMesmoDia.Any(r => r.Saidas.Any(s =>
                Math.Abs(s.Valor - t.Valor) < 0.01m &&
                DescricaoSimilar(s.Descricao, t.Descricao)));
    }

    private static TransacaoImportadaDto MapToDto(TransacaoImportada t, List<RegistroDiario> registros) =>
        new()
        {
            Id = t.Id,
            ContaBancariaId = t.ContaBancariaId,
            Data = t.Data.ToString("yyyy-MM-dd"),
            Valor = t.Valor,
            Descricao = t.Descricao,
            Tipo = t.Tipo,
            Status = t.Status,
            Categoria = t.Categoria,
            FitId = t.FitId,
            Duplicada = EhDuplicataEmRegistros(t, registros),
        };
}
