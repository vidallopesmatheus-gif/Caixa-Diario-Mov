import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import CategoriaCombobox from './CategoriaCombobox'
import * as categoriasApi from '../../api/categorias'
import type { CategoriaAdmin, Grupo } from '../../types'

const grupoOcupacao: Grupo = { id: 'g-ocupacao', nome: 'Despesas com Ocupação', bloco: 'DESPESAS OPERACIONAIS', ordem: 0, ativo: true, quantidadeCategorias: 3 }
const grupoMarketing: Grupo = { id: 'g-marketing', nome: 'Despesas com Marketing', bloco: 'DESPESAS OPERACIONAIS', ordem: 1, ativo: true, quantidadeCategorias: 3 }
const grupoVendas: Grupo = { id: 'g-vendas', nome: 'Vendas e Serviços', bloco: 'RECEITAS OPERACIONAIS', ordem: 0, ativo: true, quantidadeCategorias: 1 }

describe('CategoriaCombobox — criação de categoria nova', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('permite trocar o grupo no select antes de criar (regressão: mousedown no popover travava o select)', async () => {
    const nova: CategoriaAdmin = {
      id: '1', nome: 'Frete e entrega', tipo: 'CustoFixo', grupoId: 'g-marketing', grupoNome: 'Despesas com Marketing', bloco: 'DESPESAS OPERACIONAIS', ordem: 0, ativa: true,
    }
    vi.spyOn(categoriasApi, 'listarGrupos').mockResolvedValue([grupoVendas, grupoOcupacao, grupoMarketing])
    const criarSpy = vi.spyOn(categoriasApi, 'criarCategoria').mockResolvedValue(nova)
    const user = userEvent.setup()

    render(
      <CategoriaCombobox
        categorias={[]}
        value=""
        onChange={() => {}}
        blocoPadraoNovaCategoria="DESPESAS OPERACIONAIS"
      />,
    )

    await user.type(screen.getByPlaceholderText('Buscar categoria...'), 'Frete e entrega')
    await user.click(await screen.findByText('+ Criar categoria "Frete e entrega"'))

    const select = await screen.findByRole('combobox', { name: '' }) as HTMLSelectElement
    await waitFor(() => expect(select.value).toBe('g-ocupacao')) // primeiro grupo do bloco padrão

    await user.selectOptions(select, 'g-marketing')
    expect(select.value).toBe('g-marketing')

    await user.click(screen.getByText('Criar e usar'))

    await waitFor(() => expect(criarSpy).toHaveBeenCalledWith('Frete e entrega', 'g-marketing'))
  })
})
