import type { Config } from 'tailwindcss'

const config: Config = {
  content: [
    './app/**/*.{js,ts,jsx,tsx,mdx}',
    './components/**/*.{js,ts,jsx,tsx,mdx}',
  ],
  theme: {
    extend: {
      colors: {
        // Paleta alinhada com o design Maikon
        brand: {
          50:  '#EEF7FD',
          100: '#D9EFF9',
          200: '#B3DEF4',
          300: '#7EC8EC',
          400: '#5BBDE8',
          500: '#3EC2F2', // primary — azul Santuário Nerd
          600: '#1AAFD9', // primary darker
          700: '#167AAB',
          800: '#186288',
          900: '#1A5170',
        },
        surface: {
          900: '#121215', // Fundo da página (app bg)
          800: '#1A1A1F', // Sidebar / cards
          700: '#1E1E24', // Cards internos / hover
          600: '#1E1E24', // Input bg
          500: '#2D2D36', // Borders
          400: '#3a3a47', // Muted bg
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
