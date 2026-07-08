'use client'

import { useEffect, useState } from 'react'
import { siteConfigApi, SiteConfigDto } from '@/lib/api'
import toast, { Toaster } from 'react-hot-toast'
import { Palette, Save, Loader2, ExternalLink } from 'lucide-react'

const DEFAULTS: SiteConfigDto = {
  siteName: 'Santuário Nerd',
  heroSubtitle: 'Produtos, torneios e a melhor experiência TCG da região. Acumule pontos, compre na mesa e participe de campeonatos.',
  addressLine: 'José Bonifácio — SP',
  contactPersonName: 'Maikon',
  whatsappNumber: '5517997633103',
  contactEmail: 'santuarionerd@gmail.com',
  navTorneiosLabel: 'Torneios',
  navProdutosLabel: 'Produtos',
  navMercadoLabel: 'Mercado de Cartas',
  navPontosLabel: 'Pontos',
  ctaVerEventosLabel: 'Ver Eventos',
  ctaVerTorneiosLabel: 'Ver Torneios',
  ctaVerProdutosLabel: 'Ver Produtos',
  torneiosEyebrow: 'Agenda',
  torneiosTitle: 'Próximos Torneios',
  produtosEyebrow: 'Vitrine',
  produtosTitle: 'Em Destaque',
  pontosEyebrow: 'Programa de Fidelidade',
  pontosTitle: 'Ganhe pontos a cada visita',
  pontosParagraph: 'Acumule pontos nas suas compras e troque por descontos. Só com CPF e WhatsApp — nada de senha ou aplicativo.',
  colorPrimary: '#3EC2F2',
  colorAccent: '#FFE45E',
  colorNavy: '#0C3D5A',
}

function Field({ label, desc, children }: { label: string; desc?: string; children: React.ReactNode }) {
  return (
    <div>
      <label className="text-xs text-gray-400 font-semibold mb-1 block">{label}</label>
      {children}
      {desc && <p className="text-[11px] text-gray-500 mt-1">{desc}</p>}
    </div>
  )
}

