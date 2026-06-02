'use client'
import { useState, useEffect, useRef } from 'react'
import QRCode from 'qrcode'
import toast from 'react-hot-toast'
import { QrCode, Download, Printer, Plus, Trash2, Table2 } from 'lucide-react'

interface Mesa { id: string; nome: string; url: string; qrDataUrl: string }

const DEFAULT_MESAS = Array.from({ length: 10 }, (_, i) => `Mesa-${String(i + 1).padStart(2, '0')}`)

function useBaseUrl() {
  const [base, setBase] = useState('')
  useEffect(() => { setBase(window.location.origin) }, [])
  return base
}

export default function QRCodesPage() {
  const baseUrl  = useBaseUrl()
  const [mesas, setMesas]     = useState<Mesa[]>([])
  const [newNome, setNewNome] = useState('')
  const [loading, setLoading] = useState(true)
  const printRef = useRef<HTMLDivElement>(null)

  // Gera os QR codes ao montar ou quando baseUrl muda
  useEffect(() => {
    if (!baseUrl) return
    generateAll(DEFAULT_MESAS)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [baseUrl])

  async function generateAll(nomes: string[]) {
    setLoading(true)
    const generated = await Promise.all(nomes.map(async nome => {
      const url       = `${baseUrl}/mesa/${encodeURIComponent(nome)}`
      const qrDataUrl = await QRCode.toDataURL(url, {
        width: 400, margin: 2,
        color: { dark: '#ffffff', light: '#1A1A1F' },
        errorCorrectionLevel: 'H',
      })
      return { id: nome, nome, url, qrDataUrl }
    }))
    setMesas(generated)
    setLoading(false)
  }

  async function addMesa() {
    const nome = newNome.trim()
    if (!nome) return
    if (mesas.find(m => m.nome === nome)) { toast.error('Mesa já existe!'); return }
    const url       = `${baseUrl}/mesa/${encodeURIComponent(nome)}`
    const qrDataUrl = await QRCode.toDataURL(url, {
      width: 400, margin: 2,
      color: { dark: '#ffffff', light: '#1A1A1F' },
      errorCorrectionLevel: 'H',
    })
    setMesas(prev => [...prev, { id: nome, nome, url, qrDataUrl }])
    setNewNome('')
    toast.success(`QR Code criado para ${nome}!`)
  }

  function removeMesa(id: string) { setMesas(prev => prev.filter(m => m.id !== id)) }

  function downloadSingle(mesa: Mesa) {
    const a = document.createElement('a')
    a.href     = mesa.qrDataUrl
    a.download = `qrcode-${mesa.nome.toLowerCase().replace(/\s/g, '-')}.png`
    a.click()
  }

  function downloadAll() {
    mesas.forEach(m => downloadSingle(m))
    toast.success(`${mesas.length} QR Codes baixados!`)
  }

  function printAll() {
    if (!printRef.current) return
    const w = window.open('', '_blank', 'width=900,height=700')
    if (!w) return
    w.document.write(`
      <html><head><title>QR Codes — CardGameStore</title>
      <style>
        body { background: #1a1a1a; color: #fff; font-family: Inter, sans-serif; padding: 16px; }
        .grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; }
        .card { background: #1A1A1F; border: 1px solid #2D2D36; border-radius: 12px; padding: 16px; text-align: center; }
        .card img { width: 140px; height: 140px; margin: 0 auto 8px; display: block; }
        .name { font-size: 14px; font-weight: 700; color: #fff; }
        .url  { font-size: 9px; color: #666; margin-top: 4px; word-break: break-all; }
        @media print { body { background: #fff; color: #000; } .card { background: #fff; border: 1px solid #ddd; } .url { color: #999; } }
      </style></head><body>
      <h2 style="text-align:center;margin-bottom:20px;font-size:18px;">🃏 CardGameStore — QR Codes das Mesas</h2>
      <div class="grid">
        ${mesas.map(m => `
          <div class="card">
            <img src="${m.qrDataUrl}" alt="${m.nome}" />
            <div class="name">${m.nome}</div>
            <div class="url">${m.url}</div>
          </div>
        `).join('')}
      </div>
      </body></html>
    `)
    w.document.close()
    w.focus()
    setTimeout(() => w.print(), 500)
  }

  return (
    <div className="p-4 sm:p-6 space-y-4 sm:space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-white">QR Codes das Mesas</h1>
          <p className="text-gray-400 text-sm mt-0.5">Imprima e cole em cada mesa — cliente escaneia e entra direto</p>
        </div>
        <div className="flex gap-2">
          <button onClick={printAll} className="btn-secondary">
            <Printer className="w-4 h-4" /> Imprimir Todos
          </button>
          <button onClick={downloadAll} className="btn-primary">
            <Download className="w-4 h-4" /> Baixar Todos
          </button>
        </div>
      </div>

      {/* Adicionar mesa */}
      <div className="card flex gap-3 items-end">
        <div className="flex-1">
          <label className="label">Adicionar nova mesa</label>
          <input
            className="input" placeholder="Ex: Mesa-VIP, Mesa-Torneio, Balcão..."
            value={newNome}
            onChange={e => setNewNome(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && addMesa()}
          />
        </div>
        <button onClick={addMesa} className="btn-primary shrink-0">
          <Plus className="w-4 h-4" /> Adicionar
        </button>
      </div>

      {/* Como funciona */}
      <div className="bg-brand-600/10 border border-brand-500/20 rounded-xl p-4 text-sm">
        <p className="font-semibold text-brand-300 mb-2 flex items-center gap-2">
          <QrCode className="w-4 h-4" /> Como funciona o fluxo QR Code
        </p>
        <ol className="space-y-1 text-gray-300 list-decimal list-inside">
          <li>Imprima ou baixe os QR Codes e cole fisicamente em cada mesa</li>
          <li>Cliente aponta a câmera do celular → abre o link automaticamente</li>
          <li>Cliente informa Nome, CPF e WhatsApp (apenas na primeira visita)</li>
          <li>Comanda é aberta automaticamente e vinculada à mesa</li>
          <li>Qualquer item adicionado aparece no seu dashboard em tempo real ↗</li>
        </ol>
      </div>

      {/* Grid de QR Codes */}
      {loading ? (
        <div className="flex justify-center py-16">
          <div className="flex flex-col items-center gap-3">
            <div className="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
            <p className="text-gray-400 text-sm">Gerando QR Codes...</p>
          </div>
        </div>
      ) : (
        <div ref={printRef} className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-4">
          {mesas.map(mesa => (
            <div key={mesa.id} className="card group flex flex-col items-center text-center p-4 hover:border-brand-500/50 transition-all">
              {/* QR Code */}
              <div className="relative rounded-xl overflow-hidden bg-surface-800 p-2 mb-3">
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img src={mesa.qrDataUrl} alt={mesa.nome} className="w-32 h-32 sm:w-36 sm:h-36" />
              </div>

              <div className="flex items-center gap-1.5 mb-1">
                <Table2 className="w-4 h-4 text-brand-400" />
                <p className="font-bold text-white text-sm">{mesa.nome}</p>
              </div>
              <p className="text-[10px] text-gray-400 break-all leading-tight mb-3">{mesa.url}</p>

              {/* Ações */}
              <div className="flex gap-2 w-full opacity-0 group-hover:opacity-100 transition-opacity">
                <button onClick={() => downloadSingle(mesa)} className="btn-secondary flex-1 justify-center py-1 text-xs">
                  <Download className="w-3.5 h-3.5" /> PNG
                </button>
                <button onClick={() => removeMesa(mesa.id)} className="p-1.5 rounded-lg bg-red-600/10 hover:bg-red-600/20 text-red-400 transition-colors">
                  <Trash2 className="w-3.5 h-3.5" />
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
