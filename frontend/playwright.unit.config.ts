import { defineConfig } from '@playwright/test'

// Helpers puros não precisam iniciar o standalone do Next.js. Mantê-los nesta
// configuração evita que uma falha operacional do servidor esconda o resultado
// das regras de sanitização e cooldown.
export default defineConfig({
  testDir: './tests',
  testMatch: /(?:html-escape|sefaz-cooldown)\.spec\.ts/,
  fullyParallel: true,
  reporter: 'line',
})
