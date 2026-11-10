/**
 * Money Vision ("desloga toda hora"): quando a sessão expira de vez, o apiFetch manda pro login —
 * e tudo o que estava digitado e não salvo se perdia. Cada formulário aberto registra aqui como
 * ler o próprio estado; antes de redirecionar, salvarRascunhos() grava tudo no sessionStorage
 * (some ao fechar a aba) e, depois do login, a tela lê o rascunho de volta.
 */
const PREFIXO = 'rascunho:'
const coletores = new Map<string, () => unknown>()

/** Registra o "leitor" do estado atual do formulário; devolve a função que desregistra. */
export function registrarRascunho(chave: string, coletar: () => unknown): () => void {
  coletores.set(chave, coletar)
  return () => {
    if (coletores.get(chave) === coletar) coletores.delete(chave)
  }
}

export function salvarRascunhos(): void {
  for (const [chave, coletar] of coletores) {
    const dados = coletar()
    if (dados === null || dados === undefined) continue
    try {
      sessionStorage.setItem(PREFIXO + chave, JSON.stringify(dados))
    } catch {
      // sessionStorage cheio/bloqueado — perde o rascunho, mas o redirect pro login continua.
    }
  }
}

/** Só lê (seguro de chamar em inicializador de useState, que roda 2x no StrictMode). */
export function lerRascunho<T>(chave: string): T | null {
  try {
    const bruto = sessionStorage.getItem(PREFIXO + chave)
    return bruto ? (JSON.parse(bruto) as T) : null
  } catch {
    return null
  }
}

/** Chamar depois que o rascunho já foi aplicado na tela — ele nunca volta duas vezes. */
export function descartarRascunho(chave: string): void {
  try {
    sessionStorage.removeItem(PREFIXO + chave)
  } catch {
    // sem sessionStorage não há o que descartar
  }
}
