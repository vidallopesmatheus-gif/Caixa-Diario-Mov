using CaixaDiario.API.DTOs.Transferencias;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

// Money Vision: "Pix recebido entrando como saída" (item 3) — script de verificação pedido no
// relato. Nunca corrige sozinho: mover um item entre Entradas/Saidas exigiria recalcular o saldo
// do dia inteiro em cadeia, e o item pode legitimamente ter outras implicações (categoria,
// auditoria) que só quem está vendo o extrato de verdade sabe resolver. Só lista pra revisão
// manual, usando as ferramentas que já existem (desfazer transferência + reclassificar).
public class VerificacaoSinalTransferenciaService : IVerificacaoSinalTransferenciaService
{
    private static readonly string[] PalavrasRecebimento = { "recebid" };
    private static readonly string[] PalavrasEnvio = { "enviad" };

    private readonly IRegistroRepository _registroRepo;
    private readonly IContaBancariaRepository _contaRepo;

    public VerificacaoSinalTransferenciaService(IRegistroRepository registroRepo, IContaBancariaRepository contaRepo)
    {
        _registroRepo = registroRepo;
        _contaRepo = contaRepo;
    }

    public async Task<List<TransferenciaSinalSuspeitoDto>> ListarSinaisSuspeitosAsync()
    {
        var registros = await _registroRepo.ListarTodosNaoExcluidosAsync();
        var nomesContas = new Dictionary<Guid, string>();
        var resultado = new List<TransferenciaSinalSuspeitoDto>();

        foreach (var registro in registros)
        {
            if (registro.ContaBancariaId is not { } contaId) continue;

            foreach (var saida in registro.Saidas.Where(s => s.TransferenciaId.HasValue && ContemPalavra(s.Descricao, PalavrasRecebimento)))
                resultado.Add(await MontarItemAsync(
                    registro.ClienteId, contaId, registro.Data, saida.Descricao, saida.Valor,
                    "Está na lista de Saídas, mas a descrição sugere recebimento (deveria ser Entrada).", nomesContas));

            foreach (var entrada in registro.Entradas.Where(e => e.TransferenciaId.HasValue && ContemPalavra(e.Descricao, PalavrasEnvio)))
                resultado.Add(await MontarItemAsync(
                    registro.ClienteId, contaId, registro.Data, entrada.Descricao, entrada.Valor,
                    "Está na lista de Entradas, mas a descrição sugere envio (deveria ser Saída).", nomesContas));
        }

        return resultado.OrderBy(i => i.Data).ToList();
    }

    private static bool ContemPalavra(string descricao, string[] palavras)
    {
        var normalizada = descricao.ToLowerInvariant();
        return palavras.Any(normalizada.Contains);
    }

    private async Task<TransferenciaSinalSuspeitoDto> MontarItemAsync(
        Guid clienteId, Guid contaId, DateOnly data, string descricao, decimal valor,
        string motivo, Dictionary<Guid, string> nomesContas)
    {
        if (!nomesContas.TryGetValue(contaId, out var nomeConta))
        {
            nomeConta = (await _contaRepo.ObterPorIdAsync(contaId))?.Nome ?? "—";
            nomesContas[contaId] = nomeConta;
        }

        return new TransferenciaSinalSuspeitoDto
        {
            ClienteId = clienteId, ContaBancariaId = contaId, ContaBancariaNome = nomeConta,
            Data = data, Descricao = descricao, Valor = valor, Motivo = motivo,
        };
    }
}
