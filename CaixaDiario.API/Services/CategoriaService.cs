using CaixaDiario.API.DTOs.Categorias;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class CategoriaService : ICategoriaService
{
    // Investimento/Financiamento podem ser lançados como entrada (captação, rendimento) ou saída
    // (amortização, aquisição) — por isso aparecem nos dois lados do combobox, diferente dos
    // outros tipos que só existem de um lado.
    private static readonly HashSet<string> TiposDeEntrada = new() { "Receita", "Investimento", "Financiamento" };
    private static readonly HashSet<string> TiposDeSaida = new()
    {
        "CustoVariavel", "CustoFixo", "DespesaNaoOperacional", "Investimento", "Financiamento", "RetiradaSocios",
    };
    // Categoria.Tipo por si só não distingue "Deduções da Receita" (bloco DeducoesDaReceita) de
    // despesa comum (bloco DespesasOperacionais) — os dois mapeiam pra "CustoFixo" (ver
    // Blocos.TipoPadrao, usado no cálculo do DRE). Na prática devolução/estorno pode ser um
    // reembolso que sai da conta (saída) ou um valor que volta pra conta (entrada) — por isso, tal
    // como Investimento/Financiamento, uma categoria desse bloco entra nos dois lados do combobox.
    // EhEntrada/EhSaida são a única fonte dessa regra — ListarAgrupadasAsync e MapToDto usam os
    // dois, pra não duplicar o cálculo de novo (foi exatamente essa duplicação, entre backend e
    // frontend, que causou o bug de "Devoluções" sumindo do combobox de entradas).
    private static bool EhEntrada(Categoria c) => TiposDeEntrada.Contains(c.Tipo) || c.Grupo.Bloco == Blocos.DeducoesDaReceita;
    private static bool EhSaida(Categoria c) => TiposDeSaida.Contains(c.Tipo);

    private readonly ICategoriaRepository _repo;
    private readonly IGrupoRepository _grupoRepo;

    public CategoriaService(ICategoriaRepository repo, IGrupoRepository grupoRepo)
    {
        _repo = repo;
        _grupoRepo = grupoRepo;
    }

    public async Task<CategoriasAgrupadasDto> ListarAgrupadasAsync(Guid clienteId)
    {
        var ativas = await _repo.ListarAtivasPorClienteAsync(clienteId);
        return new CategoriasAgrupadasDto
        {
            Entradas = ativas.Where(EhEntrada).Select(MapToItemDto).ToList(),
            Saidas = ativas.Where(EhSaida).Select(MapToItemDto).ToList(),
        };
    }

    public async Task<List<CategoriaDto>> ListarParaGerenciarAsync(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);
        var todas = await _repo.ListarTodasPorClienteAsync(clienteId);
        return todas.Select(MapToDto).ToList();
    }

    public async Task<CategoriaDto> CriarAsync(Guid clienteId, CriarCategoriaDto dto, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);
        var grupo = await ObterGrupoOuFalharAsync(dto.GrupoId);
        var nome = dto.Nome.Trim();
        if (await _repo.ObterPorNomeAsync(clienteId, nome) is not null)
            throw new ApiException(409, CodigoRetorno.CATEGORIA_DUPLICADA, "Já existe uma categoria com esse nome.");

        var maiorOrdem = (await _repo.ListarTodasPorClienteAsync(clienteId)).Select(c => c.Ordem).DefaultIfEmpty(-1).Max();

        var categoria = new Categoria
        {
            Id = Guid.NewGuid(),
            Nome = nome,
            Tipo = Blocos.TipoPadrao(grupo.Bloco),
            GrupoId = grupo.Id,
            Ordem = maiorOrdem + 1,
            Ativa = true,
            CriadoEm = DateTime.UtcNow,
            ClienteId = clienteId,
            Origem = "Cliente",
            CriadoPor = usuarioLogadoId,
        };
        var criada = await _repo.AdicionarAsync(categoria);
        criada.Grupo = grupo;
        return MapToDto(criada);
    }

    public async Task<CategoriaDto> AtualizarAsync(Guid clienteId, Guid id, AtualizarCategoriaDto dto, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);
        var grupo = await ObterGrupoOuFalharAsync(dto.GrupoId);
        var categoria = await ObterDoClienteOuFalharAsync(clienteId, id);

        var nome = dto.Nome.Trim();
        var existente = await _repo.ObterPorNomeAsync(clienteId, nome);
        if (existente is not null && existente.Id != id)
            throw new ApiException(409, CodigoRetorno.CATEGORIA_DUPLICADA, "Já existe uma categoria com esse nome.");

        categoria.Nome = nome;
        categoria.GrupoId = grupo.Id;
        categoria.Tipo = Blocos.TipoPadrao(grupo.Bloco);
        categoria.Ativa = dto.Ativa;
        var atualizada = await _repo.AtualizarAsync(categoria);
        atualizada.Grupo = grupo;
        return MapToDto(atualizada);
    }

    public async Task DesativarAsync(Guid clienteId, Guid id, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);
        var categoria = await ObterDoClienteOuFalharAsync(clienteId, id);
        categoria.Ativa = false;
        await _repo.AtualizarAsync(categoria);
    }

    public async Task ReordenarAsync(Guid clienteId, ReordenarCategoriasDto dto, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);
        var doCliente = (await _repo.ListarTodasPorClienteAsync(clienteId)).Select(c => c.Id).ToHashSet();
        if (dto.Ids.Any(id => !doCliente.Contains(id)))
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        var novaOrdem = dto.Ids.Select((id, indice) => (Id: id, Ordem: indice)).ToList();
        await _repo.ReordenarAsync(novaOrdem);
    }

    public async Task<ExclusaoCategoriaResultDto> ExcluirOuInformarUsoAsync(Guid clienteId, Guid id, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);
        var categoria = await ObterDoClienteOuFalharAsync(clienteId, id);
        var quantidade = await _repo.ContarUsoAsync(clienteId, categoria.Nome);
        if (quantidade > 0)
            return new ExclusaoCategoriaResultDto { Excluida = false, QuantidadeLancamentos = quantidade };

        await _repo.RemoverAsync(categoria);
        return new ExclusaoCategoriaResultDto { Excluida = true, QuantidadeLancamentos = 0 };
    }

    public async Task MigrarLancamentosAsync(Guid clienteId, Guid origemId, Guid destinoId, Guid usuarioLogadoId, string perfil)
    {
        VerificarAcesso(clienteId, usuarioLogadoId, perfil);
        var origem = await ObterDoClienteOuFalharAsync(clienteId, origemId);
        var destino = await ObterDoClienteOuFalharAsync(clienteId, destinoId);

        await _repo.MigrarUsoAsync(clienteId, origem.Nome, destino.Nome, destino.Tipo);

        origem.Ativa = false;
        await _repo.AtualizarAsync(origem);
    }

    private static void VerificarAcesso(Guid clienteId, Guid usuarioLogadoId, string perfil)
    {
        if (perfil == "cliente" && usuarioLogadoId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
    }

    private async Task<Categoria> ObterDoClienteOuFalharAsync(Guid clienteId, Guid id)
    {
        var categoria = await _repo.ObterPorIdAsync(id)
            ?? throw new ApiException(404, CodigoRetorno.CATEGORIA_NAO_ENCONTRADA, "Categoria não encontrada.");
        // Nunca deixa um cliente operar numa categoria de outro cliente (ou no molde padrão) só
        // porque adivinhou/reutilizou um Id — mesmo estando autenticado e com acesso ao próprio clienteId.
        if (categoria.ClienteId != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
        return categoria;
    }

    private async Task<Grupo> ObterGrupoOuFalharAsync(Guid grupoId) =>
        await _grupoRepo.ObterPorIdAsync(grupoId)
            ?? throw new ApiException(404, CodigoRetorno.GRUPO_NAO_ENCONTRADO, "Grupo não encontrado.");

    private static CategoriaItemDto MapToItemDto(Categoria c) => new()
    {
        Nome = c.Nome,
        TipoCusto = c.Tipo,
        Grupo = c.Grupo.Nome,
    };

    private static CategoriaDto MapToDto(Categoria c) => new()
    {
        Id = c.Id,
        Nome = c.Nome,
        Tipo = c.Tipo,
        GrupoId = c.GrupoId,
        GrupoNome = c.Grupo.Nome,
        Bloco = c.Grupo.Bloco,
        Ordem = c.Ordem,
        Ativa = c.Ativa,
        EhEntrada = EhEntrada(c),
        EhSaida = EhSaida(c),
    };
}
