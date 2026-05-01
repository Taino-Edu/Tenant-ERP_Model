import type { Config } from 'tailwindcss'

const config: Config = {
  content: [
    './app/**/*.{js,ts,jsx,tsx,mdx}',
    './components/**/*.{js,ts,jsx,tsx,mdx}',
  ],
  theme: {
    extend: {
      colors: {
        // Paleta principal — tema dark card game
        brand: {
          50:  '#f3f0ff',
          100: '#e9e3ff',
          200: '#d4cbff',
          300: '#b4a4ff',
          400: '#9171ff',
          500: '#7c3aed', // Roxo principal
          600: '#6d28d9',
          700: '#5b21b6',
          800: '#4c1d95',
          900: '#3b0764',
        },
        surface: {
          900: '#0f0f13', // Fundo da página
          800: '#16161d', // Sidebar
          700: '#1e1e28', // Cards
          600: '#262633', // Input bg
          500: '#32323f', // Borders
          400: '#4a4a5a', // Muted text bg
        },
        accent: {
          gold:   '#f59e0b',
          green:  '#10b981',
          red:    '#ef4444',
          blue:   '#3b82f6',
          orange: '#f97316',
        }
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', 'sans-serif'],
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
