/**
 * Utilitário de taxonomia de categorias financeiras.
 * Reutilizado pela pizza chart (Task A8) e pelo select de saídas (Task B2).
 *
 * Grupos definidos pela decisão D5:
 *   Custos Diretos | Pessoas | Despesas Administrativas |
 *   Marketing | Impostos | Financeiras | Investimentos
 *
 * REGRA CRÍTICA: os nomes-chave usados pelo MetricasService
 *   "Insumos/Mercadoria", "Salários/Folha", "Manutenção"
 * devem permanecer com grafia idêntica.
 */

/** Mapa nome-de-categoria → grupo */
const MAPA_GRUPO: Record<string, string> = {
  // Custos Diretos
  'Insumos/Mercadoria': 'Custos Diretos',
  'Embalagens': 'Custos Diretos',
  'Comissões': 'Custos Diretos',
  // Pessoas
  'Salários/Folha': 'Pessoas',
  'Encargos': 'Pessoas',
  'Benefícios': 'Pessoas',
  'Pró-labore': 'Pessoas',
  // Despesas Administrativas
  'Aluguel': 'Despesas Administrativas',
  'Energia/Água/Internet': 'Despesas Administrativas',
  'Seguros': 'Despesas Administrativas',
  'Manutenção': 'Despesas Administrativas',
  'Material de Escritório': 'Despesas Administrativas',
  // Marketing
  'Publicidade': 'Marketing',
  'Mídia paga': 'Marketing',
  'Material gráfico': 'Marketing',
  // Impostos
  'Simples/DAS': 'Impostos',
  'ISS': 'Impostos',
  'Outros tributos': 'Impostos',
  // Financeiras
  'Tarifas bancárias': 'Financeiras',
  'Juros': 'Financeiras',
  'IOF': 'Financeiras',
  // Investimentos
  'Equipamentos': 'Investimentos',
  'Reformas': 'Investimentos',
  'Software': 'Investimentos',
}

/** Retorna o grupo de uma categoria pelo nome. Fallback: "Outros". */
export function grupoDaCategoria(cat?: string): string {
  if (!cat) return 'Outros'
  return MAPA_GRUPO[cat] ?? 'Outros'
}

/** Ordem canônica dos grupos para exibição */
export const ORDEM_GRUPOS: string[] = [
  'Custos Diretos',
  'Pessoas',
  'Despesas Administrativas',
  'Marketing',
  'Impostos',
  'Financeiras',
  'Investimentos',
  'Outros',
]

/**
 * Cor representativa por grupo.
 * Usada pela pizza chart (Task A8) — cada grupo tem uma cor distinta.
 */
export const CORES_GRUPO: Record<string, string> = {
  'Custos Diretos': '#ff6b6b',
  'Pessoas': '#ffa94d',
  'Despesas Administrativas': '#ffd43b',
  'Marketing': '#74c0fc',
  'Impostos': '#e599f7',
  'Financeiras': '#a9e34b',
  'Investimentos': '#63e6be',
  'Outros': '#868e96',
}
