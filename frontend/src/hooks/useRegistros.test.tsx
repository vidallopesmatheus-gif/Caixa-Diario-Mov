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

test('buscarPorData retorna dados quando encontrado', async () => {
  vi.mocked(api.listarRegistros).mockResolvedValue({ dados: [], codigoRetorno: 'OK', mensagem: '' })
  vi.mocked(api.obterRegistroPorData).mockResolvedValue({ dados: mockRegistros[0], codigoRetorno: 'OK', mensagem: '' })
  const { result } = renderHook(() => useRegistros('c1'))
  await waitFor(() => expect(result.current.loading).toBe(false))
  const reg = await result.current.buscarPorData('2026-05-15')
  expect(reg).toEqual(mockRegistros[0])
})

test('buscarPorData retorna null quando clienteId é null', async () => {
  const { result } = renderHook(() => useRegistros(null))
  const reg = await result.current.buscarPorData('2026-05-15')
  expect(reg).toBeNull()
})

test('salvar chama salvarRegistro e recarrega', async () => {
  vi.mocked(api.listarRegistros).mockResolvedValue({ dados: [], codigoRetorno: 'OK', mensagem: '' })
  const dto = { clienteId: 'c1', data: '2026-05-15', saldoInicio: 0, entrada: 0, saidas: [], contasAReceber: [], contasAPagar: [], saldoConfirmado: 0 }
  vi.mocked(api.salvarRegistro).mockResolvedValue({ dados: mockRegistros[0], codigoRetorno: 'OK', mensagem: '' })
  const { result } = renderHook(() => useRegistros('c1'))
  await waitFor(() => expect(result.current.loading).toBe(false))
  const saved = await result.current.salvar(dto)
  expect(api.salvarRegistro).toHaveBeenCalledWith(dto)
  expect(saved).toEqual(mockRegistros[0])
})

test('excluir chama excluirRegistro e recarrega', async () => {
  vi.mocked(api.listarRegistros).mockResolvedValue({ dados: [], codigoRetorno: 'OK', mensagem: '' })
  vi.mocked(api.excluirRegistro).mockResolvedValue({ dados: null, codigoRetorno: 'OK', mensagem: '' })
  const { result } = renderHook(() => useRegistros('c1'))
  await waitFor(() => expect(result.current.loading).toBe(false))
  await result.current.excluir('2026-05-15', 'motivo teste')
  expect(api.excluirRegistro).toHaveBeenCalledWith('c1', '2026-05-15', 'motivo teste')
})

test('excluir não faz nada quando clienteId é null', async () => {
  vi.mocked(api.excluirRegistro).mockClear()
  const { result } = renderHook(() => useRegistros(null))
  await result.current.excluir('2026-05-15', 'motivo')
  expect(api.excluirRegistro).not.toHaveBeenCalled()
})

test('expõe erro quando listarRegistros falha', async () => {
  vi.mocked(api.listarRegistros).mockRejectedValue(new Error('Falha de API'))
  const { result } = renderHook(() => useRegistros('c1'))
  await waitFor(() => expect(result.current.loading).toBe(false))
  expect(result.current.erro).toBe('Falha de API')
})
