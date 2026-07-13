'use client'
// =============================================================================
// AuditLogDetailModal.tsx — Visão detalhada de um registro de auditoria.
// Traduz o JSON cru de Details (context/alteracoes/snapshotExcluido) pra uma
// leitura estruturada em vez de despejar o JSON na tela.
// =============================================================================

import { useState } from 'react'
import { X, Monitor, Smartphone, MapPin, Hash, Copy, Check, Radio, Cpu } from 'lucide-react'
import { AuditLogDto, PlatformAuditLogDto } from '@/lib/api'
import { parseAuditDetails, fieldLabel, formatAuditValue } from '@/lib/auditFormat'
import SeverityBadge from './SeverityBadge'

function fmtDateTime(iso: string) {
  return new Date(iso).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

function CopyableId({ value }: { value: string }) {
  const [copied, setCopied] = useState(false)
  return (
    <button
      onClick={() => { navigator.clipboard.writeText(value); setCopied(true); setTimeout(() => setCopied(false), 1500) }}
      className="inline-flex items-center gap-1 font-mono text-xs text-gray-400 hover:text-white transition-colors"
      title="Copiar"
    >
      {value}
      {copied ? <Check className="w-3 h-3 text-green-400" /> : <Copy className="w-3 h-3" />}
    </button>
  )
}

function MetaField({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <p className="text-xs text-gray-500">{label}</p>
      <div className="text-sm text-white mt-0.5">{children}</div>
    </div>
  )
}

export function AuditLogDetailModal({
  log,
  onClose,
}: {
  log: AuditLogDto | PlatformAuditLogDto
  onClose: () => void
}) {
  const parsed = parseAuditDetails(log.details)
  const tenantSlug = 'tenantSlug' in log ? log.tenantSlug : undefined
  const ua  = parsed.context?.userAgent
  const geo = parsed.context?.geo
  const hasContext = !!(ua?.browser || ua?.os || geo?.city || geo?.country)

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 px-4" onClick={onClose}>
      <div
        className="bg-surface-800 rounded-2xl shadow-2xl border border-surface-500 w-full max-w-lg p-6 max-h-[85vh] overflow-y-auto"
        onClick={e => e.stopPropagation()}
      >
        <div className="flex items-start justify-between mb-4">
          <div>
            <h3 className="text-lg font-bold text-white">{log.action}</h3>
            <p className="text-xs text-gray-400 mt-0.5">
              {log.entityType}{log.entityId ? ` #${log.entityId.slice(0, 12)}…` : ''}
            </p>
          </div>
          <button onClick={onClose} className="text-gray-400 hover:text-white transition-colors shrink-0">
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Metadados */}
        <div className="grid grid-cols-2 gap-3 mb-4 p-3 rounded-xl bg-surface-700/50 border border-surface-600">
          <MetaField label="Quando">{fmtDateTime(log.createdAt)}</MetaField>
          <MetaField label="Ator">{log.actorUserName ?? <span className="text-gray-500 italic">anônimo/sistema</span>}</MetaField>
          {tenantSlug && <MetaField label="Loja">{tenantSlug}</MetaField>}
          <MetaField label="Severidade"><SeverityBadge severity={log.severity} /></MetaField>
          <MetaField label="Canal">
            <span className="inline-flex items-center gap-1"><Radio className="w-3.5 h-3.5 text-gray-400" />{log.channel ?? '—'}</span>
          </MetaField>
          {log.traceId && <MetaField label="Trace ID"><CopyableId value={log.traceId} /></MetaField>}
        </div>

        {/* Diff de alteração */}
        {parsed.alteracoes && parsed.alteracoes.length > 0 && (
          <div className="mb-4">
            <p className="text-xs font-semibold text-gray-300 mb-2">O que mudou</p>
            <div className="rounded-xl border border-surface-600 overflow-hidden">
              <table className="w-full text-xs">
                <thead>
                  <tr className="bg-surface-700/50 text-gray-400">
                    <th className="text-left px-3 py-2 font-medium">Campo</th>
                    <th className="text-left px-3 py-2 font-medium">De</th>
                    <th className="text-left px-3 py-2 font-medium">Para</th>
                  </tr>
                </thead>
                <tbody>
                  {parsed.alteracoes.map((c, i) => (
                    <tr key={i} className="border-t border-surface-600">
                      <td className="px-3 py-2 text-gray-300">{fieldLabel(c.campo)}</td>
                      <td className="px-3 py-2 text-red-300/80">{formatAuditValue(c.campo, c.de)}</td>
                      <td className="px-3 py-2 text-green-300/80">{formatAuditValue(c.campo, c.para)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Snapshot de exclusão */}
        {parsed.snapshotExcluido && (
          <div className="mb-4">
            <p className="text-xs font-semibold text-gray-300 mb-2">Dados do registro excluído</p>
            <div className="rounded-xl border border-surface-600 divide-y divide-surface-600">
              {Object.entries(parsed.snapshotExcluido).map(([campo, valor]) => (
                <div key={campo} className="flex items-center justify-between px-3 py-1.5 text-xs">
                  <span className="text-gray-400">{fieldLabel(campo)}</span>
                  <span className="text-white text-right max-w-[60%] truncate">{formatAuditValue(campo, valor)}</span>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Mensagem simples */}
        {parsed.message && !parsed.alteracoes && !parsed.snapshotExcluido && (
          <p className="text-sm text-gray-300 mb-4">{parsed.message}</p>
        )}

        {/* Contexto de sessão */}
        {hasContext && (
          <div className="mb-2">
            <p className="text-xs font-semibold text-gray-300 mb-2">Sessão</p>
            <div className="flex flex-wrap gap-2">
              {(ua?.browser || ua?.os) && (
                <span className="inline-flex items-center gap-1.5 text-xs text-gray-300 bg-surface-700 border border-surface-600 rounded-full px-2.5 py-1">
                  {ua?.isMobile ? <Smartphone className="w-3.5 h-3.5" /> : <Monitor className="w-3.5 h-3.5" />}
                  {[ua?.browser, ua?.os].filter(Boolean).join(' · ')}
                </span>
              )}
              {(geo?.city || geo?.country) && (
                <span className="inline-flex items-center gap-1.5 text-xs text-gray-300 bg-surface-700 border border-surface-600 rounded-full px-2.5 py-1">
                  <MapPin className="w-3.5 h-3.5" />
                  {[geo?.city, geo?.country].filter(Boolean).join(', ')}
                </span>
              )}
              {ua?.device && ua.device !== 'Other' && (
                <span className="inline-flex items-center gap-1.5 text-xs text-gray-300 bg-surface-700 border border-surface-600 rounded-full px-2.5 py-1">
                  <Cpu className="w-3.5 h-3.5" />
                  {ua.device}
                </span>
              )}
            </div>
          </div>
        )}

        {/* JSON bruto, pra debug avançado */}
        {parsed.raw && (
          <details className="mt-4">
            <summary className="text-xs text-gray-500 hover:text-gray-300 cursor-pointer select-none flex items-center gap-1">
              <Hash className="w-3 h-3" /> Ver JSON bruto
            </summary>
            <pre className="mt-2 text-xs text-gray-400 bg-surface-900/60 rounded-lg p-3 overflow-x-auto whitespace-pre-wrap break-all">
              {JSON.stringify(parsed.raw, null, 2)}
            </pre>
          </details>
        )}
      </div>
    </div>
  )
}
