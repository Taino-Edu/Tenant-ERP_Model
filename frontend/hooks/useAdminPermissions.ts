'use client'
import { useCallback, useEffect, useState } from 'react'
import { getRole, hasPermission } from '@/lib/auth'

/** Disparado pelo layout do admin quando o cookie de permissões é renovado. */
export const ADMIN_PERMISSIONS_EVENT = 'admin:permissions-updated'

/**
 * Papel e permissões de quem está logado no painel da loja.
 *
 * Existe pelo mesmo motivo do useMediaQuery: `getRole()` e `hasPermission()`
 * leem cookie, que não existe no SSR. Ler o valor real no primeiro render faz o
 * HTML do servidor divergir do client, o React descarta a árvore e remonta — na
 * Sidebar isso aparecia como um flash a cada navegação. Antes desta versão o
 * contorno estava copiado à mão na Sidebar e na MobileTabBar, e faltava por
 * completo no overlay de atalhos e na tela de Estoque, que é onde a divergência
 * ainda acontecia.
 *
 * Sempre nega antes de montar: um item restrito nasce escondido e aparece,
 * nunca o contrário.
 *
 * Isto NÃO é controle de acesso — quem autoriza é o OperatorPermissionMiddleware,
 * no backend. Aqui é só pra não oferecer o que vai voltar 403.
 */
export function useAdminPermissions() {
  const [version, setVersion] = useState(0)

  useEffect(() => {
    // Primeiro efeito: sai do estado "montando" e passa a ler o cookie de fato.
    setVersion(current => current + 1)

    // O cookie não avisa quando muda. O layout do admin renova a sessão a cada
    // 45 min; sem escutar isso, uma tela aberta ficaria com o menu do perfil
    // antigo até alguém recarregar a página.
    const onUpdate = () => setVersion(current => current + 1)
    window.addEventListener(ADMIN_PERMISSIONS_EVENT, onUpdate)
    return () => window.removeEventListener(ADMIN_PERMISSIONS_EVENT, onUpdate)
  }, [])

  const mounted = version > 0

  const can = useCallback(
    (permission: string) => mounted && hasPermission(permission),
    [mounted],
  )

  return {
    /** Já leu o cookie. Use quando precisar diferenciar "negado" de "ainda não sei". */
    mounted,
    isAdmin: mounted && getRole() === 'Admin',
    can,
  }
}
