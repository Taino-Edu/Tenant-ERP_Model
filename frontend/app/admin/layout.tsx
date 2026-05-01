import Sidebar from '@/components/admin/Sidebar'
import { Toaster } from 'react-hot-toast'
import type { Metadata } from 'next'

export const metadata: Metadata = { title: { default: 'Admin', template: '%s — CardGameStore Admin' } }

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen">
      <Sidebar />
      <main className="flex-1 overflow-auto bg-surface-900">
        <Toaster
          position="top-right"
          toastOptions={{
            style: { background: '#1e1e28', color: '#fff', border: '1px solid #32323f', fontSize: '14px' },
            success: { iconTheme: { primary: '#10b981', secondary: '#fff' } },
            error:   { iconTheme: { primary: '#ef4444', secondary: '#fff' } },
          }}
        />
        {children}
      </main>
    </div>
  )
}
