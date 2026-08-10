'use client'

import { useCallback, useEffect, useState } from 'react'
import {
  RestaurantProductionAreaDto,
  SaveRestaurantProductionAreaRequest,
  getErrorMessage,
  restaurantApi,
} from '@/lib/api'
import { useSiteConfig } from '@/contexts/SiteConfigContext'
import PageHeader from '@/components/admin/PageHeader'
import Modal from '@/components/admin/ui/Modal'
import ConfirmDialog from '@/components/admin/ui/ConfirmDialog'
import NumberInput from '@/components/admin/ui/NumberInput'
import toast from 'react-hot-toast'
import { ChefHat, Loader2, Pencil, Plus, Power, UtensilsCrossed } from 'lucide-react'

const DEFAULT_COLOR = '#3EC2F2'

function AreaModal({ area, onClose, onSaved }: {
  area: RestaurantProductionAreaDto | null
  onClose: () => void
  onSaved: () => void
}) {
  const [name, setName] = useState(area?.name ?? '')
  const [description, setDescription] = useState(area?.description ?? '')
  const [color, setColor] = useState(area?.color ?? DEFAULT_COLOR)
  const [displayOrder, setDisplayOrder] = useState(area?.displayOrder ?? 0)
  const [saving, setSaving] = useState(false)

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    const body: SaveRestaurantProductionAreaRequest = {
      name: name.trim(),
      description: description.trim() || null,
      color,
      displayOrder,
    }

    setSaving(true)
    try {
      if (area) await restaurantApi.updateProductionArea(area.id, body)
      else await restaurantApi.createProductionArea(body)
      toast.success(area ? 'Área de produção atualizada.' : 'Área de produção criada.')
      onSaved()
      onClose()
    } catch (error) {
      toast.error(getErrorMessage(error, 'Não foi possível salvar a área.'))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal onClose={onClose} closeOnBackdrop={false} title={area ? 'Editar área' : 'Nova área de produção'} icon={ChefHat}>
      <form onSubmit={submit} className="px-6 py-4 space-y-4">
        <div>
          <label className="label">Nome *</label>
          <input className="input" required maxLength={80} value={name} onChange={e => setName(e.target.value)} placeholder="Ex.: Cozinha, Bar, Confeitaria" />
        </div>
        <div>
          <label className="label">Descrição</label>
          <textarea className="input min-h-20 resize-y" maxLength={300} value={description} onChange={e => setDescription(e.target.value)} placeholder="O que este setor prepara" />
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="label">Cor de identificação</label>
            <div className="flex gap-2">
              <input type="color" className="h-11 w-14 rounded-lg bg-surface-700 border border-surface-500 p-1" value={color} onChange={e => setColor(e.target.value.toUpperCase())} />
              <input className="input font-mono" pattern="#[0-9A-Fa-f]{6}" value={color} onChange={e => setColor(e.target.value.toUpperCase())} />
            </div>
          </div>
          <div>
            <label className="label">Ordem</label>
            <NumberInput min={0} max={1000} value={displayOrder} fallback={0} onChange={v => setDisplayOrder(v ?? 0)} />
          </div>
        </div>
        <div className="flex gap-3 pt-2">
          <button type="button" className="btn-secondary flex-1 justify-center" onClick={onClose}>Cancelar</button>
          <button type="submit" className="btn-primary flex-1 justify-center" disabled={saving || !name.trim()}>
            {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : null}
            Salvar
          </button>
        </div>
      </form>
    </Modal>
  )
}

