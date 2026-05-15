import { renderHook, waitFor } from '@testing-library/react'
import { useUsuarios } from './useUsuarios'
import * as api from '../api/usuarios'

vi.mock('../api/usuarios')

const mockUsuarios = [
  { id: '1', nomeUsuario: 'cli1', nomeCompleto: 'Cliente 1', nomeEstabelecimento: 'Loja 1', perfil: 'cliente' as const, ativo: true, criadoEm: '', criadoPor: '' }
]

test('carrega usuários ao montar', async () => {
  vi.mocked(api.listarUsuarios).mockResolvedValue({ dados: mockUsuarios, codigoRetorno: 'OK', mensagem: '' })
  const { result } = renderHook(() => useUsuarios())
  await waitFor(() => expect(result.current.loading).toBe(false))
  expect(result.current.usuarios).toHaveLength(1)
  expect(result.current.usuarios[0].nomeCompleto).toBe('Cliente 1')
})

test('expõe erro quando API falha', async () => {
  vi.mocked(api.listarUsuarios).mockRejectedValue(new Error('Sem conexão'))
  const { result } = renderHook(() => useUsuarios())
  await waitFor(() => expect(result.current.loading).toBe(false))
  expect(result.current.erro).toBe('Sem conexão')
})
