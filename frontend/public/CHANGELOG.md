# Changelog — Santuário Nerd

## [v1.7.3] — 2026-06-22

### Adicionado
- **Financeiro — Crediários recebidos no período**: o card "Crediários abertos" agora exibe no sub-texto o total recebido no período filtrado; ao clicar abre modal com lista detalhada de cada pagamento (cliente, valor, forma de pagamento, horário e observação)
- **Relatório PDF de Crediário**: novo PDF disponível na tela de Relatórios — mostra situação atual de todos os devedores (saldo, dias em atraso, vencimento, WhatsApp) e tabela completa de pagamentos recebidos no mês com subtotal ao final

---

## [v1.7.2] — 2026-06-22

### Corrigido
- **Financeiro — filtro "Hoje" zerado**: `toDateInput` usava `toISOString()` (UTC) em vez de data local — após 21h no Brasil o frontend mandava "amanhã" pro backend, resultando em dados zerados (exceto crediário, que não depende do filtro de data)
- **Gráfico de receita sem labels**: backend retornava dia no formato `dd/MM` mas frontend aplicava `.slice(5)` esperando `yyyy-MM-dd` — labels ficavam em branco; corrigido para ISO no AnalyticsController
- **PDFs de relatório com data incorreta**: funções de geração de PDF (Financeiro Mensal e PDV) usavam `toISOString()` para calcular início/fim, podendo retornar um dia a menos ou a mais por causa do UTC
- **Relatório de vendas e crediário sem fuso horário**: RelatoriosController usava UTC puro — vendas após 21h no Brasil (00h UTC do dia seguinte) podiam cair no mês errado; corrigido para horário de Brasília igual ao AnalyticsController

---

## [v1.7.1] — 2026-06-22

### Adicionado
- **Sistema de preferências por perfil**: VLibras, chat IA, intervalo de atualização do dashboard, painéis visíveis e desconto padrão do PDV configuráveis por usuário — mudanças aplicadas em tempo real sem recarregar a página
- **Dashboard redesenho**: 3 tabs (Ativas / Histórico / Análises) — comandas aparecem imediatamente ao abrir o painel, sem scroll
- **Tab Análises no dashboard**: painéis financeiros colapsáveis com persistência individual, esquema de cores do gráfico (Padrão, Azul, Neon) e intervalo de atualização automática configuráveis
- **PDV — Wizard 3 etapas**: fluxo guiado (cliente → itens → pagamento) com analytics integrados de pico de horário, top produtos e formas de pagamento usadas
- **PDV — Barra flutuante de finalização**: visível em todas as etapas, com desconto rápido embutido e total atualizado em tempo real
- **PDV — Segundo pagamento livre**: valor do segundo método pode ser qualquer valor (antes era calculado automaticamente pelo saldo restante)
- **Carrossel de banners**: rotação automática com setas de navegação na seção de avisos/destaques e no hero da landing page
- **Campeonatos**: confirmação de pré-inscrições recebidas pela landing page + pódio com lista completa de participantes
- **Chat IA**: botão arrastável com posição salva entre sessões, posição fixa configurável por canto da tela

### Corrigido
- Preferências exigiam F5 para serem aplicadas — agora propagam via Context React em tempo real para todos os componentes
- Margem financeira exibida como % sobre custo (padrão de mercado), não em reais absolutos
- Intervalo de polling do dashboard recria o timer imediatamente ao ser alterado nas configurações
- Interceptor de API redireciona por contexto (/admin → /login, /cliente → /entrar) em vez de sempre ir para /login
- Custo de vendas avulsas históricas corrigido via backfill automático

---

## [v1.7.0] — 2026-06-16

### Adicionado
- **Sistema de Perfis de Acesso**: admin cria perfis nomeados (ex: Caixa, Estoquista) com checklist de 14 permissões e os atribui a operadores
- **Aba Operadores** na tela de usuários: cadastro de operadores com e-mail, senha e perfil atribuído
- **Sidebar dinâmica**: operadores veem apenas as seções permitidas pelo seu perfil
- **Renovação automática de sessão**: token renovado silenciosamente a cada 45 min, evitando desconexão por inatividade
- **Manual do usuário** na página Sobre: 9 módulos explicados com seções expansíveis

### Corrigido
- Pontos de fidelidade não são mais acumulados quando cashback é usado em qualquer parte do pagamento (método principal ou secundário)
- Operadores redirecionados corretamente para o painel admin ao fazer login (antes iam para a tela de cliente)

---

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