export default function RestaurantePage() {
  const { site } = useSiteConfig()
  const enabled = site.enabledModules.includes('restaurante')
  const [areas, setAreas] = useState<RestaurantProductionAreaDto[]>([])
  const [loading, setLoading] = useState(true)
  const [editing, setEditing] = useState<RestaurantProductionAreaDto | null | undefined>(undefined)
  const [confirmarDesativar, setConfirmarDesativar] = useState<RestaurantProductionAreaDto | null>(null)
  const [desativando, setDesativando] = useState(false)

  const load = useCallback(async () => {
    if (!enabled) {
      setLoading(false)
      return
    }
    try {
      const { data } = await restaurantApi.listProductionAreas(true)
      setAreas(data)
    } catch (error) {
      toast.error(getErrorMessage(error, 'Não foi possível carregar o módulo Restaurante.'))
    } finally {
      setLoading(false)
    }
  }, [enabled])

  useEffect(() => { load() }, [load])

  async function deactivate(area: RestaurantProductionAreaDto) {
    setDesativando(true)
    try {
      await restaurantApi.deactivateProductionArea(area.id)
      toast.success('Área desativada sem apagar o histórico.')
      setConfirmarDesativar(null)
      await load()
    } catch (error) {
      toast.error(getErrorMessage(error, 'Não foi possível desativar a área.'))
    } finally {
      setDesativando(false)
    }
  }

  async function reactivate(area: RestaurantProductionAreaDto) {
    try {
      await restaurantApi.reactivateProductionArea(area.id)
      toast.success('Área reativada.')
      await load()
    } catch (error) {
      toast.error(getErrorMessage(error, 'Não foi possível reativar a área.'))
    }
  }

  if (!enabled) {
    return (
      <div className="p-6 md:p-8 max-w-5xl mx-auto">
        <div className="card p-8 text-center">
          <UtensilsCrossed className="w-10 h-10 text-gray-500 mx-auto mb-3" />
          <h1 className="text-xl font-bold text-white">Módulo Restaurante não habilitado</h1>
          <p className="text-sm text-gray-400 mt-2">A ativação é opcional e feita pelo dono da plataforma para esta loja.</p>
        </div>
      </div>
    )
  }

  return (
    <div className="p-6 md:p-8 max-w-6xl mx-auto space-y-6">
      <PageHeader
        icon={UtensilsCrossed}
        title="Restaurante"
        description="Configurações adicionais de produção — sem alterar suas comandas atuais."
        actions={<button className="btn-primary" onClick={() => setEditing(null)}><Plus className="w-4 h-4" /> Nova área</button>}
      />

      <section className="card p-5">
        <div className="flex items-start gap-3 mb-5">
          <ChefHat className="w-5 h-5 text-brand-400 mt-0.5" />
          <div>
            <h2 className="font-semibold text-white">Áreas de produção</h2>
            <p className="text-sm text-gray-400">Cadastre os setores que futuramente receberão filas e impressão separadas, como cozinha e bar.</p>
          </div>
        </div>

        {loading ? (
          <div className="flex justify-center py-10"><Loader2 className="w-6 h-6 animate-spin text-brand-400" /></div>
        ) : areas.length === 0 ? (
          <div className="rounded-xl border border-dashed border-surface-500 p-8 text-center">
            <p className="text-gray-300 font-medium">Nenhuma área cadastrada</p>
            <p className="text-sm text-gray-500 mt-1">Crie “Cozinha”, “Bar” ou os setores que fazem sentido para esta operação.</p>
          </div>
        ) : (
          <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-3">
            {areas.map(area => (
              <article key={area.id} className={`rounded-xl border border-surface-500 bg-surface-800 p-4 ${area.isActive ? '' : 'opacity-55'}`}>
                <div className="flex items-start justify-between gap-3">
                  <div className="flex items-start gap-3 min-w-0">
                    <span className="w-3 h-3 rounded-full mt-1.5 shrink-0" style={{ backgroundColor: area.color }} />
                    <div className="min-w-0">
                      <h3 className="font-semibold text-white truncate">{area.name} {!area.isActive && <span className="text-[10px] text-gray-500">(inativa)</span>}</h3>
                      <p className="text-xs text-gray-500 mt-1 line-clamp-2">{area.description || 'Sem descrição'}</p>
                    </div>
                  </div>
                  <span className="text-[10px] text-gray-500 font-mono">#{area.displayOrder}</span>
                </div>
                <div className="flex gap-2 mt-4 pt-3 border-t border-surface-600">
                  <button className="btn-secondary text-xs py-1.5 flex-1 justify-center" onClick={() => setEditing(area)}><Pencil className="w-3.5 h-3.5" /> Editar</button>
                  {area.isActive ? (
                    <button className="btn-secondary text-xs py-1.5 text-red-400 hover:text-red-300" title="Desativar" onClick={() => setConfirmarDesativar(area)}><Power className="w-3.5 h-3.5" /></button>
                  ) : (
                    <button className="btn-secondary text-xs py-1.5 text-brand-400" onClick={() => reactivate(area)}><Power className="w-3.5 h-3.5" /> Reativar</button>
                  )}
                </div>
              </article>
            ))}
          </div>
        )}
      </section>

      <div className="grid md:grid-cols-3 gap-3">
        {[
          ['Adicionais e observações', 'Estrutura separada para ponto, extras e remoção de ingredientes.'],
          ['Fila da cozinha', 'Status recebido, preparando, pronto e servido por área.'],
          ['Salão e garçons', 'Transferência de mesas, responsável e taxa de serviço.'],
        ].map(([title, description]) => (
          <div key={title} className="rounded-xl border border-surface-600 bg-surface-800/50 p-4 opacity-70">
            <p className="text-sm font-medium text-gray-300">{title}</p>
            <p className="text-xs text-gray-500 mt-1">{description}</p>
            <span className="inline-block mt-3 text-[10px] uppercase tracking-wider text-brand-400">Próxima etapa</span>
          </div>
        ))}
      </div>

      {editing !== undefined && <AreaModal area={editing} onClose={() => setEditing(undefined)} onSaved={load} />}

      {confirmarDesativar && (
        <ConfirmDialog
          title="Desativar área"
          message={<>Desativar a área <strong>{confirmarDesativar.name}</strong>? O histórico dela é preservado e ela pode ser reativada depois.</>}
          confirmLabel="Desativar"
          loading={desativando}
          onConfirm={() => deactivate(confirmarDesativar)}
          onClose={() => setConfirmarDesativar(null)}
        />
      )}
    </div>
  )
}
