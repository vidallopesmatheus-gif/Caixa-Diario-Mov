export const CATEGORIAS: Record<string, string[]> = {
  'Custos Diretos':  ['Mercadorias', 'Matéria-prima', 'Insumos', 'Fretes'],
  'Pessoas':         ['Salários', 'Pró-labore', 'Comissões', 'Benefícios'],
  'Administrativas': ['Aluguel', 'Energia', 'Internet', 'Telefonia', 'Software', 'Outros'],
  'Marketing':       ['Tráfego pago', 'Designer', 'Agência', 'Produção de conteúdo'],
  'Impostos':        ['DAS', 'ISS', 'ICMS', 'Outros tributos'],
  'Financeiras':     ['Juros', 'Tarifas bancárias', 'Empréstimos'],
  'Investimentos':   ['Equipamentos', 'Reformas', 'Veículos'],
}

export const LISTA_CATEGORIAS = Object.keys(CATEGORIAS)
