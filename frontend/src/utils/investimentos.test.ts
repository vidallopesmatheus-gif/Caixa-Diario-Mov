import { calcularCapitalInvestido, calcularProjecaoSelic } from './investimentos'
import type { ContaBancaria, Registro } from '../types'

function criarConta(overrides: Partial<ContaBancaria> = {}): ContaBancaria {
  return {
    id: 'conta-1', clienteId: 'cliente-1', nome: 'Conta', tipo: 'ContaCorrente',
    saldoInicial: 0, saldoAtual: 0, entradasMes: 0, saidasMes: 0, pendentesCategorizacao: 0,
    ativa: true, dataCriacao: '2026-01-01',
    ...overrides,
  }
}

function criarRegistro(overrides: Partial<Registro> = {}): Registro {
  return {
    id: 'reg-1', clienteId: 'cliente-1', contaBancariaId: 'conta-1', data: '2026-09-01',
    saldoInicio: 0, entradas: [], saidas: [], contasAReceber: [], contasAPagar: [],
    saldoConfirmado: 0, saldoCalculado: 0, criadoEm: '2026-09-01',
    ...overrides,
  }
}

describe('calcularCapitalInvestido', () => {
  it('retorna zero sem contas nem registros', () => {
    expect(calcularCapitalInvestido([], [])).toBe(0)
  })

  it('soma o saldo das contas tipo Investimento ativas', () => {
    const contas = [
      criarConta({ id: 'inv-1', tipo: 'Investimento', saldoAtual: 5000 }),
      criarConta({ id: 'inv-2', tipo: 'Investimento', saldoAtual: 3000 }),
      criarConta({ id: 'cc-1', tipo: 'ContaCorrente', saldoAtual: 10000 }),
    ]
    expect(calcularCapitalInvestido(contas, [])).toBe(8000)
  })

  it('ignora conta Investimento inativa', () => {
    const contas = [criarConta({ id: 'inv-1', tipo: 'Investimento', saldoAtual: 5000, ativa: false })]
    expect(calcularCapitalInvestido(contas, [])).toBe(0)
  })

  it('soma aporte lançado como categoria Investimento numa conta corrente comum', () => {
    // Cliente sem conta de investimento cadastrada — aplicação em CDB categorizada direto como
    // saída da conta corrente. Sem essa soma, o capital investido inteiro some do dashboard.
    const contas = [criarConta({ id: 'cc-1', tipo: 'ContaCorrente' })]
    const registros = [criarRegistro({
      contaBancariaId: 'cc-1',
      saidas: [{ id: 's1', descricao: 'Aplicação CDB', valor: 2000, categoria: 'Investimento', subcategoria: '', tipoCusto: 'Investimento' }],
    })]
    expect(calcularCapitalInvestido(contas, registros)).toBe(2000)
  })

  it('subtrai resgate lançado como categoria Investimento numa conta corrente comum', () => {
    const contas = [criarConta({ id: 'cc-1', tipo: 'ContaCorrente' })]
    const registros = [
      criarRegistro({
        contaBancariaId: 'cc-1', data: '2026-01-01',
        saidas: [{ id: 's1', descricao: 'Aplicação CDB', valor: 5000, categoria: 'Investimento', subcategoria: '', tipoCusto: 'Investimento' }],
      }),
      criarRegistro({
        contaBancariaId: 'cc-1', data: '2026-06-01',
        entradas: [{ id: 'e1', descricao: 'Resgate CDB', valor: 2000, categoria: 'Investimento', tipoCusto: 'Investimento' }],
      }),
    ]
    expect(calcularCapitalInvestido(contas, registros)).toBe(3000)
  })

  it('não soma de novo um aporte feito por transferência pra uma conta Investimento existente', () => {
    // A transferência já move o saldo da conta Investimento diretamente — contar o lançamento
    // tipoCusto Investimento dentro DELA MESMA duplicaria o valor.
    const contas = [criarConta({ id: 'inv-1', tipo: 'Investimento', saldoAtual: 5000 })]
    const registros = [criarRegistro({
      contaBancariaId: 'inv-1',
      entradas: [{ id: 'e1', descricao: 'Transferência recebida', valor: 5000, categoria: 'Investimento', tipoCusto: 'Investimento' }],
    })]
    expect(calcularCapitalInvestido(contas, registros)).toBe(5000)
  })

  it('ignora lançamentos com outras categorias (não entram no capital investido)', () => {
    const contas = [criarConta({ id: 'cc-1', tipo: 'ContaCorrente' })]
    const registros = [criarRegistro({
      contaBancariaId: 'cc-1',
      saidas: [{ id: 's1', descricao: 'Aluguel', valor: 2000, categoria: 'Aluguel', subcategoria: '', tipoCusto: 'CustoFixo' }],
    })]
    expect(calcularCapitalInvestido(contas, registros)).toBe(0)
  })
})

describe('calcularProjecaoSelic', () => {
  it('retorna null com capital zero', () => {
    expect(calcularProjecaoSelic(0, 13.75)).toBeNull()
  })

  it('retorna null com capital ausente (null/undefined)', () => {
    expect(calcularProjecaoSelic(null, 13.75)).toBeNull()
    expect(calcularProjecaoSelic(undefined, 13.75)).toBeNull()
  })

  it('retorna null com SELIC ausente (null/undefined/zero)', () => {
    expect(calcularProjecaoSelic(10000, null)).toBeNull()
    expect(calcularProjecaoSelic(10000, undefined)).toBeNull()
    expect(calcularProjecaoSelic(10000, 0)).toBeNull()
  })

  it('projeta capital composto à SELIC em 10/20/30 anos', () => {
    const resultado = calcularProjecaoSelic(10000, 10)
    expect(resultado).not.toBeNull()
    expect(resultado!.dez).toBeCloseTo(10000 * Math.pow(1.1, 10), 2)
    expect(resultado!.vinte).toBeCloseTo(10000 * Math.pow(1.1, 20), 2)
    expect(resultado!.trinta).toBeCloseTo(10000 * Math.pow(1.1, 30), 2)
  })
})
