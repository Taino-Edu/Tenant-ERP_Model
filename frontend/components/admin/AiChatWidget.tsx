'use client'

// =============================================================================
// AiChatWidget.tsx — Assistente IA flutuante no painel admin
//
// Botão fixo no canto inferior direito. Clique para abrir/fechar o chat.
// Envia perguntas para POST /api/ai/chat e exibe respostas em texto.
// Fecha ao pressionar ESC.
// =============================================================================

import { useState, useRef, useEffect, KeyboardEvent } from 'react'
import { BrainCircuit, X, Send, Loader2, ChevronDown } from 'lucide-react'
import { aiApi } from '@/lib/api'

interface Message {
  role: 'user' | 'assistant'
  text: string
}

const SUGGESTIONS = [
  'Quanto vendi hoje?',
  'Quem está devendo no crediário?',
  'Quais produtos estão com estoque baixo?',
  'Quais são os produtos mais vendidos?',
  'Quantas comandas estão abertas agora?',
]

export default function AiChatWidget() {
  const [open,     setOpen]     = useState(false)
  const [messages, setMessages] = useState<Message[]>([])
  const [input,    setInput]    = useState('')
  const [loading,  setLoading]  = useState(false)

  const bottomRef  = useRef<HTMLDivElement>(null)
  const inputRef   = useRef<HTMLTextAreaElement>(null)

  // Fechar com ESC
  useEffect(() => {
    const onKey = (e: globalThis.KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false)
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  // Scroll para o fim ao chegar nova mensagem
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages, loading])

  // Focar input ao abrir
  useEffect(() => {
    if (open) setTimeout(() => inputRef.current?.focus(), 100)
  }, [open])

  async function send(text?: string) {
    const msg = (text ?? input).trim()
    if (!msg || loading) return

    setInput('')
    setMessages(prev => [...prev, { role: 'user', text: msg }])
    setLoading(true)

    try {
      const { data } = await aiApi.chat(msg)
      setMessages(prev => [...prev, {
        role: 'assistant',
        text: data.success ? data.reply : (data.reply || 'Erro ao obter resposta.'),
      }])
    } catch {
      setMessages(prev => [...prev, {
        role: 'assistant',
        text: 'Não consegui conectar ao assistente. Tente novamente.',
      }])
    } finally {
      setLoading(false)
    }
  }

  function onKeyDown(e: KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      send()
    }
  }

  return (
    <>
      {/* ── Painel de chat ────────────────────────────────────────────────── */}
      {open && (
        <div
          className="fixed bottom-24 right-4 z-50 flex flex-col w-80 sm:w-96 rounded-2xl shadow-2xl overflow-hidden"
          style={{ background: '#16161C', border: '1px solid #2D2D36', maxHeight: '70vh' }}
        >
          {/* Cabeçalho */}
          <div className="flex items-center justify-between px-4 py-3"
               style={{ background: '#1A1A24', borderBottom: '1px solid #2D2D36' }}>
            <div className="flex items-center gap-2">
              <BrainCircuit size={18} className="text-violet-400" />
              <span className="text-sm font-semibold text-white">Assistente Santuário Nerd</span>
            </div>
            <button
              onClick={() => setOpen(false)}
              className="text-gray-400 hover:text-white transition-colors"
              aria-label="Fechar chat"
            >
              <X size={16} />
            </button>
          </div>

          {/* Mensagens */}
          <div className="flex-1 overflow-y-auto px-4 py-3 space-y-3" style={{ minHeight: 0 }}>
            {messages.length === 0 && (
              <div className="space-y-3">
                <p className="text-xs text-gray-400 text-center pt-2">
                  Pergunte sobre vendas, estoque, crediários ou clientes
                </p>
                <div className="space-y-2">
                  {SUGGESTIONS.map(s => (
                    <button
                      key={s}
                      onClick={() => send(s)}
                      className="w-full text-left text-xs px-3 py-2 rounded-lg text-gray-300 hover:text-white transition-colors"
                      style={{ background: '#1E1E28', border: '1px solid #2D2D36' }}
                    >
                      {s}
                    </button>
                  ))}
                </div>
              </div>
            )}

            {messages.map((m, i) => (
              <div
                key={i}
                className={`flex ${m.role === 'user' ? 'justify-end' : 'justify-start'}`}
              >
                <div
                  className="max-w-[85%] px-3 py-2 rounded-xl text-sm leading-relaxed"
                  style={
                    m.role === 'user'
                      ? { background: '#6D28D9', color: '#fff', borderRadius: '12px 12px 2px 12px' }
                      : { background: '#1E1E28', color: '#E5E7EB', border: '1px solid #2D2D36', borderRadius: '12px 12px 12px 2px' }
                  }
                >
                  {m.text}
                </div>
              </div>
            ))}

            {loading && (
              <div className="flex justify-start">
                <div className="px-3 py-2 rounded-xl text-sm"
                     style={{ background: '#1E1E28', border: '1px solid #2D2D36', borderRadius: '12px 12px 12px 2px' }}>
                  <Loader2 size={14} className="animate-spin text-violet-400" />
                </div>
              </div>
            )}

            <div ref={bottomRef} />
          </div>

          {/* Input */}
          <div className="px-3 py-3" style={{ borderTop: '1px solid #2D2D36' }}>
            <div className="flex items-end gap-2">
              <textarea
                ref={inputRef}
                value={input}
                onChange={e => setInput(e.target.value)}
                onKeyDown={onKeyDown}
                placeholder="Pergunte algo sobre a loja..."
                rows={1}
                disabled={loading}
                className="flex-1 resize-none text-sm text-white placeholder-gray-500 rounded-xl px-3 py-2 outline-none disabled:opacity-50"
                style={{
                  background: '#1E1E28',
                  border: '1px solid #2D2D36',
                  maxHeight: '80px',
                  lineHeight: '1.5',
                }}
              />
              <button
                onClick={() => send()}
                disabled={!input.trim() || loading}
                className="flex-shrink-0 p-2 rounded-xl transition-colors disabled:opacity-40"
                style={{ background: '#6D28D9' }}
                aria-label="Enviar"
              >
                {loading
                  ? <Loader2 size={16} className="animate-spin text-white" />
                  : <Send size={16} className="text-white" />
                }
              </button>
            </div>
            <p className="text-xs text-gray-600 mt-1 text-center">
              Enter para enviar · Shift+Enter nova linha
            </p>
          </div>
        </div>
      )}

      {/* ── Botão flutuante ───────────────────────────────────────────────── */}
      <button
        onClick={() => setOpen(o => !o)}
        className="fixed bottom-5 right-4 z-50 flex items-center justify-center w-13 h-13 rounded-full shadow-lg transition-all hover:scale-105 active:scale-95"
        style={{
          background: open ? '#4C1D95' : 'linear-gradient(135deg, #6D28D9, #7C3AED)',
          width: '52px',
          height: '52px',
          boxShadow: '0 4px 20px rgba(109, 40, 217, 0.5)',
        }}
        aria-label={open ? 'Fechar assistente IA' : 'Abrir assistente IA'}
        title="Assistente IA"
      >
        {open
          ? <ChevronDown size={22} className="text-white" />
          : <BrainCircuit size={22} className="text-white" />
        }
      </button>
    </>
  )
}
