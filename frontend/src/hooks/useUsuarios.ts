import { useState, useEffect, useCallback } from 'react'
import { listarUsuarios, criarUsuario, atualizarUsuario, desativarUsuario } from '../api/usuarios'
import type { Usuario } from '../types'

export function useUsuarios() {
  const [usuarios, setUsuarios] = useState<Usuario[]>([])
  const [loading, setLoading] = useState(true)
  const [erro, setErro] = useState('')

  const carregar = useCallback(async () => {
    setLoading(true)
    try {
      const res = await listarUsuarios()
      setUsuarios(res.dados)
    } catch (e: any) {
      setErro(e.message)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { carregar() }, [carregar])

  const criar = async (dto: Parameters<typeof criarUsuario>[0]) => {
    const res = await criarUsuario(dto)
    await carregar()
    return res.dados
  }

  const atualizar = async (id: string, dto: Parameters<typeof atualizarUsuario>[1]) => {
    const res = await atualizarUsuario(id, dto)
    await carregar()
    return res.dados
  }

  const desativar = async (id: string) => {
    await desativarUsuario(id)
    await carregar()
  }

  return { usuarios, loading, erro, criar, atualizar, desativar, recarregar: carregar }
}
