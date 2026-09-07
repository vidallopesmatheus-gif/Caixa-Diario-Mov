import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import CategoriaCombobox from './CategoriaCombobox'
import * as categoriasApi from '../../api/categorias'
import type { CategoriaAdmin } from '../../types'

describe('CategoriaCombobox — criação de categoria nova', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('permite trocar a classificação no select antes de criar (regressão: mousedown no popover travava o select)', async () => {
    const nova: CategoriaAdmin = {
      id: '1', nome: 'Frete e entrega', tipo: 'CustoFixo', ordem: 0, ativa: true,
    }
    const criarSpy = vi.spyOn(categoriasApi, 'criarCategoria').mockResolvedValue(nova)
    const user = userEvent.setup()

    render(
      <CategoriaCombobox
        categorias={[]}
        value=""
        onChange={() => {}}
        tipoPadraoNovaCategoria="CustoVariavel"
      />,
    )

    await user.type(screen.getByPlaceholderText('Buscar categoria...'), 'Frete e entrega')
    await user.click(await screen.findByText('+ Criar categoria "Frete e entrega"'))

    const select = await screen.findByRole('combobox', { name: '' }) as HTMLSelectElement
    expect(select.value).toBe('CustoVariavel')

    await user.selectOptions(select, 'CustoFixo')
    expect(select.value).toBe('CustoFixo')

    await user.click(screen.getByText('Criar e usar'))

    await waitFor(() => expect(criarSpy).toHaveBeenCalledWith('Frete e entrega', 'CustoFixo'))
  })
})