function ColorField({ label, value, onChange }: { label: string; value: string; onChange: (v: string) => void }) {
  return (
    <div>
      <label className="text-xs text-gray-400 font-semibold mb-1 block">{label}</label>
      <div className="flex items-center gap-2">
        <input
          type="color"
          value={/^#[0-9a-fA-F]{6}$/.test(value) ? value : '#000000'}
          onChange={e => onChange(e.target.value)}
          className="w-10 h-10 rounded-lg border border-surface-500 bg-transparent cursor-pointer shrink-0"
        />
        <input
          value={value}
          onChange={e => onChange(e.target.value)}
          placeholder="#000000"
          className="input w-full font-mono text-sm"
        />
      </div>
    </div>
  )
}

export default function SiteConfigPage() {
  const [cfg, setCfg]         = useState<SiteConfigDto>(DEFAULTS)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving]   = useState(false)

  useEffect(() => {
    siteConfigApi.get()
      .then(r => setCfg(r.data))
      .catch(() => toast.error('Erro ao carregar personalização do site'))
      .finally(() => setLoading(false))
  }, [])

  function set<K extends keyof SiteConfigDto>(key: K, value: SiteConfigDto[K]) {
    setCfg(prev => ({ ...prev, [key]: value }))
  }

  async function save() {
    setSaving(true)
    try {
      const { data } = await siteConfigApi.save(cfg)
      setCfg(data)
      toast.success('Personalização do site salva!')
    } catch {
      toast.error('Erro ao salvar')
    } finally {
      setSaving(false)
    }
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="w-6 h-6 animate-spin text-brand-400" />
      </div>
    )
  }

  return (
    <div className="p-4 sm:p-6 space-y-6 max-w-3xl">
      <Toaster position="top-center" />

      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h1 className="text-2xl font-bold text-white flex items-center gap-2">
            <Palette className="w-6 h-6 text-brand-400" /> Personalizar Site
          </h1>
          <p className="text-gray-500 text-sm mt-0.5">Nome, textos e cores da página pública — mudanças aparecem pra todo mundo</p>
        </div>
        <a href="/" target="_blank" rel="noreferrer"
          className="flex items-center gap-1.5 text-xs font-medium text-brand-400 hover:text-brand-300 transition-colors">
          Ver site <ExternalLink className="w-3.5 h-3.5" />
        </a>
      </div>

      {/* Identidade */}
      <div className="card p-5 space-y-3">
        <h3 className="font-bold text-white mb-1">Identidade</h3>
        <Field label="Nome do site" desc="Aparece na navbar, rodapé e título principal">
          <input value={cfg.siteName} onChange={e => set('siteName', e.target.value)} className="input w-full" />
        </Field>
        <Field label="Frase de apresentação (abaixo do título principal)">
          <textarea value={cfg.heroSubtitle} onChange={e => set('heroSubtitle', e.target.value)}
            rows={2} className="input w-full resize-none" />
        </Field>
        <Field label="Endereço / cidade" desc="Aparece no rodapé">
          <input value={cfg.addressLine} onChange={e => set('addressLine', e.target.value)} className="input w-full" />
        </Field>
        <Field label="Nome de quem atende" desc={'Usado em textos como "Falar com [nome]" e "[nome] vai confirmar sua vaga..."'}>
          <input value={cfg.contactPersonName} onChange={e => set('contactPersonName', e.target.value)} className="input w-full" />
        </Field>
      </div>

      {/* Contato */}
      <div className="card p-5 space-y-3">
        <h3 className="font-bold text-white mb-1">Contato</h3>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <Field label="WhatsApp" desc="Só números, com DDI (ex: 5517999999999)">
            <input value={cfg.whatsappNumber} onChange={e => set('whatsappNumber', e.target.value)} className="input w-full font-mono" />
          </Field>
          <Field label="E-mail de contato">
            <input value={cfg.contactEmail} onChange={e => set('contactEmail', e.target.value)} className="input w-full" />
          </Field>
        </div>
      </div>

      {/* Cores */}
      <div className="card p-5 space-y-3">
        <h3 className="font-bold text-white mb-1">Cores</h3>
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
          <ColorField label="Cor primária (azul)" value={cfg.colorPrimary} onChange={v => set('colorPrimary', v)} />
          <ColorField label="Cor de destaque (amarelo)" value={cfg.colorAccent} onChange={v => set('colorAccent', v)} />
          <ColorField label="Cor da navbar" value={cfg.colorNavy} onChange={v => set('colorNavy', v)} />
        </div>
      </div>

      {/* Textos da navbar */}
      <div className="card p-5 space-y-3">
        <h3 className="font-bold text-white mb-1">Textos da navbar</h3>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <Field label="Link 'Torneios'">
            <input value={cfg.navTorneiosLabel} onChange={e => set('navTorneiosLabel', e.target.value)} className="input w-full" />
          </Field>
          <Field label="Link 'Produtos'">
            <input value={cfg.navProdutosLabel} onChange={e => set('navProdutosLabel', e.target.value)} className="input w-full" />
          </Field>
          <Field label="Link 'Mercado de Cartas'">
            <input value={cfg.navMercadoLabel} onChange={e => set('navMercadoLabel', e.target.value)} className="input w-full" />
          </Field>
          <Field label="Link 'Pontos'">
            <input value={cfg.navPontosLabel} onChange={e => set('navPontosLabel', e.target.value)} className="input w-full" />
          </Field>
          <Field label="Botão 'Ver Eventos' (navbar)">
            <input value={cfg.ctaVerEventosLabel} onChange={e => set('ctaVerEventosLabel', e.target.value)} className="input w-full" />
          </Field>
          <Field label="Botão 'Ver Torneios' (topo)">
            <input value={cfg.ctaVerTorneiosLabel} onChange={e => set('ctaVerTorneiosLabel', e.target.value)} className="input w-full" />
          </Field>
          <Field label="Botão 'Ver Produtos' (topo)">
            <input value={cfg.ctaVerProdutosLabel} onChange={e => set('ctaVerProdutosLabel', e.target.value)} className="input w-full" />
          </Field>
        </div>
      </div>

      {/* Textos das seções */}
      <div className="card p-5 space-y-4">
        <h3 className="font-bold text-white mb-1">Textos das seções</h3>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <Field label="Torneios — etiqueta">
            <input value={cfg.torneiosEyebrow} onChange={e => set('torneiosEyebrow', e.target.value)} className="input w-full" />
          </Field>
          <Field label="Torneios — título">
            <input value={cfg.torneiosTitle} onChange={e => set('torneiosTitle', e.target.value)} className="input w-full" />
          </Field>
          <Field label="Produtos — etiqueta">
            <input value={cfg.produtosEyebrow} onChange={e => set('produtosEyebrow', e.target.value)} className="input w-full" />
          </Field>
          <Field label="Produtos — título">
            <input value={cfg.produtosTitle} onChange={e => set('produtosTitle', e.target.value)} className="input w-full" />
          </Field>
          <Field label="Pontos — etiqueta">
            <input value={cfg.pontosEyebrow} onChange={e => set('pontosEyebrow', e.target.value)} className="input w-full" />
          </Field>
          <Field label="Pontos — título">
            <input value={cfg.pontosTitle} onChange={e => set('pontosTitle', e.target.value)} className="input w-full" />
          </Field>
        </div>
        <Field label="Pontos — parágrafo">
          <textarea value={cfg.pontosParagraph} onChange={e => set('pontosParagraph', e.target.value)}
            rows={2} className="input w-full resize-none" />
        </Field>
      </div>

      <button onClick={save} disabled={saving} className="btn-primary">
        {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
        Salvar
      </button>
    </div>
  )
}
