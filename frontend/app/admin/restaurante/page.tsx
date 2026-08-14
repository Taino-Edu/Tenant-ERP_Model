'use client'

import { useCallback, useEffect, useState } from 'react'
import {
  ComandaDto,
  RestaurantProductMappingDto,
  RestaurantProductionAreaDto,
  RestaurantProductionItemDto,
  RestaurantProductionStatus,
  SaveRestaurantProductionAreaRequest,
  comandaApi,
  getErrorMessage,
  restaurantApi,
} from '@/lib/api'
import { useSiteConfig } from '@/contexts/SiteConfigContext'
import PageHeader from '@/components/admin/PageHeader'
import Modal from '@/components/admin/ui/Modal'
import ConfirmDialog from '@/components/admin/ui/ConfirmDialog'
import NumberInput from '@/components/admin/ui/NumberInput'
import toast from 'react-hot-toast'
import { ArrowRight, ChefHat, Clock3, ExternalLink, Flame, Loader2, MessageSquare, PackageCheck, Pencil, Plus, Power, ReceiptText, UtensilsCrossed } from 'lucide-react'
import Link from 'next/link'
import { startHub, stopHub } from '@/lib/signalr'

const DEFAULT_COLOR = '#3EC2F2'
const PRODUCTION_STATUSES: RestaurantProductionStatus[] = ['Recebido', 'Preparando', 'Pronto']
const NEXT_STATUS: Record<Exclude<RestaurantProductionStatus, 'Servido'>, RestaurantProductionStatus> = {
  Recebido: 'Preparando',
  Preparando: 'Pronto',
  Pronto: 'Servido',
}

