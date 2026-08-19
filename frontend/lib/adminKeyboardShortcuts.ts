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

export const ADMIN_KEYBOARD_SHORTCUTS: AdminKeyboardShortcut[] = [
  { key: 'g', label: 'G', description: 'Painel Geral', category: 'Operação', route: '/admin/dashboard', permission: 'dashboard', sidebar: true },
  { key: 'd', label: 'D', description: 'Comanda (ao vivo)', category: 'Operação', route: '/admin/comanda', permission: 'comandas', module: 'restaurante', sidebar: true },
  { key: 'p', label: 'P', description: 'Frente de Caixa (PDV)', category: 'Operação', route: '/admin/venda-avulsa', permission: 'pdv', sidebar: true },
  { key: 'e', label: 'E', description: 'Estoque', category: 'Operação', route: '/admin/estoque', permission: 'estoque', module: 'estoque', sidebar: true },
  { key: 'q', label: 'Q', description: 'Gatilhos QR Code', category: 'Operação', route: '/admin/qrcodes', permission: 'qrcodes', sidebar: true },
  { key: 'a', label: 'A', description: 'Abrir / fechar Assistente de IA', category: 'Operação', eventName: 'admin:toggle-ai', permission: 'ia', module: 'ia' },
  { key: 'u', label: 'U', description: 'Clientes / Usuários', category: 'Gestão', route: '/admin/usuarios', permission: 'usuarios', sidebar: true },
  { key: 'c', label: 'C', description: 'Crediário', category: 'Gestão', route: '/admin/crediario', permission: 'crediario', sidebar: true },
  { key: 'f', label: 'F', description: 'Financeiro', category: 'Gestão', route: '/admin/financeiro', permission: 'financeiro', sidebar: true },
  { key: 'r', label: 'R', description: 'Relatórios', category: 'Gestão', route: '/admin/relatorios', permission: 'relatorios', sidebar: true },
  { key: 'i', label: 'I', description: 'Fiscal', category: 'Gestão', route: '/admin/fiscal', permission: 'fiscal', module: 'fiscal', sidebar: true },
  { key: 'm', label: 'M', description: 'Mensageria', category: 'Gestão', route: '/admin/mensageria', permission: 'anuncios', sidebar: true },
  { key: 't', label: 'T', description: 'Timer', category: 'Gestão', route: '/admin/timer', permission: 'timers', sidebar: true },
  { key: 's', label: 'S', description: 'Configurações', category: 'Gestão', route: '/admin/configuracoes', sidebar: true },
  { key: 'h', label: 'H', description: 'Manual completo', category: 'Ajuda', route: '/admin/manual' },
  { key: '1', label: '1', description: 'Primeiros passos', category: 'Ajuda', route: '/admin/primeiros-passos', sidebar: true },
]

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
