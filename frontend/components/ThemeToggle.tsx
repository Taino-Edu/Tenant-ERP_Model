'use client'
import { useEffect, useState } from 'react'
import { Sun, Moon } from 'lucide-react'

export default function ThemeToggle({ compact = false }: { compact?: boolean }) {
  const [light, setLight] = useState(false)

  useEffect(() => {
    // MESMA regra do script anti-FOUC em app/layout.tsx: preferência salva, e
    // na falta dela o tema do sistema. As duas leituras precisam bater — se
    // divergirem, a tela pisca de um tema pro outro quando este efeito monta,
    // que era o que acontecia enquanto o comentário aqui prometia "escuro por
    // padrão" e o layout já tinha gravado 'light' antes de alguém ler.
    const consultaSistema = window.matchMedia('(prefers-color-scheme: dark)')
    const resolver = () => {
      const salvo = localStorage.getItem('theme')
      return salvo ? salvo === 'light' : !consultaSistema.matches
    }

    const aplicar = () => {
      const isLight = resolver()
      setLight(isLight)
      document.documentElement.classList.toggle('light', isLight)
    }
    aplicar()

    // Sem escolha explícita, acompanhar o sistema significa acompanhar também
    // quando ele muda — é o que o usuário espera de quem deixou no automático
    // e vê o computador virar pro escuro à noite. Quem já clicou no botão tem
    // `theme` salvo, e o `resolver()` ignora o sistema nesse caso.
    consultaSistema.addEventListener('change', aplicar)
    return () => consultaSistema.removeEventListener('change', aplicar)
  }, [])

  function toggle() {
    const next = !light
    setLight(next)
    document.documentElement.classList.toggle('light', next)
    localStorage.setItem('theme', next ? 'light' : 'dark')
  }

  if (compact) {
    return (
      <button
        onClick={toggle}
        title={light ? 'Mudar para tema escuro' : 'Mudar para tema claro'}
        className="w-8 h-8 rounded-lg flex items-center justify-center transition-colors hover:bg-[var(--border-color)]"
      >
        {light
          ? <Moon className="w-4 h-4 text-brand-400" />
          : <Sun  className="w-4 h-4 text-brand-400" />
        }
      </button>
    )
  }

  return (
    <button
      onClick={toggle}
      className="flex items-center gap-3 px-3 py-2 rounded-lg w-full transition-colors hover:bg-[var(--border-color)] text-[var(--text-muted)] hover:text-[var(--text-primary)]"
    >
      {light
        ? <Moon className="w-4 h-4 text-brand-400" />
        : <Sun  className="w-4 h-4 text-brand-400" />
      }
      <span className="text-sm">{light ? 'Tema Escuro' : 'Tema Claro'}</span>
    </button>
  )
}
