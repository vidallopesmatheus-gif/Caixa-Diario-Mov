namespace CaixaDiario.API.Models;

// Money Vision ("desloga toda hora"): sessão deslizante. O access token (JWT) é curto; este token
// longo vive só num cookie HttpOnly e é trocado por um novo a cada uso (rotação). No banco fica só
// o hash — vazar a tabela não dá acesso a nenhuma sessão.
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    // SHA-256 (hex) do valor que está no cookie.
    public string TokenHash { get; set; } = string.Empty;
    // Todos os tokens de uma mesma sessão (login) — reuso de um token já trocado revoga a família
    // inteira (sinal de que o cookie vazou).
    public Guid FamiliaId { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime ExpiraEm { get; set; }
    public DateTime? RevogadoEm { get; set; }
    // Preenchido quando o token foi revogado por ter sido trocado por um novo (não por logout/reuso).
    public bool Substituido { get; set; }
}
