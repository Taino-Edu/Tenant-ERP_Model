'use client'
import { useEffect, useRef, useState } from 'react'
import { notificationsApi, AppNotification } from '@/lib/api'
import { Bell, X, CheckCheck, ExternalLink } from 'lucide-react'
import Link from 'next/link'

export default function NotificationBell() {
  const [notifications, setNotifications] = useState<AppNotification[]>([])
  const [open, setOpen]     = useState(false)
  const [unread, setUnread] = useState(0)
  const ref = useRef<HTMLDivElement>(null)

  // Polling a cada 30s
  useEffect(() => {
    fetchCount()
    const id = setInterval(fetchCount, 30_000)
    return () => clearInterval(id)
  }, [])

  // Fecha ao clicar fora
  useEffect(() => {
    function onClick(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onClick)
    return () => document.removeEventListener('mousedown', onClick)
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
    setNotifications(prev => prev.map(n => n.id === id ? { ...n, isRead: true, readAt: new Date().toISOString() } : n))
    setUnread(u => Math.max(0, u - 1))
  }

  async function markAllRead() {
    await notificationsApi.markAllRead()
    setNotifications(prev => prev.map(n => ({ ...n, isRead: true, readAt: new Date().toISOString() })))
    setUnread(0)
  }

  async function remove(id: string) {
    await notificationsApi.remove(id)
    const wasUnread = notifications.find(n => n.id === id)?.isRead === false
    setNotifications(prev => prev.filter(n => n.id !== id))
    if (wasUnread) setUnread(u => Math.max(0, u - 1))
  }

  function timeAgo(iso: string) {
    const diff = (Date.now() - new Date(iso).getTime()) / 1000
    if (diff < 60)   return 'agora'
    if (diff < 3600) return `${Math.floor(diff / 60)}min`
    if (diff < 86400) return `${Math.floor(diff / 3600)}h`
    return `${Math.floor(diff / 86400)}d`
  }

  return (
    <div className="relative" ref={ref}>
      {/* Bell button */}
      <button
        onClick={openPanel}
        className="relative p-2 rounded-xl hover:bg-white/10 transition-colors"
        title="Notificações"
      >
        <Bell className="w-5 h-5 text-gray-400" />
        {unread > 0 && (
          <span className="absolute -top-0.5 -right-0.5 min-w-[18px] h-[18px] bg-red-500 text-white text-[10px] font-bold rounded-full flex items-center justify-center px-1">
            {unread > 9 ? '9+' : unread}
          </span>
        )}
      </button>

      {/* Dropdown */}
      {open && (
        <div className="absolute right-0 mt-2 w-80 rounded-2xl border shadow-2xl z-50 overflow-hidden"
          style={{ backgroundColor: 'var(--surface)', borderColor: 'var(--border)' }}>

          {/* Header */}
          <div className="flex items-center justify-between px-4 py-3 border-b"
            style={{ borderColor: 'var(--border)' }}>
            <span className="font-semibold text-sm text-[var(--text-primary)]">
              Notificações {unread > 0 && <span className="text-red-400">({unread})</span>}
            </span>
            {unread > 0 && (
              <button onClick={markAllRead}
                className="flex items-center gap-1 text-xs text-[var(--text-muted)] hover:text-violet-400 transition-colors">
                <CheckCheck className="w-3.5 h-3.5" /> Marcar todas como lidas
              </button>
            )}
          </div>

          {/* Lista */}
          <div className="max-h-80 overflow-y-auto divide-y divide-[var(--border)]">
            {notifications.length === 0 ? (
              <div className="py-8 text-center text-sm text-[var(--text-muted)]">
                Nenhuma notificação
              </div>
            ) : (
              notifications.map(n => (
                <div key={n.id}
                  className={`flex gap-3 px-4 py-3 transition-colors ${!n.isRead ? 'bg-violet-500/5' : ''}`}>
                  {/* Dot de não lida */}
                  <div className="mt-1.5 flex-shrink-0">
                    {!n.isRead
                      ? <div className="w-2 h-2 rounded-full bg-violet-500" />
                      : <div className="w-2 h-2 rounded-full bg-transparent" />
                    }
                  </div>

                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium text-[var(--text-primary)] leading-snug">
                      {n.title}
                    </p>
                    <p className="text-xs text-[var(--text-muted)] mt-0.5 line-clamp-2">{n.body}</p>
                    <div className="flex items-center gap-2 mt-1.5">
                      <span className="text-[10px] text-[var(--text-muted)]">{timeAgo(n.createdAt)}</span>
                      {n.link && (
                        <Link href={n.link} onClick={() => { markRead(n.id); setOpen(false) }}
                          className="text-[10px] text-violet-400 hover:underline flex items-center gap-0.5">
                          Ver <ExternalLink className="w-2.5 h-2.5" />
                        </Link>
                      )}
                      {!n.isRead && (
                        <button onClick={() => markRead(n.id)}
                          className="text-[10px] text-[var(--text-muted)] hover:text-violet-400 transition-colors">
                          Marcar lida
                        </button>
                      )}
                    </div>
                  </div>

                  <button onClick={() => remove(n.id)}
                    className="flex-shrink-0 p-1 rounded hover:bg-white/10 text-[var(--text-muted)] hover:text-red-400 transition-colors mt-0.5">
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
