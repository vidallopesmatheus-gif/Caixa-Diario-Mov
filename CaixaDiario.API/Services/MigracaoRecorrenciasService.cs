using CaixaDiario.API.DTOs.Admin;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

// Fase 0.2: antes da correção em MaterializarMesAtualAsync, uma ocorrência de recorrência podia
// ser materializada no registro de QUALQUER conta que já existisse naquele dia (nunca checava se
// era a conta certa). Esta migration corrige o estrago já feito: move cada ContaProvisionada
// (RecorrenciaId preenchido) presa no registro de uma conta errada pro registro da conta correta
// da própria recorrência, e some com registros que ficarem vazios depois disso.
public class MigracaoRecorrenciasService : IMigracaoRecorrenciasService
{
    private readonly IContaRecorrenteRepository _contaRecorrenteRepo;
    private readonly IRegistroRepository _registroRepo;
    private readonly IContaBancariaRepository _contaBancariaRepo;

    public MigracaoRecorrenciasService(
        IContaRecorrenteRepository contaRecorrenteRepo, IRegistroRepository registroRepo,
        IContaBancariaRepository contaBancariaRepo)
    {
        _contaRecorrenteRepo = contaRecorrenteRepo;
        _registroRepo = registroRepo;
        _contaBancariaRepo = contaBancariaRepo;
    }

    public async Task<MigracaoRecorrenciasResultDto> MigrarOcorrenciasSemContaCorretaAsync(bool confirmar)
    {
        var resultado = new MigracaoRecorrenciasResultDto { Confirmado = confirmar };
        var recorrencias = await _contaRecorrenteRepo.ListarTodasAsync();

        var registrosParaCriar = new Dictionary<Guid, RegistroDiario>();
        var registrosParaAtualizar = new Dictionary<Guid, RegistroDiario>();
        // Cache de registro-destino (conta, data) pra nunca criar dois registros novos pro mesmo
        // par dentro desta mesma execução — ResolverOuCriarAsync só sabe o que já está no banco,
        // não o que esta migration já decidiu criar (e ainda não persistiu, se for dry-run).
        var destinoCache = new Dictionary<(Guid Conta, DateOnly Data), RegistroDiario>();

        async Task<RegistroDiario> ObterDestinoAsync(ContaBancaria contaDestino, DateOnly data)
        {
            var chave = (contaDestino.Id, data);
            if (destinoCache.TryGetValue(chave, out var existente)) return existente;

            var (registro, novo) = await RegistroDiaHelper.ResolverOuCriarAsync(_registroRepo, contaDestino, data);
            destinoCache[chave] = registro;
            if (novo)
            {
                registrosParaCriar[registro.Id] = registro;
                resultado.RegistrosCriados++;
            }
            return registro;
        }

        void MoverItem(RegistroDiario origem, RegistroDiario destino, ContaProvisionada item, bool ehReceber)
        {
            if (ehReceber)
            {
                origem.ContasReceber = origem.ContasReceber.Where(c => !ReferenceEquals(c, item)).ToList();
                destino.ContasReceber = new List<ContaProvisionada>(destino.ContasReceber) { item };
            }
            else
            {
                origem.ContasPagar = origem.ContasPagar.Where(c => !ReferenceEquals(c, item)).ToList();
                destino.ContasPagar = new List<ContaProvisionada>(destino.ContasPagar) { item };
            }
            origem.SalvoEm = DateTime.UtcNow;
            destino.SalvoEm = DateTime.UtcNow;
            registrosParaAtualizar[origem.Id] = origem;
            if (!registrosParaCriar.ContainsKey(destino.Id)) registrosParaAtualizar[destino.Id] = destino;
            resultado.ItensMovidos++;
        }

        foreach (var grupo in recorrencias.GroupBy(r => r.ClienteId))
        {
            var clienteId = grupo.Key;
            var registrosDoCliente = await _registroRepo.ListarPorClienteAsync(clienteId);

            foreach (var recorrencia in grupo)
            {
                var contaDestino = await _contaBancariaRepo.ObterPorIdAsync(recorrencia.ContaBancariaId);
                if (contaDestino == null)
                {
                    resultado.ItensQuePrecisamRevisaoManual.Add(
                        $"Cliente {clienteId}: recorrência \"{recorrencia.Descricao}\" aponta pra uma conta bancária que não existe mais ({recorrencia.ContaBancariaId}).");
                    continue;
                }

                foreach (var registroOrigem in registrosDoCliente.Where(r => r.ContaBancariaId != recorrencia.ContaBancariaId))
                {
                    foreach (var item in registroOrigem.ContasReceber.Where(c => c.RecorrenciaId == recorrencia.Id).ToList())
                        await TratarItemAsync(item, ehReceber: true);
                    foreach (var item in registroOrigem.ContasPagar.Where(c => c.RecorrenciaId == recorrencia.Id).ToList())
                        await TratarItemAsync(item, ehReceber: false);

                    async Task TratarItemAsync(ContaProvisionada item, bool ehReceber)
                    {
                        if (item.Pago)
                        {
                            var tipo = ehReceber ? "receber" : "pagar";
                            resultado.ItensQuePrecisamRevisaoManual.Add(
                                $"Cliente {clienteId}: \"{item.Descricao}\" ({tipo}, R$ {item.Valor:N2}, venc. {item.DataVencimento}) " +
                                $"já está pago no registro da conta {registroOrigem.ContaBancariaId}, mas pertence à conta {recorrencia.ContaBancariaId} — " +
                                "não movida automaticamente (afetaria saldo histórico). Revisar manualmente.");
                            return;
                        }

                        var dataAlvo = item.DataVencimento ?? registroOrigem.Data;
                        var destino = await ObterDestinoAsync(contaDestino, dataAlvo);
                        MoverItem(registroOrigem, destino, item, ehReceber);
                        resultado.Detalhes.Add(
                            $"Cliente {clienteId}: \"{item.Descricao}\" (venc. {dataAlvo}) movida do registro de {registroOrigem.Data} " +
                            $"(conta {registroOrigem.ContaBancariaId}) pro registro da conta {contaDestino.Id}.");
                    }
                }
            }
        }

        // Registros de origem que esvaziaram de verdade depois da mudança — nunca os que ainda
        // guardam qualquer outra coisa (entrada/saída manual, outra pendência etc.).
        foreach (var registro in registrosParaAtualizar.Values)
        {
            if (registro.Excluido) continue;
            if (registro.Entradas.Count == 0 && registro.Saidas.Count == 0 &&
                registro.ContasReceber.Count == 0 && registro.ContasPagar.Count == 0)
            {
                registro.Excluido = true;
                registro.MotivoExclusao = "Migração 0.2: registro ficou vazio após mover ocorrências de recorrência para a conta correta.";
                resultado.RegistrosExcluidosVazios++;
            }
        }

        if (confirmar)
        {
            foreach (var registro in registrosParaCriar.Values)
                await _registroRepo.AdicionarAsync(registro);
            foreach (var registro in registrosParaAtualizar.Values)
                await _registroRepo.AtualizarAsync(registro);
        }

        return resultado;
    }
}
