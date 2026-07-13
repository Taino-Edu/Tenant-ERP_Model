'use client'
import { useEffect, useState, useCallback } from 'react'
import { platformApi, PlatformOverviewDto, getErrorMessage } from '@/lib/api'
import PageHeader from '@/components/admin/PageHeader'
import StatCard from '@/components/admin/StatCard'
import toast from 'react-hot-toast'
import { LayoutDashboard, DollarSign, Building2, Layers, CreditCard } from 'lucide-react'

function fmtReais(cents: number) {
  return (cents / 100).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
}

export default function PlataformaOverviewPage() {
  const [overview, setOverview] = useState<PlatformOverviewDto | null>(null)

  const fetchOverview = useCallback(() => {
    platformApi.getOverview()
      .then(r => setOverview(r.data))
      .catch(err => toast.error(getErrorMessage(err, 'Erro ao carregar visão geral')))
  }, [])

  useEffect(() => { fetchOverview() }, [fetchOverview])

  const paymentAtrasado = overview?.paymentStatusCounts?.Atrasado ?? 0
  const paymentPago     = overview?.paymentStatusCounts?.Pago ?? 0
  const moduleSub = overview
    ? Object.entries(overview.moduleAdoptionCounts).map(([m, c]) => `${m}: ${c}`).join(' · ') || 'nenhum módulo ativo'
    : undefined

  return (
    <div className="space-y-5">
      <PageHeader
        icon={LayoutDashboard}
        title="Visão Geral"
        description="Resumo agregado de todas as lojas da plataforma"
      />

      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <StatCard
          icon={DollarSign}
          label="Receita do mês"
          value={overview ? fmtReais(overview.receitaMesAtualCents) : '—'}
          tone="success"
        />
        <StatCard
          icon={Building2}
          label="Tenants ativos"
          value={overview ? overview.activeTenants : '—'}
          sub={overview ? `${overview.suspendedTenants} suspensos` : undefined}
          tone="brand"
        />
        <StatCard
          icon={Layers}
          label="Adoção de módulos"
          value={overview ? Object.values(overview.moduleAdoptionCounts).reduce((a, b) => a + b, 0) : '—'}
          sub={moduleSub}
          tone="neutral"
        />
        <StatCard
          icon={CreditCard}
          label="Pagamento em dia"
          value={overview ? paymentPago : '—'}
          sub={overview && paymentAtrasado > 0 ? `${paymentAtrasado} atrasado(s)` : undefined}
          tone={paymentAtrasado > 0 ? 'warning' : 'success'}
        />
      </div>
    </div>
  )
}
