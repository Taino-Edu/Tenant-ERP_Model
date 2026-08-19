'use client'
import { useCallback, useEffect, useState } from 'react'
import { hasPlatformPermission } from '@/lib/auth'

/** Disparado pelo PlataformaShell quando o cookie de permissões é renovado. */
export const PLATFORM_PERMISSIONS_EVENT = 'platform:permissions-updated'

/**
 * Permissões do integrante logado no painel da plataforma, prontas pra decidir
 * o que aparece na tela.
 *
 * Começa SEMPRE negando, pelo mesmo motivo do useMediaQuery: no SSR não existe
 * `document.cookie`, e ler o valor real no primeiro render faria o HTML do
 * servidor divergir do client. O valor verdadeiro entra no primeiro efeito.
 *
 * Negar antes de montar significa que um botão restrito nasce escondido e
 * aparece — nunca o contrário. É a ordem certa: piscar um botão que a pessoa
 * não pode usar é pior do que ele chegar um quadro depois.
 *
 * Isto NÃO é controle de acesso: quem autoriza é o PlatformAccessMiddleware, no
 * backend. Aqui é só pra não oferecer uma ação que vai voltar 403.
 */
export function usePlatformPermissions() {
  const [version, setVersion] = useState(0)

  useEffect(() => {
    // Primeiro efeito: sai do estado "montando" e passa a ler o cookie de fato.
    setVersion(current => current + 1)

    // O cookie não avisa ninguém quando muda. O shell renova a sessão ao entrar
    // e a cada 45 min; sem escutar isso, uma tela aberta ficaria com o menu de
    // ações do perfil antigo até alguém recarregar a página.
    const onUpdate = () => setVersion(current => current + 1)
    window.addEventListener(PLATFORM_PERMISSIONS_EVENT, onUpdate)
    return () => window.removeEventListener(PLATFORM_PERMISSIONS_EVENT, onUpdate)
  }, [])

  return useCallback(
    (permission: string) => version > 0 && hasPlatformPermission(permission),
    [version],
  )
}
