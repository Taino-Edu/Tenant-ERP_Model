'use client'
import { useEffect, useRef, useState } from 'react'
import { notificationsApi, pushApi, AppNotification } from '@/lib/api'
import { Bell, X, CheckCheck, ExternalLink } from 'lucide-react'
import Link from 'next/link'

const NAVY   = '#0C3D5A'
const BLUE   = '#28b0d6'
const MUTED  = '#4D8FAC'
const BORDER = 'rgba(12,61,90,0.12)'

function urlBase64ToUint8Array(b64: string) {
  const pad = '='.repeat((4 - b64.length % 4) % 4)
  const raw = atob((b64 + pad).replace(/-/g, '+').replace(/_/g, '/'))
  return Uint8Array.from([...raw].map(c => c.charCodeAt(0)))
}

export default function NotificationBell() {
  const [notifications, setNotifications] = useState<AppNotification[]>([])
  const [open,   setOpen]   = useState(false)
  const [unread, setUnread] = useState(0)
  const ref = useRef<HTMLDivElement>(null)

  // Polling unread count a cada 30s
  useEffect(() => {
    fetchCount()
    const id = setInterval(fetchCount, 30_000)
    return () => clearInterval(id)
  }, [])

  // Registra Service Worker e subscreve push na primeira vez
  useEffect(() => { registerPush() }, [])

  // Fecha dropdown ao clicar fora
  useEffect(() => {
    function handler(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  async function fetchCount() {
    try {
      const r = await notificationsApi.unreadCount()
      setUnread(r.data.count)
    } catch {}
  }

  async function openPanel() {
    setOpen(o => !o)
    if (!open) {
      try {
        const r = await notificationsApi.list()
        setNotifications(r.data)
      } catch {}
    }
  }

  async function markRead(id: string) {
    await notificationsApi.markRead(id)
    setNotifications(prev => prev.map(n => n.id === id ? { ...n, isRead: true } : n))
    setUnread(u => Math.max(0, u - 1))
  }

  async function markAllRead() {
    await notificationsApi.markAllRead()
    setNotifications(prev => prev.map(n => ({ ...n, isRead: true })))
    setUnread(0)
  }

  async function remove(id: string) {
    await notificationsApi.remove(id)
    const wasUnread = notifications.find(n => n.id === id)?.isRead === false
    setNotifications(prev => prev.filter(n => n.id !== id))
    if (wasUnread) setUnread(u => Math.max(0, u - 1))
  }

  async function registerPush() {
    if (typeof window === 'undefined') return
    if (!('serviceWorker' in navigator) || !('PushManager' in window)) return

    // A chave VAPID vem ANTES de pedir permissão, de propósito. Na ordem
    // inversa (permissão primeiro) o usuário concedia a permissão e só então
    // descobríamos que o servidor não tem push configurado — a chamada morria
    // num catch vazio e ele ficava com a permissão dada esperando notificação
    // que nunca chegaria. Sem chave, não incomoda o usuário e não faz nada.
    let publicKey: string
    try {
      const { data } = await pushApi.publicKey()
      if (!data?.publicKey) return
      publicKey = data.publicKey
    } catch {
      // 404 = servidor sem VAPID configurado (push desativado nesta instalação).
      return
    }

    try {
      const reg  = await navigator.serviceWorker.register('/sw.js', { scope: '/' })
      const perm = await Notification.requestPermission()
      if (perm !== 'granted') return
      const sub = await reg.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: urlBase64ToUint8Array(publicKey),
      })
      const json = sub.toJSON()
      await pushApi.subscribe({
        endpoint: sub.endpoint,
        p256dh:   json.keys?.p256dh   ?? '',
        auth:     json.keys?.auth     ?? '',
      })
    } catch (err) {
      // Permissão negada é o caso normal e não merece ruído; qualquer outra
      // falha (SW não registrou, subscribe rejeitado, POST falhou) some sem
      // rastro se não logar — foi assim que o push ficou quebrado sem ninguém
      // perceber.
      if (Notification.permission !== 'denied') console.warn('Push não pôde ser ativado:', err)
    }
  }

  function timeAgo(iso: string) {
    const diff = (Date.now() - new Date(iso).getTime()) / 1000
    if (diff < 60)    return 'agora'
    if (diff < 3600)  return `${Math.floor(diff / 60)}min`
    if (diff < 86400) return `${Math.floor(diff / 3600)}h`
    return `${Math.floor(diff / 86400)}d`
  }

  return (
    <div className="relative" ref={ref}>
      {/* Botão do sino */}
      <button
        onClick={openPanel}
        className="relative p-2 rounded-xl transition-colors"
        style={{ color: 'rgba(255,255,255,0.75)' }}
        title="Notificações"
      >
        <Bell className="w-5 h-5" />
        {unread > 0 && (
          <span className="absolute -top-0.5 -right-0.5 min-w-[18px] h-[18px] bg-red-500 text-white text-[10px] font-bold rounded-full flex items-center justify-center px-1">
            {unread > 9 ? '9+' : unread}
          </span>
        )}
      </button>

      {/* Dropdown — fundo branco sólido, funciona em qualquer contexto */}
      {open && (
        <div
          className="absolute right-0 mt-2 w-80 rounded-2xl z-50 overflow-hidden"
          style={{
            backgroundColor: '#FFFFFF',
            border: `1px solid ${BORDER}`,
            boxShadow: '0 20px 60px rgba(12,61,90,0.18)',
          }}
        >
          {/* Cabeçalho */}
          <div className="flex items-center justify-between px-4 py-3 border-b" style={{ borderColor: BORDER }}>
            <span className="font-black text-sm" style={{ color: NAVY }}>
              Notificações{unread > 0 && <span className="text-red-500 ml-1">({unread})</span>}
            </span>
            {unread > 0 && (
              <button
                onClick={markAllRead}
                className="flex items-center gap-1 text-xs font-semibold transition-colors"
                style={{ color: MUTED }}
              >
                <CheckCheck className="w-3.5 h-3.5" /> Todas lidas
              </button>
            )}
          </div>

          {/* Lista */}
          <div className="max-h-[360px] overflow-y-auto">
            {notifications.length === 0 ? (
              <div className="py-10 text-center">
                <Bell className="w-8 h-8 mx-auto mb-2 opacity-20" style={{ color: NAVY }} />
                <p className="text-sm font-bold" style={{ color: MUTED }}>Nenhuma notificação</p>
              </div>
            ) : (
              notifications.map(n => (
                <div
                  key={n.id}
                  className="flex gap-3 px-4 py-3 border-b transition-colors"
                  style={{
                    borderColor: BORDER,
                    backgroundColor: !n.isRead ? `rgba(40,176,214,0.06)` : 'transparent',
                  }}
                >
                  {/* Indicador não lida */}
                  <div className="mt-1.5 shrink-0">
                    <div
                      className="w-2 h-2 rounded-full"
                      style={{ backgroundColor: !n.isRead ? BLUE : 'transparent' }}
                    />
                  </div>

                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-black leading-snug" style={{ color: NAVY }}>{n.title}</p>
                    <p className="text-xs mt-0.5 line-clamp-2" style={{ color: MUTED }}>{n.body}</p>
                    {n.imageUrl && (
                      // eslint-disable-next-line @next/next/no-img-element
                      <img src={n.imageUrl} alt=""
                        className="mt-2 rounded-lg w-full max-h-32 object-cover"
                        onError={e => { e.currentTarget.style.display = 'none' }} />
                    )}
                    <div className="flex items-center gap-2 mt-1.5">
                      <span className="text-[10px]" style={{ color: MUTED }}>{timeAgo(n.createdAt)}</span>
                      {n.link && (
                        <Link
                          href={n.link}
                          onClick={() => { markRead(n.id); setOpen(false) }}
                          className="text-[10px] font-bold flex items-center gap-0.5"
                          style={{ color: BLUE }}
                        >
                          Ver <ExternalLink className="w-2.5 h-2.5" />
                        </Link>
                      )}
                      {!n.isRead && (
                        <button
                          onClick={() => markRead(n.id)}
                          className="text-[10px] font-semibold"
                          style={{ color: MUTED }}
                        >
                          Marcar lida
                        </button>
                      )}
                    </div>
                  </div>

                  <button
                    onClick={() => remove(n.id)}
                    className="shrink-0 p-1 rounded-lg mt-0.5 transition-colors"
                    style={{ color: MUTED }}
                  >
                    <X className="w-3 h-3" />
                  </button>
                </div>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  )
}
