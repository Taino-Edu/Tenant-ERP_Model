// =============================================================================
// SeverityBadge.tsx — Pill colorido pra AuditSeverity (Info/Warning/Critical).
// =============================================================================

import { Info, AlertTriangle, ShieldAlert } from 'lucide-react'
import type { AuditSeverity } from '@/lib/api'

const STYLES: Record<AuditSeverity, string> = {
  Info:     'bg-surface-600 text-gray-300 border-surface-500',
  Warning:  'bg-yellow-500/15 text-yellow-400 border-yellow-500/30',
  Critical: 'bg-red-500/15 text-red-400 border-red-500/30',
}

const ICONS: Record<AuditSeverity, typeof Info> = {
  Info: Info, Warning: AlertTriangle, Critical: ShieldAlert,
}

const LABELS: Record<AuditSeverity, string> = {
  Info: 'Info', Warning: 'Atenção', Critical: 'Crítico',
}

export default function SeverityBadge({ severity }: { severity: string }) {
  const s = (severity in STYLES ? severity : 'Info') as AuditSeverity
  const Icon = ICONS[s]
  return (
    <span className={`inline-flex items-center gap-1 text-xs font-medium px-2 py-0.5 rounded-full border ${STYLES[s]}`}>
      <Icon className="w-3 h-3" />
      {LABELS[s]}
    </span>
  )
}
