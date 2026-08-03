import { test, expect } from '@playwright/test';

test.describe('Cliente - Comanda UI', () => {
  test.skip(
    !process.env.RUN_BACKEND_E2E,
    'requer RUN_BACKEND_E2E=1 e backend de teste na porta 5000',
  )
  // Configuração simulada antes de cada teste
  test.beforeEach(async ({ page }) => {
    // Como os testes de frontend não dependem do banco de dados real em execução,
    // podemos mocar as respostas da API ou rodar contra o servidor de dev real.
    // Aqui faremos um teste simples contra a página inicial e a rota de cliente.
  });

  test('Deve exibir a página de login/QR Code com o novo tema', async ({ page }) => {
    // Supondo que a aplicação esteja rodando na porta 3000
    await page.goto('http://localhost:3000/mesa/Mesa1');
    
    // Verifica se o título com o nome da loja está visível
    await expect(page.locator('h1').filter({ hasText: 'Octus' })).toBeVisible();
    
    // Verifica a presença dos botões do formulário
    await expect(page.locator('button', { hasText: 'Abrir Comanda' })).toBeVisible();
  });
});
