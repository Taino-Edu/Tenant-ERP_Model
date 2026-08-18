import type { Config } from 'tailwindcss'

const config: Config = {
  content: [
    './app/**/*.{js,ts,jsx,tsx,mdx}',
    './components/**/*.{js,ts,jsx,tsx,mdx}',
    // `lib` também: o Tailwind só gera a classe que ele ENXERGA no texto dos
    // arquivos varridos aqui. Quando o tema do site público saiu de dentro de
    // app/institucional/page.tsx para lib/institucional.ts, todo className que
    // só existia lá parou de ser gerado — e as seções perderam o fundo colorido
    // sem nenhum erro de build, de lint ou de teste para acusar. Qualquer
    // arquivo que monte string de classe precisa estar nesta lista.
    './lib/**/*.{js,ts,jsx,tsx,mdx}',
    './hooks/**/*.{js,ts,jsx,tsx,mdx}',
    './contexts/**/*.{js,ts,jsx,tsx,mdx}',
  ],
  theme: {
    extend: {
      // `xs` cobre a faixa 400-639px (iPhone SE/8 em pé são 375; Android médio
      // 393-412). Sem ele o salto de 0 -> 640 (`sm`) obriga a desenhar tudo
      // para o pior caso; com ele dá pra soltar 2 colunas já no celular comum.
      screens: {
        xs: '400px',
      },
      spacing: {
        // Recortes do sistema (notch, barra de gestos do iOS/Android). Usados
        // como `pb-safe-b`, `pt-safe-t` etc. em barras fixas — sem isso o
        // conteúdo da bottom bar fica embaixo da barra de gestos.
        'safe-t': 'env(safe-area-inset-top, 0px)',
        'safe-b': 'env(safe-area-inset-bottom, 0px)',
        'safe-l': 'env(safe-area-inset-left, 0px)',
        'safe-r': 'env(safe-area-inset-right, 0px)',
        // Altura das barras fixas do mobile — referenciadas por scroll-padding
        // e pelo respiro do fim das páginas.
        'topbar': '3.5rem',
        'tabbar': '4rem',
      },
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
        // Identidade FIXA da plataforma, derivada do ciano da logo (#28b0d6 =
        // octus-500, o mesmo valor de --brand-cyan). Existe separada de `brand`
        // porque `brand` é dinâmica: o TenantColorInjector troca --brand-400/500/600
        // pela cor da loja, e o site institucional — que fala da 3E Systen, não
        // de um tenant — não pode mudar de cor conforme quem visitou por último.
        //
        // A escala não é decorativa, é de contraste. O 500 é a cor da marca e
        // rende só 2,5:1 sobre branco: serve para preenchimento e ícone grande,
        // NUNCA para texto sobre fundo claro. Por isso:
        //   600 (4,9:1) — texto, link e botão primário sobre fundo claro
        //   700 (6,2:1) — hover do botão e texto pequeno sobre fundo claro
        //   400 (8,4:1 sobre o navy #08192d) — texto e ícone sobre fundo escuro
        octus: {
          50:  '#EEF8FC',
          100: '#D6EFF8',
          200: '#A9DFF0',
          300: '#6FCAE5',
          400: '#53C0DE',
          500: '#28B0D6', // a cor da logo
          600: '#1C7B96',
          700: '#186A80',
          800: '#14586B',
          900: '#0F4351',
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
        // Fontes do sistema mantêm o build reproduzível e evitam depender da
        // disponibilidade do Google Fonts durante cada deploy.
        sans: ['ui-sans-serif', 'system-ui', '-apple-system', '"Segoe UI"', 'sans-serif'],
        mono: ['JetBrains Mono', 'monospace'],
      },
      animation: {
        'pulse-slow':   'pulse 3s cubic-bezier(0.4, 0, 0.6, 1) infinite',
        'slide-in':     'slideIn 0.3s ease-out',
        'fade-in':      'fadeIn 0.2s ease-out',
        'bounce-in':    'bounceIn 0.4s ease-out',
        'sheet-up':     'sheetUp 0.28s cubic-bezier(0.32, 0.72, 0, 1)',
      },
      keyframes: {
        slideIn:  { from: { transform: 'translateX(-1rem)', opacity: '0' }, to: { transform: 'translateX(0)', opacity: '1' } },
        fadeIn:   { from: { opacity: '0', transform: 'translateY(0.5rem)' }, to: { opacity: '1', transform: 'translateY(0)' } },
        bounceIn: { '0%': { transform: 'scale(0.9)', opacity: '0' }, '70%': { transform: 'scale(1.02)' }, '100%': { transform: 'scale(1)', opacity: '1' } },
        // Bottom sheet do mobile: entra deslizando de baixo, curva de saída do
        // iOS (rápida no início, assenta devagar) pra não parecer "pulo".
        sheetUp:  { from: { transform: 'translateY(100%)' }, to: { transform: 'translateY(0)' } },
      },
    },
  },
  plugins: [],
}

export default config
