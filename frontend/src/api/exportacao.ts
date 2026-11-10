import { apiFetchResposta } from './client'

/** Baixa um arquivo da API (PDF/XLSX/CSV) já autenticado — a sessão renova sozinha se vencer. */
export async function baixarArquivo(path: string): Promise<Blob> {
  const res = await apiFetchResposta(path)
  if (!res.ok) throw new Error(`Erro ${res.status}`)
  return res.blob()
}

/** Dispara o "salvar como" do navegador pra um Blob já baixado. */
export function salvarNoDispositivo(blob: Blob, nomeArquivo: string) {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = nomeArquivo
  a.click()
  URL.revokeObjectURL(url)
}
