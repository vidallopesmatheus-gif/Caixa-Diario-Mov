namespace CaixaDiario.API.Models;

// Item 3.2: registra que o usuário decidiu "Manter os dois" pra um par de lançamentos sugerido
// pela ferramenta "Revisar duplicatas" (DuplicataExtratoService) — sem isso, o mesmo par volta a
// aparecer em toda abertura do modal, pra sempre, até que um dos dois lados seja excluído.
// LancamentoMenorId/LancamentoMaiorId SEMPRE armazenam o par em ordem (Guid.CompareTo) — qual
// lançamento veio como "A" ou "B" na sugestão depende da ordem de iteração, que pode mudar entre
// chamadas; normalizar aqui é o que permite um índice único e uma busca sem ambiguidade.
public class DuplicataDispensada
{
    public Guid Id { get; set; }
    public Guid ContaBancariaId { get; set; }
    public Guid LancamentoMenorId { get; set; }
    public Guid LancamentoMaiorId { get; set; }
    public DateTime CriadoEm { get; set; }
}
