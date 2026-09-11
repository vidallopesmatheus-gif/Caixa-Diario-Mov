import { portalFetch } from './portalClient'
import type { ApiResponse, PortalConciliacaoData } from '../types'

export const obterPortalConciliacao = async (token: string): Promise<PortalConciliacaoData> => {
  const res = await portalFetch<ApiResponse<PortalConciliacaoData>>(`/api/portal-conciliacao/${token}`)
  return res.dados
}

export const classificarPendentePortal = async (
  token: string,
  itens: Array<{ id: string; data: string; contaBancariaId: string; categoria: string }>,
): Promise<void> => {
  await portalFetch<ApiResponse<null>>(`/api/portal-conciliacao/${token}/classificar`, {
    method: 'POST',
    body: JSON.stringify({ itens }),
  })
}
