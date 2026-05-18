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

        var dto = new CriarUsuarioDto { NomeUsuario = "novocliente", Senha = "senha1", NomeCompleto = "Novo Cliente", NomeEstabelecimento = "Loja X" };
        var resultado = await _sut.CriarAsync(dto, "admin");

        Assert.Equal("novocliente", resultado.NomeUsuario);
        Assert.Equal("Novo Cliente", resultado.NomeCompleto);
        Assert.Equal("cliente", resultado.Perfil);
        Assert.True(resultado.Ativo);
    }

    [Fact]
    public async Task Criar_NomeUsuarioDuplicado_LancaNomeUsuarioDuplicado()
    {
        _repoMock.Setup(r => r.ExisteNomeUsuarioAsync("duplicado", null)).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.CriarAsync(new CriarUsuarioDto { NomeUsuario = "duplicado", Senha = "senha1", NomeCompleto = "Teste" }, "admin"));

        Assert.Equal(409, ex.StatusCode);
        Assert.Equal(CodigoRetorno.NOME_USUARIO_DUPLICADO, ex.Codigo);
    }

    [Fact]
    public async Task Criar_SenhaCurta_LancaSenhaMuitoCurta()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.CriarAsync(new CriarUsuarioDto { NomeUsuario = "user", Senha = "abc", NomeCompleto = "Teste" }, "admin"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.SENHA_MUITO_CURTA, ex.Codigo);
    }

    [Theory]
    [InlineData("", "senha1", "Nome")]
    [InlineData("user", "senha1", "")]
    public async Task Criar_CamposObrigatoriosVazios_LancaDadosInvalidos(string nomeUsuario, string senha, string nomeCompleto)
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.CriarAsync(new CriarUsuarioDto { NomeUsuario = nomeUsuario, Senha = senha, NomeCompleto = nomeCompleto }, "admin"));

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
            _sut.AtualizarAsync(Guid.NewGuid(), new AtualizarUsuarioDto { NomeCompleto = "Y" }, "admin"));

        Assert.Equal(404, ex.StatusCode);
        Assert.Equal(CodigoRetorno.USUARIO_NAO_ENCONTRADO, ex.Codigo);
    }

    [Fact]
    public async Task Atualizar_NomeCompletoVazio_LancaDadosInvalidos()
    {
        var usuario = CriarUsuario();
        _repoMock.Setup(r => r.ObterPorIdAsync(usuario.Id)).ReturnsAsync(usuario);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.AtualizarAsync(usuario.Id, new AtualizarUsuarioDto { NomeCompleto = "" }, "admin"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task Atualizar_SenhaCurta_LancaSenhaMuitoCurta()
    {
        var usuario = CriarUsuario();
        _repoMock.Setup(r => r.ObterPorIdAsync(usuario.Id)).ReturnsAsync(usuario);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.AtualizarAsync(usuario.Id, new AtualizarUsuarioDto { NomeCompleto = "Nome", Senha = "abc" }, "admin"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.SENHA_MUITO_CURTA, ex.Codigo);
    }

    [Fact]
    public async Task Atualizar_DadosValidos_SemNovaSenha_RetornaUsuarioAtualizado()
    {
        var usuario = CriarUsuario();
        _repoMock.Setup(r => r.ObterPorIdAsync(usuario.Id)).ReturnsAsync(usuario);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<Usuario>())).ReturnsAsync((Usuario u) => u);

        var resultado = await _sut.AtualizarAsync(usuario.Id,
            new AtualizarUsuarioDto { NomeCompleto = "Novo Nome", NomeEstabelecimento = "Loja Y" }, "admin");

        Assert.Equal("Novo Nome", resultado.NomeCompleto);
        Assert.Equal("Loja Y", resultado.NomeEstabelecimento);
    }

    [Fact]
    public async Task Atualizar_DadosValidos_ComNovaSenha_AtualizaSenha()
    {
        var usuario = CriarUsuario();
        var senhaHashOriginal = usuario.SenhaHash;
        _repoMock.Setup(r => r.ObterPorIdAsync(usuario.Id)).ReturnsAsync(usuario);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<Usuario>())).ReturnsAsync((Usuario u) => u);

        await _sut.AtualizarAsync(usuario.Id,
            new AtualizarUsuarioDto { NomeCompleto = "Novo Nome", Senha = "nova1234" }, "admin");

        _repoMock.Verify(r => r.AtualizarAsync(It.Is<Usuario>(u => u.SenhaHash != senhaHashOriginal)), Times.Once);
    }
}
