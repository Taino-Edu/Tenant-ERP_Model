# Changelog — Santuário Nerd

## [v1.6.0] — 2026-06-15

### Adicionado
- **Histórico de cliente** na área de usuários: acesse comandas, vendas no caixa (PDV), crediários e campeonatos de cada cliente em um único painel
- Vendas avulsas no caixa agora vinculam o cliente identificado, permitindo rastreamento futuro no histórico
- Estatísticas do cliente: total de visitas, total gasto, primeira e última visita

### Corrigido
- **Crediário**: itens de todas as comandas acumuladas agora aparecem corretamente no painel de crediário — antes apenas a primeira comanda aparecia
- **Venda Avulsa (mobile)**: barra fixa no rodapé do celular com total e botão "Finalizar" sem precisar rolar a página
- **Crediário**: overflow de DateTime ao calcular período de itens de crediário aberto (erro 500 no servidor)

---

## [v1.5.0] — 2026-06-12

### Adicionado
- Página **Sobre o Sistema** com versionamento e histórico de atualizações
- Relatório **PDV** com receita dia a dia, top produtos e formas de pagamento
- Relatório **Clientes** com pontos, validade e status de atividade
- Relatório **Comandas Abertas** com filtro por dias de abertura

### Corrigido
- Datas do relatório PDV exibindo "Invalid Date" (backend enviava apenas dd/MM)
- Pontos de fidelidade expirando em 1 ano em vez de 30 dias em ComandaService e VendaAvulsaService
- Autenticação MongoDB habilitada em produção com script de migração sem downtime

---

## [v1.4.0] — 2026-05-20

### Adicionado
- Pré-inscrições de campeonatos via landing page pública
- Pódio de campeonatos visível no painel do admin
- Painel de LGPD e auditoria de ações

### Corrigido
- Dashboard: barras do gráfico ancoradas corretamente no bottom
- Card do gráfico não esticava mais com o grid

---

## [v1.3.0] — 2026-05-10

### Adicionado
- Relatório de estoque em PDF
- Relatório financeiro e operacional em PDF
- Sistema de crediário com vencimento e histórico de itens

### Corrigido
- Foto de perfil do cliente não aparecia na área administrativa
- QR Codes de gatilho com link correto para o produto

---

## [v1.2.0] — 2026-04-15

### Adicionado
- Frente de Caixa (Venda Avulsa) com múltiplas formas de pagamento
- Pontos de fidelidade: 1 ponto por R$1 gasto, validade de 30 dias
- Cashback e pagamento por pontos acumulados

---

## [v1.1.0] — 2026-03-20

### Adicionado
- Catálogo TCG com busca integrada à API externa
- Campeonatos com inscrições e gerenciamento de rodadas
- Anúncios e banners configuráveis pelo admin

---

## [v1.0.0] — 2026-03-01

### Adicionado
- Lançamento inicial do sistema Santuário Nerd
- Gestão de estoque, categorias e produtos
- Painel administrativo com dashboard financeiro em tempo real
- Comandas de mesa com abertura, itens e fechamento
- Área do cliente com pontos, histórico e perfil
