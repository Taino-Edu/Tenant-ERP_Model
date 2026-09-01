import { test, expect } from '@playwright/test'
import {
  NAV_SECTIONS,
  currentNavItem,
  isItemVisible,
  tabBarItems,
  visibleSections,
  type NavVisibilityCtx,
} from '../lib/adminNav'
import { ADMIN_KEYBOARD_SHORTCUTS } from '../lib/adminKeyboardShortcuts'

// As permissões de operador vivem em CardGameStore/Models/PostgreSQL/Perfil.cs
// (`Permissao.Todos`). A cópia abaixo existe para que um erro de digitação no
// menu — 'comanda' em vez de 'comandas', 'usuario' em vez de 'usuarios' —
// quebre aqui, e não em produção como um item que nunca aparece pra ninguém.
// Ao somar uma permissão no backend, some aqui também.
const PERMISSOES_DO_BACKEND = [
  'dashboard', 'pdv', 'comandas', 'estoque', 'categorias',
  'usuarios', 'crediario', 'financeiro',
  'relatorios', 'anuncios', 'qrcodes', 'lgpd',
  'fiscal', 'eventos', 'timers', 'suporte', 'ia', 'restaurante',
]

const TODOS_OS_ITENS = NAV_SECTIONS.flatMap(section => section.items)

/** Rotas que existem só como atalho de teclado, sem item de menu. */
const ATALHOS_SEM_ITEM_DE_MENU = ['/admin/manual']

function contexto(overrides: Partial<NavVisibilityCtx> = {}): NavVisibilityCtx {
  return {
    isAdmin: false,
    enabledModules: ['restaurante', 'estoque', 'fiscal', 'eventos', 'ia'],
    hasPerm: () => true,
    ...overrides,
  }
}

test.describe('navegação do admin', () => {
  test('toda permissão do menu existe no backend', () => {
    const desconhecidas = TODOS_OS_ITENS
      .map(item => item.perm)
      .filter((perm): perm is string => perm !== null)
      .filter(perm => !PERMISSOES_DO_BACKEND.includes(perm))

    expect(desconhecidas).toEqual([])
  })

  test('um item some quando falta a permissão dele', () => {
    const comanda = TODOS_OS_ITENS.find(item => item.href === '/admin/comanda')!

    expect(comanda.perm).toBe('comandas')
    expect(isItemVisible(comanda, contexto({ hasPerm: perm => perm === 'comandas' }))).toBe(true)
    expect(isItemVisible(comanda, contexto({ hasPerm: perm => perm === 'dashboard' }))).toBe(false)
  })

  test('um item de módulo some quando o módulo dele está desligado', () => {
    const restaurante = TODOS_OS_ITENS.find(item => item.href === '/admin/comanda/restaurante')!

    expect(isItemVisible(restaurante, contexto({ enabledModules: [] }))).toBe(false)
    expect(isItemVisible(restaurante, contexto({ enabledModules: ['restaurante'] }))).toBe(true)
  })

  test('a barra do celular respeita permissão e módulo', () => {
    const semNada = tabBarItems(contexto({ hasPerm: () => false, enabledModules: [] }))
    expect(semNada).toEqual([])

    const soCaixa = tabBarItems(contexto({ hasPerm: perm => perm === 'pdv', enabledModules: [] }))
    expect(soCaixa.map(item => item.href)).toEqual(['/admin/venda-avulsa'])
  })

  test('o primeiro nível tem no máximo oito áreas e esconde áreas vazias', () => {
    expect(NAV_SECTIONS).toHaveLength(8)

    const operadorCaixa = visibleSections(contexto({
      isAdmin: false,
      enabledModules: [],
      hasPerm: perm => perm === 'pdv',
    }))

    expect(operadorCaixa.map(section => section.label)).toEqual(['Vendas', 'Configurações', 'Ajuda'])
    expect(operadorCaixa.flatMap(section => section.items).map(item => item.href)).not.toContain('/admin/perfis')
  })

  test('uma rota filha seleciona apenas o item mais específico', () => {
    expect(currentNavItem('/admin/comanda/restaurante')?.href).toBe('/admin/comanda/restaurante')
    expect(currentNavItem('/admin/comanda/123')?.href).toBe('/admin/comanda')
  })
})

test.describe('atalhos de teclado', () => {
  test('cada tecla é usada uma vez só', () => {
    const teclas = ADMIN_KEYBOARD_SHORTCUTS.map(atalho => atalho.key)

    expect(teclas).toEqual([...new Set(teclas)])
  })

  test('herdam a permissão e o módulo do item de menu da mesma rota', () => {
    const divergentes = ADMIN_KEYBOARD_SHORTCUTS
      .filter(atalho => atalho.route)
      .map(atalho => ({ atalho, item: TODOS_OS_ITENS.find(item => item.href === atalho.route) }))
      .filter(({ item }) => item)
      .filter(({ atalho, item }) => atalho.permission !== (item!.perm ?? undefined) || atalho.module !== item!.module)
      .map(({ atalho }) => atalho.route)

    expect(divergentes).toEqual([])
  })

  test('toda rota de atalho tem item de menu, salvo as exceções declaradas', () => {
    const orfas = ADMIN_KEYBOARD_SHORTCUTS
      .map(atalho => atalho.route)
      .filter((route): route is string => !!route)
      .filter(route => !TODOS_OS_ITENS.some(item => item.href === route))
      .filter(route => !ATALHOS_SEM_ITEM_DE_MENU.includes(route))

    expect(orfas).toEqual([])
  })
})
