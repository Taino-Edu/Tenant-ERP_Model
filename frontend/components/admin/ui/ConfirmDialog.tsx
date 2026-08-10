'use client'
import { ReactNode } from 'react'
import { AlertTriangle, LucideIcon } from 'lucide-react'
import Modal from './Modal'
import Button from './Button'

interface ConfirmDialogProps {
  title: string
  /** Corpo da pergunta. String simples na maioria dos casos; ReactNode quando
   * precisa destacar o nome do que vai ser afetado. */
  message: ReactNode
  /** Texto do botão que confirma. Deve dizer a AÇÃO ("Remover", "Cancelar
   * venda"), não "OK" — é o que o usuário lê quando está com pressa. */
  confirmLabel?: string
  /** "Voltar", não "Cancelar": em "Cancelar comanda" um botão escrito
   * "Cancelar" pergunta se você quer cancelar o cancelamento. */
  cancelLabel?: string
  /** `danger` para ação destrutiva (padrão), `primary` para confirmação neutra. */
  variant?: 'danger' | 'primary'
  icon?: LucideIcon
  loading?: boolean
  onConfirm: () => void
  onClose: () => void
}

/**
 * Confirmação no visual do sistema, no lugar de `window.confirm`.
 *
 * Promovido de `comanda/ConfirmModal` — ele já fazia isso certo, mas morava
 * numa pasta de feature e por isso as outras 5 telas nem sabiam que existia e
 * caíram no `window.confirm`. Componente de UI genérico mora em `ui/`.
 *
 * O `window.confirm` nativo era usado em 5 telas, inclusive na frente de caixa.
 * Ele abre a caixa do navegador — no Chrome em tema escuro, um retângulo preto
 * pequeno, colado no topo da janela, com a fonte e os botões do sistema
 * operacional. Não parece parte do sistema, não respeita o tema, não aceita
 * destaque no nome do item, e o botão diz "OK" em vez de dizer o que vai
 * acontecer.
 *
 * Pior que a aparência: ele TRAVA a thread. Enquanto está aberto, o SignalR não
 * processa evento nenhum — numa frente de caixa com várias comandas abertas,
 * a tela fica congelada até alguém responder.
 */
export default function ConfirmDialog({
  title, message, confirmLabel = 'Confirmar', cancelLabel = 'Voltar',
  variant = 'danger', icon = AlertTriangle, loading = false, onConfirm, onClose,
}: ConfirmDialogProps) {
  return (
    <Modal onClose={onClose} maxWidth="sm" title={title} icon={icon} closeOnBackdrop={false}>
      <div className="p-4 flex flex-col gap-5">
        <div className="text-sm leading-relaxed" style={{ color: 'var(--text-muted)' }}>
          {message}
        </div>
        <div className="flex gap-2 justify-end">
          <Button variant="secondary" size="sm" onClick={onClose} disabled={loading}>{cancelLabel}</Button>
          <Button variant={variant} size="sm" onClick={onConfirm} loading={loading}>{confirmLabel}</Button>
        </div>
      </div>
    </Modal>
  )
}
