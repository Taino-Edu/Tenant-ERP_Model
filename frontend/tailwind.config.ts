import type { Config } from 'tailwindcss'

const config: Config = {
  content: [
    './app/**/*.{js,ts,jsx,tsx,mdx}',
    './components/**/*.{js,ts,jsx,tsx,mdx}',
  ],
  theme: {
    extend: {
      colors: {
        // 400/500/600 são dinâmicos — ligados a --brand-400/500/600 (CSS custom
        // properties setadas em runtime por TenantColorInjector a partir de
        // SiteConfig.ColorPrimary, ver app/admin/layout.tsx). O padrão
        // rgb(var(--x) / <alpha-value>) é suportado nativamente pelo Tailwind
        // (resolve /20, /30 etc. e gradientes from-brand-600/to-brand-400) sem
        // precisar de hack !important como o tema claro faz hoje com `surface`.
        // 50-300/700-900 continuam estáticos (não são consumidos por
        // componentes que precisam refletir a cor do tenant).
        brand: {
          50:  '#EEF7FD',
          100: '#D9EFF9',
          200: '#B3DEF4',
          300: '#7EC8EC',
          400: 'rgb(var(--brand-400) / <alpha-value>)',
          500: 'rgb(var(--brand-500) / <alpha-value>)', // primary (default #3EC2F2, configurável em SiteConfig.ColorPrimary)
          600: 'rgb(var(--brand-600) / <alpha-value>)',
          700: '#167AAB',
          800: '#186288',
          900: '#1A5170',
        },
        // Ligado a --surface-XXX (globals.css), mesmo padrão do brand acima —
        // resolve nativamente qualquer variante de opacidade (bg-surface-700/50,
        // hover:bg-surface-500/30, from-surface-900 etc.) em vez de precisar de
        // um !important por classe-e-opacidade específica pro tema claro (era
        // assim antes, e por isso variantes com opacidade "escapavam" do
        // override e apareciam com a cor crua do tema escuro vazando no claro).
        surface: {
          900: 'rgb(var(--surface-900) / <alpha-value>)', // Fundo da página (app bg)
          800: 'rgb(var(--surface-800) / <alpha-value>)', // Sidebar / cards
          700: 'rgb(var(--surface-700) / <alpha-value>)', // Cards internos / hover
          600: 'rgb(var(--surface-600) / <alpha-value>)', // Input bg
          500: 'rgb(var(--surface-500) / <alpha-value>)', // Borders
          400: 'rgb(var(--surface-400) / <alpha-value>)', // Muted bg
        },
        accent: {
          gold:   '#FFE45E',
          green:  '#00F0A8',
          red:    '#FF3B30',
          blue:   '#3b82f6',
          orange: '#f97316',
        }
      },
      fontFamily: {
        sans: ['var(--font-nunito)', 'ui-sans-serif', 'system-ui', 'sans-serif'],
        mono: ['JetBrains Mono', 'monospace'],
      },
      animation: {
        'pulse-slow':   'pulse 3s cubic-bezier(0.4, 0, 0.6, 1) infinite',
        'slide-in':     'slideIn 0.3s ease-out',
        'fade-in':      'fadeIn 0.2s ease-out',
        'bounce-in':    'bounceIn 0.4s ease-out',
      },
      keyframes: {
        slideIn:  { from: { transform: 'translateX(-1rem)', opacity: '0' }, to: { transform: 'translateX(0)', opacity: '1' } },
        fadeIn:   { from: { opacity: '0', transform: 'translateY(0.5rem)' }, to: { opacity: '1', transform: 'translateY(0)' } },
        bounceIn: { '0%': { transform: 'scale(0.9)', opacity: '0' }, '70%': { transform: 'scale(1.02)' }, '100%': { transform: 'scale(1)', opacity: '1' } },
      },
    },
  },
  plugins: [],
}

export default config
