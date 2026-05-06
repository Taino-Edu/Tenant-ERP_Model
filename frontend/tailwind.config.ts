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
          50:  '#f3f0ff',
          100: '#e9e3ff',
          200: '#d4cbff',
          300: '#BFA0FB',
          400: '#A37BFA', // secondary
          500: '#7839F3', // primary
          600: '#6c2ef0', // primary darker
          700: '#5820d0',
          800: '#4414a8',
          900: '#2e0b80',
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
          gold:   '#FFD700',
          green:  '#00F0A8',
          red:    '#FF3B30',
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
