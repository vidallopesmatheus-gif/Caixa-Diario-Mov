namespace CaixaDiario.API.DTOs.Regras;

public class ContagemCorrespondenciaDto
{
    // Quantos lançamentos PENDENTES de categorização (na conta/tipo informados) casam com o
    // critério derivado da descrição de referência — ajuda a validar antes de salvar.
    public int Quantidade { get; set; }
}