function elapsedLabel(openedAt: string) {
  const minutes = Math.max(0, Math.floor((Date.now() - new Date(openedAt).getTime()) / 60000))
  if (minutes < 60) return `${minutes} min`
  const hours = Math.floor(minutes / 60)
  return `${hours}h ${minutes % 60}min`
}

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
  const { site, loading: siteLoading } = useSiteConfig()
  const enabled = site.enabledModules.includes('restaurante')
  const [areas, setAreas] = useState<RestaurantProductionAreaDto[]>([])
  const [comandas, setComandas] = useState<ComandaDto[]>([])
  const [productionQueue, setProductionQueue] = useState<RestaurantProductionItemDto[]>([])
  const [products, setProducts] = useState<RestaurantProductMappingDto[]>([])
  const [loading, setLoading] = useState(true)
  const [updatingItem, setUpdatingItem] = useState<string | null>(null)
  const [updatingProduct, setUpdatingProduct] = useState<string | null>(null)
  const [editing, setEditing] = useState<RestaurantProductionAreaDto | null | undefined>(undefined)
  const [confirmarDesativar, setConfirmarDesativar] = useState<RestaurantProductionAreaDto | null>(null)
  const [desativando, setDesativando] = useState(false)

  const load = useCallback(async () => {
    if (!enabled) {
      setLoading(false)
      return
    }
    try {
      const [areasResult, comandasResult, queueResult, productsResult] = await Promise.allSettled([
        restaurantApi.listProductionAreas(true),
        comandaApi.dashboard(),
        restaurantApi.listProductionQueue(),
        restaurantApi.listProductMappings(),
      ])
      if (areasResult.status === 'rejected') throw areasResult.reason
      setAreas(areasResult.value.data)
      setComandas(comandasResult.status === 'fulfilled' ? comandasResult.value.data : [])
      setProductionQueue(queueResult.status === 'fulfilled' ? queueResult.value.data : [])
      setProducts(productsResult.status === 'fulfilled' ? productsResult.value.data : [])
    } catch (error) {
      toast.error(getErrorMessage(error, 'Não foi possível carregar o módulo Restaurante.'))
    } finally {
      setLoading(false)
    }
  }, [enabled])

  useEffect(() => {
    load()
    const interval = window.setInterval(load, 30000)
    let hub: Awaited<ReturnType<typeof startHub>> | undefined
    if (enabled) {
      startHub().then(connection => {
        hub = connection
        connection.on('ComandaUpdated', load)
        connection.on('ComandaOpened', load)
        connection.on('ComandaClosed', load)
        connection.on('ComandaCancelled', load)
        connection.on('ProductionStatusUpdated', load)
      }).catch(() => {})
    }
    return () => {
      window.clearInterval(interval)
      if (hub) {
        hub.off('ComandaUpdated', load)
        hub.off('ComandaOpened', load)
        hub.off('ComandaClosed', load)
        hub.off('ComandaCancelled', load)
        hub.off('ProductionStatusUpdated', load)
      }
      stopHub()
    }
  }, [enabled, load])

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

  async function advanceProduction(item: RestaurantProductionItemDto) {
    if (item.status === 'Servido') return
    setUpdatingItem(item.itemId)
    try {
      await restaurantApi.updateProductionStatus(item.comandaId, item.itemId, NEXT_STATUS[item.status])
      await load()
    } catch (error) {
      toast.error(getErrorMessage(error, 'Não foi possível atualizar o preparo.'))
    } finally {
      setUpdatingItem(null)
    }
  }

  async function assignProductArea(productId: string, productionAreaId: string | null) {
    setUpdatingProduct(productId)
    try {
      await restaurantApi.assignProductArea(productId, productionAreaId)
      setProducts(current => current.map(product => product.id === productId
        ? { ...product, productionAreaId }
        : product))
      toast.success(productionAreaId ? 'Produto encaminhado para a área.' : 'Produto removido da produção.')
    } catch (error) {
      toast.error(getErrorMessage(error, 'Não foi possível vincular o produto.'))
    } finally {
      setUpdatingProduct(null)
    }
  }

  if (siteLoading) {
    return <div className="p-10 flex justify-center"><Loader2 className="w-7 h-7 animate-spin text-brand-400" /></div>
  }

  if (!enabled) {
    return (
      <div className="p-4 sm:p-6 md:p-8 max-w-5xl mx-auto">
        <div className="card p-8 text-center">
          <UtensilsCrossed className="w-10 h-10 text-gray-500 mx-auto mb-3" />
          <h1 className="text-xl font-bold text-white">Módulo Restaurante não habilitado</h1>
          <p className="text-sm text-gray-400 mt-2">A ativação é opcional e feita pelo dono da plataforma para esta loja.</p>
        </div>
      </div>
    )
  }

  return (
    <div className="p-4 sm:p-6 md:p-8 max-w-6xl mx-auto space-y-6">
      <PageHeader
        icon={UtensilsCrossed}
        title="Restaurante"
        description="Comandas, salão e produção conectados em um único fluxo."
        actions={<button className="btn-primary" onClick={() => setEditing(null)}><Plus className="w-4 h-4" /> Nova área</button>}
      />

      <section className="card p-5">
        <div className="flex flex-wrap items-start justify-between gap-3 mb-5">
          <div className="flex items-start gap-3">
            <ReceiptText className="w-5 h-5 text-accent-green mt-0.5" />
            <div>
              <h2 className="font-semibold text-white">Operação do salão</h2>
              <p className="text-sm text-gray-400">Comandas abertas, pedidos e observações do cliente em um só lugar.</p>
            </div>
          </div>
          <Link href="/admin/comanda" className="btn-secondary text-xs py-2">
            Abrir gestão completa <ExternalLink className="w-3.5 h-3.5" />
          </Link>
        </div>

        {loading ? (
          <div className="flex justify-center py-10"><Loader2 className="w-6 h-6 animate-spin text-brand-400" /></div>
        ) : comandas.length === 0 ? (
          <div className="rounded-xl border border-dashed border-surface-500 p-7 text-center">
            <p className="text-gray-300 font-medium">Salão sem comandas ativas</p>
            <p className="text-sm text-gray-500 mt-1">Novas comandas aparecem aqui automaticamente.</p>
          </div>
        ) : (
          <div className="grid md:grid-cols-2 xl:grid-cols-3 gap-3">
            {comandas.map(comanda => (
              <article key={comanda.id} className="rounded-xl border border-surface-500 bg-surface-800 p-4 space-y-3">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="font-semibold text-white">{comanda.tableIdentifier || 'Balcão'} · {comanda.userName}</p>
                    <p className="text-xs text-gray-500 mt-1 flex items-center gap-1">
                      <Clock3 className="w-3.5 h-3.5" /> aberta há {elapsedLabel(comanda.openedAt)}
                    </p>
                  </div>
                  <span className="text-xs font-bold text-accent-gold">R$ {comanda.totalInReais.toFixed(2).replace('.', ',')}</span>
                </div>

                <div className="space-y-1.5 border-t border-surface-600 pt-3">
                  {comanda.items.length === 0 ? (
                    <p className="text-xs text-gray-500 italic">Aguardando o primeiro item.</p>
                  ) : comanda.items.slice(-4).map(item => (
                    <div key={item.id} className="flex justify-between gap-3 text-xs">
                      <span className="text-gray-300 truncate">{item.quantity}× {item.itemNameSnapshot}</span>
                      <span className="text-gray-500 shrink-0">R$ {item.subtotalInReais.toFixed(2).replace('.', ',')}</span>
                    </div>
                  ))}
                  {comanda.items.length > 4 && <p className="text-[11px] text-gray-500">+ {comanda.items.length - 4} item(ns)</p>}
                </div>

                {comanda.notes && (
                  <div className="rounded-lg border border-amber-500/25 bg-amber-500/10 px-3 py-2 text-xs text-amber-200 flex gap-2">
                    <MessageSquare className="w-3.5 h-3.5 shrink-0 mt-0.5" />
                    <span className="whitespace-pre-wrap break-words">{comanda.notes}</span>
                  </div>
                )}
              </article>
            ))}
          </div>
        )}
      </section>

      <section className="card p-5">
        <div className="flex items-start gap-3 mb-5">
          <Flame className="w-5 h-5 text-orange-400 mt-0.5" />
          <div>
            <h2 className="font-semibold text-white">Fila de produção</h2>
            <p className="text-sm text-gray-400">Cada item nasce na comanda e avança pela área responsável até ser servido.</p>
          </div>
        </div>

        {loading ? (
          <div className="flex justify-center py-10"><Loader2 className="w-6 h-6 animate-spin text-brand-400" /></div>
        ) : productionQueue.length === 0 ? (
          <div className="rounded-xl border border-dashed border-surface-500 p-7 text-center">
            <PackageCheck className="w-7 h-7 text-gray-600 mx-auto mb-2" />
            <p className="text-gray-300 font-medium">Nenhum item aguardando produção</p>
            <p className="text-sm text-gray-500 mt-1">Vincule produtos às áreas; novos itens aparecerão automaticamente.</p>
          </div>
        ) : (
          <div className="grid lg:grid-cols-3 gap-3 items-start">
            {PRODUCTION_STATUSES.map(status => {
              const statusItems = productionQueue.filter(item => item.status === status)
              return (
                <div key={status} className="rounded-xl border border-surface-600 bg-surface-900/50 p-3">
                  <div className="flex items-center justify-between mb-3">
                    <h3 className="text-xs font-black uppercase tracking-wider text-gray-300">{status}</h3>
                    <span className="text-[11px] rounded-full bg-surface-700 px-2 py-0.5 text-gray-400">{statusItems.length}</span>
                  </div>
                  <div className="space-y-2">
                    {statusItems.length === 0 ? <p className="text-xs text-gray-600 py-4 text-center">Fila vazia</p> : statusItems.map(item => {
                      const area = areas.find(current => current.id === item.productionAreaId)
                      const next = NEXT_STATUS[item.status as Exclude<RestaurantProductionStatus, 'Servido'>]
                      return (
                        <article key={item.itemId} className="rounded-lg border border-surface-600 bg-surface-800 p-3 space-y-2">
                          <div className="flex items-start justify-between gap-2">
                            <div className="min-w-0">
                              <p className="text-sm font-semibold text-white truncate">{item.quantity}× {item.itemName}</p>
                              <p className="text-[11px] text-gray-500">{item.tableIdentifier || 'Balcão'} · {item.userName} · {elapsedLabel(item.addedAt)}</p>
                            </div>
                            <span className="text-[10px] font-bold rounded-full px-2 py-1 shrink-0" style={{ color: area?.color ?? '#3EC2F2', backgroundColor: `${area?.color ?? '#3EC2F2'}18` }}>
                              {item.productionAreaName}
                            </span>
                          </div>
                          {item.comandaNotes && (
                            <p className="text-xs text-amber-200 bg-amber-500/10 rounded-md px-2 py-1.5 flex gap-1.5">
                              <MessageSquare className="w-3 h-3 shrink-0 mt-0.5" /> {item.comandaNotes}
                            </p>
                          )}
                          <button type="button" onClick={() => advanceProduction(item)} disabled={updatingItem === item.itemId} className="btn-secondary text-xs py-1.5 w-full justify-center">
                            {updatingItem === item.itemId ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <ArrowRight className="w-3.5 h-3.5" />}
                            {next === 'Servido' ? 'Marcar servido' : `Mover para ${next.toLowerCase()}`}
                          </button>
                        </article>
                      )
                    })}
                  </div>
                </div>
              )
            })}
          </div>
        )}
      </section>

      <section className="card p-5">
        <div className="flex items-start gap-3 mb-5">
          <ChefHat className="w-5 h-5 text-brand-400 mt-0.5" />
          <div>
            <h2 className="font-semibold text-white">Áreas de produção</h2>
            <p className="text-sm text-gray-400">Cadastre os setores que recebem os itens das comandas, como cozinha, bar e confeitaria.</p>
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

      <section className="card p-5">
        <div className="flex items-start gap-3 mb-5">
          <PackageCheck className="w-5 h-5 text-brand-400 mt-0.5" />
          <div>
            <h2 className="font-semibold text-white">Produtos por área</h2>
            <p className="text-sm text-gray-400">Define para onde cada novo item da comanda será enviado.</p>
          </div>
        </div>
        <div className="grid md:grid-cols-2 gap-2 max-h-[28rem] overflow-y-auto pr-1">
          {products.map(product => (
            <div key={product.id} className="flex items-center gap-3 rounded-xl border border-surface-600 bg-surface-800 px-3 py-2.5">
              <div className="min-w-0 flex-1">
                <p className="text-sm font-medium text-white truncate">{product.name}</p>
                <p className="text-[11px] text-gray-500 truncate">{product.category}</p>
              </div>
              <select
                value={product.productionAreaId ?? ''}
                disabled={updatingProduct === product.id}
                onChange={event => assignProductArea(product.id, event.target.value || null)}
                className="input text-xs py-1.5 w-40"
                aria-label={`Área de produção de ${product.name}`}
              >
                <option value="">Sem produção</option>
                {areas.filter(area => area.isActive).map(area => <option key={area.id} value={area.id}>{area.name}</option>)}
              </select>
              {updatingProduct === product.id && <Loader2 className="w-4 h-4 animate-spin text-brand-400" />}
            </div>
          ))}
        </div>
      </section>

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
