import { NAV_SECTIONS } from './adminNav'

export type AdminShortcutCategory = 'Operação' | 'Gestão' | 'Ajuda'

export interface AdminKeyboardShortcut {
  key: string
  label: string
  description: string
  category: AdminShortcutCategory
  route?: string
  eventName?: string
  permission?: string
  module?: string
  sidebar?: boolean
}

/** O atalho declara a tecla e o destino. Permissão e módulo NÃO se declaram
 *  aqui: quando a rota existe no menu, elas são lidas de lá. */
type ShortcutDefinition =
  Omit<AdminKeyboardShortcut, 'permission' | 'module'>
  & Partial<Pick<AdminKeyboardShortcut, 'permission' | 'module'>>

const SHORTCUT_DEFINITIONS: ShortcutDefinition[] = [
  { key: 'g', label: 'G', description: 'Painel Geral', category: 'Operação', route: '/admin/dashboard', sidebar: true },
  { key: 'd', label: 'D', description: 'Comanda (ao vivo)', category: 'Operação', route: '/admin/comanda', sidebar: true },
  { key: 'p', label: 'P', description: 'Frente de Caixa (PDV)', category: 'Operação', route: '/admin/venda-avulsa', sidebar: true },
  { key: 'e', label: 'E', description: 'Estoque', category: 'Operação', route: '/admin/estoque', sidebar: true },
  { key: 'q', label: 'Q', description: 'Gatilhos QR Code', category: 'Operação', route: '/admin/qrcodes', sidebar: true },
  // Sem rota: abre um painel por evento, então declara as regras à mão.
  { key: 'a', label: 'A', description: 'Abrir / fechar Assistente de IA', category: 'Operação', eventName: 'admin:toggle-ai', permission: 'ia', module: 'ia' },
  { key: 'u', label: 'U', description: 'Clientes / Usuários', category: 'Gestão', route: '/admin/usuarios', sidebar: true },
  { key: 'c', label: 'C', description: 'Crediário', category: 'Gestão', route: '/admin/crediario', sidebar: true },
  { key: 'f', label: 'F', description: 'Financeiro', category: 'Gestão', route: '/admin/financeiro', sidebar: true },
  { key: 'r', label: 'R', description: 'Relatórios', category: 'Gestão', route: '/admin/relatorios', sidebar: true },
  { key: 'i', label: 'I', description: 'Fiscal', category: 'Gestão', route: '/admin/fiscal', sidebar: true },
  { key: 'm', label: 'M', description: 'Mensageria', category: 'Gestão', route: '/admin/mensageria', sidebar: true },
  { key: 't', label: 'T', description: 'Timer', category: 'Gestão', route: '/admin/timer', sidebar: true },
  { key: 's', label: 'S', description: 'Configurações', category: 'Gestão', route: '/admin/configuracoes', sidebar: true },
  // Fora do menu: não tem item de navegação de onde herdar.
  { key: 'h', label: 'H', description: 'Manual completo', category: 'Ajuda', route: '/admin/manual' },
  { key: '1', label: '1', description: 'Primeiros passos', category: 'Ajuda', route: '/admin/primeiros-passos', sidebar: true },
]

const NAV_BY_HREF = new Map(NAV_SECTIONS.flatMap(section => section.items).map(item => [item.href, item]))

/**
 * Atalhos com permissão e módulo resolvidos a partir de lib/adminNav.ts.
 *
 * Antes as duas listas repetiam esse mapa à mão, e ele saiu de sincronia
 * exatamente como se esperava: a tecla D e o item Comanda pediam `dashboard`
 * quando a API exige `comandas`, e a tecla Q não exigia o módulo `restaurante`
 * que a tela exige. Herdar da navegação torna a divergência impossível — a
 * permissão de uma rota passa a existir num lugar só.
 */
export const ADMIN_KEYBOARD_SHORTCUTS: AdminKeyboardShortcut[] = SHORTCUT_DEFINITIONS.map(definition => {
  const navItem = definition.route ? NAV_BY_HREF.get(definition.route) : undefined
  return {
    ...definition,
    permission: definition.permission ?? navItem?.perm ?? undefined,
    module: definition.module ?? navItem?.module,
  }
})

export const SIDEBAR_SHORTCUT_KEYS = Object.fromEntries(
  ADMIN_KEYBOARD_SHORTCUTS
    .filter(shortcut => shortcut.sidebar && shortcut.route)
    .map(shortcut => [shortcut.route as string, shortcut.label]),
) as Record<string, string>

export function isEditableShortcutTarget(event: KeyboardEvent): boolean {
  return event.composedPath().some(node => {
    if (!(node instanceof HTMLElement)) return false
    if (node.dataset.keyboardShortcutsDisabled === 'true') return true
    if (node.isContentEditable) return true
    return ['INPUT', 'TEXTAREA', 'SELECT'].includes(node.tagName)
  })
}
