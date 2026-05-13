import Sidebar from '@/components/admin/Sidebar'
import AiChatWidget from '@/components/admin/AiChatWidget'
import { Toaster } from 'react-hot-toast'
import type { Metadata } from 'next'

export const metadata: Metadata = { title: { default: 'Admin', template: '%s — CardGameStore Admin' } }

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen bg-surface-900">
      <Sidebar />
      <main className="flex-1 overflow-auto pt-14 md:pt-0"
            style={{ background: 'radial-gradient(circle at top right, #1A1A24, #121215)' }}>
        <Toaster
          position="top-right"
          toastOptions={{
            style: { background: '#1A1A1F', color: '#fff', border: '1px solid #2D2D36', fontSize: '14px', borderRadius: '12px' },
            success: { iconTheme: { primary: '#00F0A8', secondary: '#000' } },
            error:   { iconTheme: { primary: '#FF3B30', secondary: '#fff' } },
          }}
        />
        {children}
      </main>
      <AiChatWidget />
    </div>
  )
}
