import { descartarRascunho, lerRascunho, registrarRascunho, salvarRascunhos } from './rascunhos'

function criarStorageFake(): Storage {
  const dados = new Map<string, string>()
  return {
    get length() { return dados.size },
    key: (i: number) => [...dados.keys()][i] ?? null,
    getItem: (k: string) => dados.get(k) ?? null,
    setItem: (k: string, v: string) => { dados.set(k, v) },
    removeItem: (k: string) => { dados.delete(k) },
    clear: () => { dados.clear() },
  }
}

beforeEach(() => { vi.stubGlobal('sessionStorage', criarStorageFake()) })
afterEach(() => { vi.unstubAllGlobals() })

describe('rascunhos', () => {
  it('should save only the forms that have something typed when the session expires', () => {
    const desregistrarCaixa = registrarRascunho('caixa-dia', () => ({ valor: 150 }))
    const desregistrarConta = registrarRascunho('nova-conta', () => null)

    salvarRascunhos()

    expect(lerRascunho('caixa-dia')).toEqual({ valor: 150 })
    expect(lerRascunho('nova-conta')).toBeNull()
    desregistrarCaixa()
    desregistrarConta()
  })

  it('should stop saving a form after it unregisters when the screen closes', () => {
    registrarRascunho('importacao:1', () => ({ banco: 'Nubank' }))()

    salvarRascunhos()

    expect(lerRascunho('importacao:1')).toBeNull()
  })

  it('should not bring a draft back twice when the screen already applied it', () => {
    const desregistrar = registrarRascunho('caixa-dia', () => ({ valor: 150 }))
    salvarRascunhos()
    desregistrar()

    descartarRascunho('caixa-dia')

    expect(lerRascunho('caixa-dia')).toBeNull()
  })
})
