import { listarRegistros, obterRegistroPorData, salvarRegistro, excluirRegistro } from './registros'

const mockOk = (body: unknown) =>
  vi.mocked(fetch).mockResolvedValue({
    ok: true, status: 200, json: async () => body,
  } as Response)

beforeEach(() => vi.stubGlobal('fetch', vi.fn()))
afterEach(() => vi.unstubAllGlobals())

test('listarRegistros faz GET em /api/registros/:clienteId', async () => {
  mockOk({ dados: [] })
  await listarRegistros('abc-123')
  expect(vi.mocked(fetch)).toHaveBeenCalledWith('/api/registros/abc-123', expect.any(Object))
})

test('obterRegistroPorData faz GET em /api/registros/:clienteId/:data', async () => {
  mockOk({ dados: null })
  await obterRegistroPorData('abc-123', '2026-05-15')
  expect(vi.mocked(fetch)).toHaveBeenCalledWith('/api/registros/abc-123/2026-05-15', expect.any(Object))
})

test('salvarRegistro faz POST em /api/registros', async () => {
  mockOk({ dados: {} })
  await salvarRegistro({
    clienteId: 'c1', data: '2026-05-15', saldoInicio: 0,
    entrada: 0, saidas: [], contasAReceber: [], contasAPagar: [], saldoConfirmado: 0,
  })
  expect(vi.mocked(fetch)).toHaveBeenCalledWith(
    '/api/registros',
    expect.objectContaining({ method: 'POST' })
  )
})

test('excluirRegistro faz DELETE com motivoExclusao no body', async () => {
  mockOk({ dados: null })
  await excluirRegistro('c1', '2026-05-15', 'erro de digitação')
  const call = vi.mocked(fetch).mock.calls[0][1] as RequestInit
  expect(call.method).toBe('DELETE')
  expect(JSON.parse(call.body as string)).toEqual({ motivoExclusao: 'erro de digitação' })
})
