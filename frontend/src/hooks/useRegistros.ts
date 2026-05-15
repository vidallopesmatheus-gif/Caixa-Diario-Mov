import { useState, useEffect, useCallback } from 'react'
import { listarRegistros, salvarRegistro, excluirRegistro, obterRegistroPorData } from '../api/registros'
import type { Registro } from '../types'

export function useRegistros(clienteId: string | null) {
  const [registros, setRegistros] = useState<Registro[]>([])
  const [loading, setLoading] = useState(false)
  const [erro, setErro] = useState('')

  const carregar = useCallback(async () => {
    if (!clienteId) return
    setLoading(true)
    try {
      const res = await listarRegistros(clienteId)
      setRegistros(res.dados ?? [])
    } catch (e: any) {
      setErro(e.message)
    } finally {
      setLoading(false)
    }
  }, [clienteId])

  useEffect(() => { carregar() }, [carregar])

  const salvar = async (dto: Parameters<typeof salvarRegistro>[0]) => {
    const res = await salvarRegistro(dto)
    await carregar()
    return res.dados
  }

  const excluir = async (data: string, motivo: string) => {
    if (!clienteId) return
    await excluirRegistro(clienteId, data, motivo)
    await carregar()
  }

  const buscarPorData = async (data: string) => {
    if (!clienteId) return null
    try {
      const res = await obterRegistroPorData(clienteId, data)
      return res.dados
    } catch {
      return null
    }
  }

  return { registros, loading, erro, salvar, excluir, buscarPorData, recarregar: carregar }
}
