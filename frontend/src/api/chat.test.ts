import { enviarMensagem } from './chat'

beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn())
  localStorage.setItem('token', 'jwt-test')
})

afterEach(() => {
  vi.unstubAllGlobals()
  localStorage.clear()
})

test('enviarMensagem faz POST em /api/chat', async () => {
  vi.mocked(fetch).mockResolvedValue({
    ok: true,
    status: 200,
    json: async () => ({ dados: { reply: 'Olá!', wasBlocked: false }, codigoRetorno: 'SUCESSO', mensagem: '' }),
  } as Response)

  await enviarMensagem('como uso o app?', [])

  expect(vi.mocked(fetch)).toHaveBeenCalledWith(
    '/api/chat',
    expect.objectContaining({ method: 'POST' })
  )
})

test('enviarMensagem envia message e history no body', async () => {
  vi.mocked(fetch).mockResolvedValue({
    ok: true,
    status: 200,
    json: async () => ({ dados: { reply: 'ok', wasBlocked: false }, codigoRetorno: 'SUCESSO', mensagem: '' }),
  } as Response)

  const historico = [{ role: 'user' as const, content: 'oi' }]
  await enviarMensagem('como exportar?', historico)

  const call = vi.mocked(fetch).mock.calls[0][1] as RequestInit
  const body = JSON.parse(call.body as string)
  expect(body.message).toBe('como exportar?')
  expect(body.history).toEqual(historico)
})

test('enviarMensagem inclui Authorization header', async () => {
  vi.mocked(fetch).mockResolvedValue({
    ok: true,
    status: 200,
    json: async () => ({ dados: { reply: 'ok', wasBlocked: false }, codigoRetorno: 'SUCESSO', mensagem: '' }),
  } as Response)

  await enviarMensagem('oi', [])

  const call = vi.mocked(fetch).mock.calls[0][1] as RequestInit
  expect((call.headers as Record<string, string>)['Authorization']).toBe('Bearer jwt-test')
})
