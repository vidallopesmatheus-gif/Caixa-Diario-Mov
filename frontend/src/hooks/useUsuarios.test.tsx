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

test('criar chama criarUsuario e recarrega lista', async () => {
  vi.mocked(api.listarUsuarios).mockResolvedValue({ dados: mockUsuarios, codigoRetorno: 'OK', mensagem: '' })
  vi.mocked(api.criarUsuario).mockResolvedValue({ dados: mockUsuarios[0], codigoRetorno: 'OK', mensagem: '' })
  const { result } = renderHook(() => useUsuarios())
  await waitFor(() => expect(result.current.loading).toBe(false))
  const dto = { nomeUsuario: 'novo', senha: '123', nomeCompleto: 'Novo', nomeEstabelecimento: 'Loja', perfil: 'cliente' }
  const criado = await result.current.criar(dto)
  expect(api.criarUsuario).toHaveBeenCalledWith(dto)
  expect(criado).toEqual(mockUsuarios[0])
})

test('atualizar chama atualizarUsuario e recarrega lista', async () => {
  vi.mocked(api.listarUsuarios).mockResolvedValue({ dados: mockUsuarios, codigoRetorno: 'OK', mensagem: '' })
  vi.mocked(api.atualizarUsuario).mockResolvedValue({ dados: mockUsuarios[0], codigoRetorno: 'OK', mensagem: '' })
  const { result } = renderHook(() => useUsuarios())
  await waitFor(() => expect(result.current.loading).toBe(false))
  const atualizado = await result.current.atualizar('1', { nomeCompleto: 'Atualizado', nomeEstabelecimento: 'Loja2' })
  expect(api.atualizarUsuario).toHaveBeenCalledWith('1', { nomeCompleto: 'Atualizado', nomeEstabelecimento: 'Loja2' })
  expect(atualizado).toEqual(mockUsuarios[0])
})

test('desativar chama desativarUsuario e recarrega lista', async () => {
  vi.mocked(api.listarUsuarios).mockResolvedValue({ dados: mockUsuarios, codigoRetorno: 'OK', mensagem: '' })
  vi.mocked(api.desativarUsuario).mockResolvedValue({ dados: null, codigoRetorno: 'OK', mensagem: '' })
  const { result } = renderHook(() => useUsuarios())
  await waitFor(() => expect(result.current.loading).toBe(false))
  await result.current.desativar('1')
  expect(api.desativarUsuario).toHaveBeenCalledWith('1')
})

test('expõe erro como string quando lançado não-Error', async () => {
  vi.mocked(api.listarUsuarios).mockRejectedValue('erro string')
  const { result } = renderHook(() => useUsuarios())
  await waitFor(() => expect(result.current.loading).toBe(false))
  expect(result.current.erro).toBe('erro string')
})
