/**
 * Escapa texto antes de interpolá-lo em templates HTML.
 *
 * React já escapa conteúdo renderizado normalmente. Este helper é para os poucos
 * fluxos que montam um documento como string e o entregam a `document.write()`.
 */
export function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;')
}
