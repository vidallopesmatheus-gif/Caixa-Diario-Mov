import { calcularCustoDeVida, calcularFire } from './fire'
import type { Registro, CategoriaAdmin, ItemFinanceiroSaida } from '../types'

const HOJE = new Date(2026, 9, 2) // 2026-10-02 (mês corrente = outubro, nunca entra na janela)

function categoria(nome: string, ehPessoal = false): CategoriaAdmin {
  return {
    id: nome, nome, tipo: 'CustoFixo', grupoId: 'g1', grupoNome: 'Grupo', bloco: 'DESPESAS OPERACIONAIS',
    ordem: 0, ativa: true, ehEntrada: false, ehSaida: true, ehPessoal,
  }
}

function saida(descricao: string, valor: number, categoria: string, tipoCusto: ItemFinanceiroSaida['tipoCusto'] = 'CustoFixo'): ItemFinanceiroSaida {
  return { descricao, valor, categoria, subcategoria: '', tipoCusto }
}

function registro(data: string, saidas: ItemFinanceiroSaida[]): Registro {
  return {
    id: data, clienteId: 'c1', contaBancariaId: 'conta-1', data, saldoInicio: 0,
    entradas: [], saidas, contasAReceber: [], contasAPagar: [], saldoConfirmado: 0, saldoCalculado: 0, criadoEm: data,
  }
}

describe('calcularCustoDeVida', () => {
  it('usa o valor manual quando presente, sem olhar lançamentos', () => {
    const resultado = calcularCustoDeVida([], [], 8000, HOJE)
    expect(resultado).toEqual({ valor: 8000, origem: 'manual', mesesConsiderados: 0 })
  })

  it('ignora manual zero ou negativo e cai pro automático', () => {
    const categorias = [categoria('Pró-labore', true)]
    const resultado = calcularCustoDeVida([], categorias, 0, HOJE)
    expect(resultado.origem).toBe('vazio') // sem registros também
  })

  it('vazio quando nenhuma categoria está marcada como pessoal', () => {
    const resultado = calcularCustoDeVida([], [], null, HOJE)
    expect(resultado).toEqual({ valor: null, origem: 'vazio', mesesConsiderados: 0 })
  })

  it('vazio quando há categoria pessoal mas menos de 3 meses fechados com dado', () => {
    const categorias = [categoria('Pró-labore', true)]
    const registros = [
      registro('2026-09-10', [saida('Retirada', 5000, 'Pró-labore')]),
      registro('2026-08-10', [saida('Retirada', 5000, 'Pró-labore')]),
    ]
    const resultado = calcularCustoDeVida(registros, categorias, null, HOJE)
    expect(resultado.origem).toBe('vazio')
    expect(resultado.mesesConsiderados).toBe(2)
  })

  it('média automática dos últimos 12 meses fechados com pelo menos 3 meses de dado', () => {
    const categorias = [categoria('Pró-labore', true)]
    const registros = [
      registro('2026-09-10', [saida('Retirada', 6000, 'Pró-labore')]),
      registro('2026-08-10', [saida('Retirada', 5000, 'Pró-labore')]),
      registro('2026-07-10', [saida('Retirada', 4000, 'Pró-labore')]),
    ]
    const resultado = calcularCustoDeVida(registros, categorias, null, HOJE)
    expect(resultado.origem).toBe('automatico')
    expect(resultado.mesesConsiderados).toBe(3)
    expect(resultado.valor).toBeCloseTo(5000, 5) // (6000+5000+4000)/3
  })

  it('não conta o mês corrente (incompleto) nem meses fora da janela de 12', () => {
    const categorias = [categoria('Pró-labore', true)]
    const registros = [
      registro('2026-10-01', [saida('Retirada', 99999, 'Pró-labore')]), // mês corrente — ignorado
      registro('2025-01-10', [saida('Retirada', 99999, 'Pró-labore')]), // fora da janela de 12 meses
      registro('2026-09-10', [saida('Retirada', 6000, 'Pró-labore')]),
      registro('2026-08-10', [saida('Retirada', 5000, 'Pró-labore')]),
      registro('2026-07-10', [saida('Retirada', 4000, 'Pró-labore')]),
    ]
    const resultado = calcularCustoDeVida(registros, categorias, null, HOJE)
    expect(resultado.valor).toBeCloseTo(5000, 5)
    expect(resultado.mesesConsiderados).toBe(3)
  })

  it('ignora saídas de categorias não marcadas como pessoais', () => {
    const categorias = [categoria('Pró-labore', true), categoria('Aluguel', false)]
    const registros = [
      registro('2026-09-10', [saida('Retirada', 5000, 'Pró-labore'), saida('Aluguel do escritório', 3000, 'Aluguel')]),
      registro('2026-08-10', [saida('Retirada', 5000, 'Pró-labore')]),
      registro('2026-07-10', [saida('Retirada', 5000, 'Pró-labore')]),
    ]
    const resultado = calcularCustoDeVida(registros, categorias, null, HOJE)
    expect(resultado.valor).toBeCloseTo(5000, 5) // aluguel nunca entra
  })

  it('ignora transferências/investimento mesmo com categoria marcada como pessoal por engano', () => {
    const categorias = [categoria('Pró-labore', true)]
    const registros = [
      registro('2026-09-10', [saida('Retirada', 5000, 'Pró-labore', 'CustoFixo')]),
      registro('2026-08-10', [saida('Aporte', 10000, 'Pró-labore', 'Investimento')]),
      registro('2026-07-10', [saida('Retirada', 5000, 'Pró-labore', 'CustoFixo')]),
    ]
    const resultado = calcularCustoDeVida(registros, categorias, null, HOJE)
    // Só setembro e julho contam de verdade (ehOperacional exclui Investimento) — menos de 3 meses com dado operacional.
    expect(resultado.origem).toBe('vazio')
  })
})

describe('calcularFire', () => {
  it('retorna null sem custo de vida', () => {
    expect(calcularFire(null, 4, 100000)).toBeNull()
    expect(calcularFire(0, 4, 100000)).toBeNull()
  })

  it('retorna null sem taxa de retirada', () => {
    expect(calcularFire(8000, null, 100000)).toBeNull()
    expect(calcularFire(8000, 0, 100000)).toBeNull()
  })

  it('calcula valor-alvo, percentual e faltam com capital zero', () => {
    const resultado = calcularFire(8000, 4, 0)
    expect(resultado).not.toBeNull()
    expect(resultado!.valorAlvo).toBeCloseTo(2400000, 2) // 8000*12/0.04
    expect(resultado!.percentualAtingido).toBe(0)
    expect(resultado!.faltam).toBeCloseTo(2400000, 2)
    expect(resultado!.atingido).toBe(false)
  })

  it('trata capital ausente (null/undefined) como zero, sem lançar exceção', () => {
    expect(calcularFire(8000, 4, null)!.percentualAtingido).toBe(0)
    expect(calcularFire(8000, 4, undefined)!.percentualAtingido).toBe(0)
  })

  it('marca como atingido quando capital >= valor-alvo, sem passar de 100%', () => {
    const resultado = calcularFire(1000, 4, 999999999)
    expect(resultado!.atingido).toBe(true)
    expect(resultado!.percentualAtingido).toBe(100)
    expect(resultado!.faltam).toBe(0)
  })

  it('taxa de retirada maior reduz o valor-alvo', () => {
    const comQuatro = calcularFire(8000, 4, 0)!
    const comSeis = calcularFire(8000, 6, 0)!
    expect(comSeis.valorAlvo).toBeLessThan(comQuatro.valorAlvo)
  })
})
