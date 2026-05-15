import { renderHook, waitFor } from '@testing-library/react'
import { useRegistros } from './useRegistros'
import * as api from '../api/registros'

vi.mock('../api/registros')

const mockRegistros = [
  { id: 'r1', clienteId: 'c1', data: '2026-05-15', saldoInicio: 100, entrada: 200,
    saidas: [{ descricao: 'Despesa', valor: 50 }], contasAReceber: [], contasAPagar: [],
    saldoConfirmado: 250, saldoCalculado: 250, criadoEm: '' }
]

test('não carrega quando clienteId é null', () => {
  const { result } = renderHook(() => useRegistros(null))
  expect(result.current.loading).toBe(false)
  expect(result.current.registros).toHaveLength(0)
})

test('carrega registros quando clienteId é fornecido', async () => {
  vi.mocked(api.listarRegistros).mockResolvedValue({ dados: mockRegistros, codigoRetorno: 'OK', mensagem: '' })
  const { result } = renderHook(() => useRegistros('c1'))
  await waitFor(() => expect(result.current.loading).toBe(false))
  expect(result.current.registros).toHaveLength(1)
  expect(result.current.registros[0].saldoConfirmado).toBe(250)
})

test('buscarPorData retorna null quando não encontrado', async () => {
  vi.mocked(api.listarRegistros).mockResolvedValue({ dados: [], codigoRetorno: 'OK', mensagem: '' })
  vi.mocked(api.obterRegistroPorData).mockRejectedValue(new Error('não encontrado'))
  const { result } = renderHook(() => useRegistros('c1'))
  await waitFor(() => expect(result.current.loading).toBe(false))
  const reg = await result.current.buscarPorData('2026-05-01')
  expect(reg).toBeNull()
})
