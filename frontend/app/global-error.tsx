'use client'

// =============================================================================
// global-error.tsx — Último recurso: pega erro que estourou no PRÓPRIO
// app/layout.tsx (ou no error.tsx irmão). Só renderiza nesse caso; erro de
// página normal cai no error.tsx do segmento, que é bem mais informativo.
//
// Substitui o root layout inteiro, então precisa das próprias tags <html>/<body>
// — e não pode contar com o globals.css, que é importado justamente pelo layout
// que falhou. Por isso os estilos aqui são inline, com os valores do tema
// escuro (--bg-primary / --bg-card / --border-color de globals.css) escritos
// à mão. É a única tela do projeto que duplica cor de propósito.
// =============================================================================

export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string }
  reset: () => void
}) {
  return (
    <html lang="pt-BR">
      <body
        style={{
          margin: 0,
          minHeight: '100vh',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          padding: '24px',
          backgroundColor: '#121215',
          color: '#F3F4F6',
          fontFamily: 'system-ui, -apple-system, Segoe UI, sans-serif',
        }}
      >
        <div style={{ maxWidth: '28rem', width: '100%', textAlign: 'center' }}>
          <div
            style={{
              width: '4rem',
              height: '4rem',
              margin: '0 auto 1.5rem',
              borderRadius: '1rem',
              backgroundColor: '#1E1E24',
              border: '1px solid #2D2D36',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              fontSize: '1.75rem',
            }}
            aria-hidden="true"
          >
            ⚠️
          </div>

          <h1 style={{ fontSize: '1.25rem', fontWeight: 700, margin: '0 0 0.5rem' }}>
            Algo deu errado
          </h1>

          <p style={{ fontSize: '0.875rem', color: '#9CA3AF', margin: '0 0 1.5rem', lineHeight: 1.6 }}>
            Não conseguimos carregar o sistema. Tente novamente — se continuar
            assim, recarregue a página ou avise o suporte.
          </p>

          <button
            type="button"
            onClick={reset}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '0.5rem',
              padding: '0.5rem 1rem',
              backgroundColor: '#3EC2F2',
              color: '#FFFFFF',
              fontWeight: 600,
              border: 'none',
              borderRadius: '0.75rem',
              cursor: 'pointer',
              fontSize: '0.9375rem',
            }}
          >
            Tentar novamente
          </button>

          {/* digest é o hash que o Next.js grava no log do servidor — é o que
              liga esta tela à stack trace real, já que a mensagem do erro é
              omitida em produção de propósito. */}
          {error.digest && (
            <p style={{ fontSize: '0.75rem', color: '#4B5563', marginTop: '1.5rem' }}>
              Código do erro: <code>{error.digest}</code>
            </p>
          )}
        </div>
      </body>
    </html>
  )
}
