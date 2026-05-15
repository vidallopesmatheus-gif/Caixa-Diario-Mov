using CaixaDiario.API.DTOs.Usuarios;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class UsuarioServiceTests
{
    private readonly Mock<IUsuarioRepository> _repoMock = new();
    private readonly UsuarioService _sut;

    public UsuarioServiceTests()
    {
        _sut = new UsuarioService(_repoMock.Object);
    }

    private static Usuario CriarUsuario() => new()
    {
        Id = Guid.NewGuid(),
        NomeUsuario = "cliente1",
        SenhaHash = BCrypt.Net.BCrypt.HashPassword("senha1"),
        Nome = "Cliente Um",
        Perfil = "cliente",
        Ativo = true,
        CriadoEm = DateTime.UtcNow
    };

    [Fact]
    public async Task Criar_DadosValidos_RetornaUsuarioCriado()
    {
        _repoMock.Setup(r => r.ExisteNomeUsuarioAsync("novocliente", null)).ReturnsAsync(false);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Usuario>())).ReturnsAsync((Usuario u) => u);

        var dto = new CriarUsuarioDto { NomeUsuario = "novocliente", Senha = "senha1", Nome = "Novo Cliente", Loja = "Loja X" };
        var resultado = await _sut.CriarAsync(dto, "admin");

        Assert.Equal("novocliente", resultado.NomeUsuario);
        Assert.Equal("Novo Cliente", resultado.Nome);
        Assert.Equal("cliente", resultado.Perfil);
        Assert.True(resultado.Ativo);
    }

    [Fact]
    public async Task Criar_NomeUsuarioDuplicado_LancaNomeUsuarioDuplicado()
    {
        _repoMock.Setup(r => r.ExisteNomeUsuarioAsync("duplicado", null)).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.CriarAsync(new CriarUsuarioDto { NomeUsuario = "duplicado", Senha = "senha1", Nome = "Teste" }, "admin"));

        Assert.Equal(409, ex.StatusCode);
        Assert.Equal(CodigoRetorno.NOME_USUARIO_DUPLICADO, ex.Codigo);
    }

    [Fact]
    public async Task Criar_SenhaCurta_LancaSenhaMuitoCurta()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.CriarAsync(new CriarUsuarioDto { NomeUsuario = "user", Senha = "abc", Nome = "Teste" }, "admin"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.SENHA_MUITO_CURTA, ex.Codigo);
    }

    [Theory]
    [InlineData("", "senha1", "Nome")]
    [InlineData("user", "senha1", "")]
    public async Task Criar_CamposObrigatoriosVazios_LancaDadosInvalidos(string nomeUsuario, string senha, string nome)
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.CriarAsync(new CriarUsuarioDto { NomeUsuario = nomeUsuario, Senha = senha, Nome = nome }, "admin"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task Desativar_UsuarioInexistente_LancaUsuarioNaoEncontrado()
    {
        _repoMock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>())).ReturnsAsync((Usuario?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.DesativarAsync(Guid.NewGuid(), "admin"));

        Assert.Equal(404, ex.StatusCode);
        Assert.Equal(CodigoRetorno.USUARIO_NAO_ENCONTRADO, ex.Codigo);
    }

    [Fact]
    public async Task Desativar_UsuarioExistente_SetaAtivoFalse()
    {
        var usuario = CriarUsuario();
        _repoMock.Setup(r => r.ObterPorIdAsync(usuario.Id)).ReturnsAsync(usuario);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<Usuario>())).ReturnsAsync((Usuario u) => u);

        await _sut.DesativarAsync(usuario.Id, "admin");

        _repoMock.Verify(r => r.AtualizarAsync(It.Is<Usuario>(u => !u.Ativo)), Times.Once);
    }

    [Fact]
    public async Task Listar_RetornaListaDeUsuarios()
    {
        var lista = new List<Usuario> { CriarUsuario(), CriarUsuario() };
        _repoMock.Setup(r => r.ListarAtivosAsync()).ReturnsAsync(lista);

        var resultado = await _sut.ListarAsync();

        Assert.Equal(2, resultado.Count);
    }

    [Fact]
    public async Task ObterPorId_UsuarioExistente_RetornaUsuario()
    {
        var usuario = CriarUsuario();
        _repoMock.Setup(r => r.ObterPorIdAsync(usuario.Id)).ReturnsAsync(usuario);

        var resultado = await _sut.ObterPorIdAsync(usuario.Id);

        Assert.Equal(usuario.Id, resultado.Id);
        Assert.Equal(usuario.NomeUsuario, resultado.NomeUsuario);
    }

    [Fact]
    public async Task ObterPorId_UsuarioInexistente_LancaUsuarioNaoEncontrado()
    {
        _repoMock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>())).ReturnsAsync((Usuario?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ObterPorIdAsync(Guid.NewGuid()));

        Assert.Equal(404, ex.StatusCode);
        Assert.Equal(CodigoRetorno.USUARIO_NAO_ENCONTRADO, ex.Codigo);
    }

    [Fact]
    public async Task Atualizar_UsuarioInexistente_LancaUsuarioNaoEncontrado()
    {
        _repoMock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>())).ReturnsAsync((Usuario?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.AtualizarAsync(Guid.NewGuid(), new AtualizarUsuarioDto { NomeUsuario = "x", Nome = "Y" }, "admin"));

        Assert.Equal(404, ex.StatusCode);
        Assert.Equal(CodigoRetorno.USUARIO_NAO_ENCONTRADO, ex.Codigo);
    }

    [Fact]
    public async Task Atualizar_CamposObrigatoriosVazios_LancaDadosInvalidos()
    {
        var usuario = CriarUsuario();
        _repoMock.Setup(r => r.ObterPorIdAsync(usuario.Id)).ReturnsAsync(usuario);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.AtualizarAsync(usuario.Id, new AtualizarUsuarioDto { NomeUsuario = "", Nome = "Y" }, "admin"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task Atualizar_NomeCamposVazios_LancaDadosInvalidos()
    {
        var usuario = CriarUsuario();
        _repoMock.Setup(r => r.ObterPorIdAsync(usuario.Id)).ReturnsAsync(usuario);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.AtualizarAsync(usuario.Id, new AtualizarUsuarioDto { NomeUsuario = "user", Nome = "" }, "admin"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task Atualizar_SenhaCurta_LancaSenhaMuitoCurta()
    {
        var usuario = CriarUsuario();
        _repoMock.Setup(r => r.ObterPorIdAsync(usuario.Id)).ReturnsAsync(usuario);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.AtualizarAsync(usuario.Id, new AtualizarUsuarioDto { NomeUsuario = "user", Nome = "Nome", Senha = "abc" }, "admin"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.SENHA_MUITO_CURTA, ex.Codigo);
    }

    [Fact]
    public async Task Atualizar_NomeUsuarioDuplicado_LancaNomeUsuarioDuplicado()
    {
        var usuario = CriarUsuario();
        _repoMock.Setup(r => r.ObterPorIdAsync(usuario.Id)).ReturnsAsync(usuario);
        _repoMock.Setup(r => r.ExisteNomeUsuarioAsync("duplicado", usuario.Id)).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.AtualizarAsync(usuario.Id, new AtualizarUsuarioDto { NomeUsuario = "duplicado", Nome = "Nome" }, "admin"));

        Assert.Equal(409, ex.StatusCode);
        Assert.Equal(CodigoRetorno.NOME_USUARIO_DUPLICADO, ex.Codigo);
    }

    [Fact]
    public async Task Atualizar_DadosValidos_SemNovaSenha_RetornaUsuarioAtualizado()
    {
        var usuario = CriarUsuario();
        _repoMock.Setup(r => r.ObterPorIdAsync(usuario.Id)).ReturnsAsync(usuario);
        _repoMock.Setup(r => r.ExisteNomeUsuarioAsync("novouser", usuario.Id)).ReturnsAsync(false);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<Usuario>())).ReturnsAsync((Usuario u) => u);

        var resultado = await _sut.AtualizarAsync(usuario.Id,
            new AtualizarUsuarioDto { NomeUsuario = "novouser", Nome = "Novo Nome", Loja = "Loja Y" }, "admin");

        Assert.Equal("novouser", resultado.NomeUsuario);
    }

    [Fact]
    public async Task Atualizar_DadosValidos_ComNovaSenha_AtualizaSenha()
    {
        var usuario = CriarUsuario();
        var senhaHashOriginal = usuario.SenhaHash;
        _repoMock.Setup(r => r.ObterPorIdAsync(usuario.Id)).ReturnsAsync(usuario);
        _repoMock.Setup(r => r.ExisteNomeUsuarioAsync("novouser", usuario.Id)).ReturnsAsync(false);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<Usuario>())).ReturnsAsync((Usuario u) => u);

        await _sut.AtualizarAsync(usuario.Id,
            new AtualizarUsuarioDto { NomeUsuario = "novouser", Nome = "Novo Nome", Senha = "nova1234" }, "admin");

        _repoMock.Verify(r => r.AtualizarAsync(It.Is<Usuario>(u => u.SenhaHash != senhaHashOriginal)), Times.Once);
    }
}
