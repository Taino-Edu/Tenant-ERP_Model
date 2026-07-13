# Casos de Uso — Tenant-ERP

> Documento de referência pra QA, onboarding e planejamento de testes. Descreve
> os fluxos principais do sistema do ponto de vista de quem usa — não de como o
> código é organizado (isso está em `DOCUMENTACAO-COMPLETA.md`). Cada caso lista
> ator, pré-condição, passos, resultado esperado e os pontos que mais quebram
> (onde vale focar teste manual/automatizado).

---

## 1. Autenticação

### 1.1 Login do Admin/Operador
**Ator:** Admin ou Operator de uma loja.
**Pré-condição:** conta já existe (Admin é criado no provisionamento da loja; Operator é criado por um Admin em Clientes → Operadores).
**Passos:** acessa `/login` no subdomínio da loja → informa e-mail/senha → recebe cookies HttpOnly (`accessToken`, `refreshToken`) → redireciona pro painel.
**Resultado esperado:** sessão válida por 15 min de access token, renovada silenciosamente a cada 45 min enquanto a aba estiver aberta.
**Pontos de atenção:**
- Rate limit de 15 tentativas/min por IP (política `"auth"`) — mais que isso vira 429, mensagem "Muitas tentativas, aguarde 1 minuto".
- Operator só vê no menu as seções que o Perfil de Acesso dele permite (`OperatorPermissionMiddleware`).
- Login errado (senha/e-mail) e conta desativada (`IsActive=false`) devem dar a mesma mensagem genérica (não vazar qual dos dois casos é, pra não ajudar enumeração de e-mails).

### 1.2 Quick-login do cliente via QR Code
**Ator:** Cliente sentado numa mesa.
**Pré-condição:** QR Code da mesa já gerado (`/admin/qrcodes`).
**Passos:** escaneia o QR → cai em `/mesa/{nome}` → informa CPF (validado Módulo 11 no cliente e no servidor) + WhatsApp → aceita o consentimento LGPD → comanda abre (ou reabre, se já tinha uma aberta na mesma mesa) automaticamente.
**Resultado esperado:** cliente cai direto na tela de acompanhamento da própria comanda, sem senha.
**Pontos de atenção:** CPF com dígito verificador inválido deve ser rejeitado ANTES de criar usuário (não só client-side — o servidor sempre revalida). Reabrir a mesma mesa não deve duplicar comanda.

### 1.3 Login do Contador
**Ator:** Contador (conta cross-tenant, vive no catálogo, não em nenhum schema de loja específico).
**Pré-condição:** conta criada em `/contador/cadastro`, e pelo menos um vínculo aprovado com uma loja (convite do lojista ou solicitação aceita).
**Passos:** login em `/contador` (domínio raiz, não subdomínio de loja) → vê lista de lojas vinculadas → escolhe uma → painel mostra dados só daquela loja.
**Pontos de atenção:** contador sem nenhum vínculo aprovado ainda deve ver estado vazio claro, não erro.

---

## 2. Comanda (mesa via QR Code)

### 2.1 Abrir comanda e pedir itens
**Ator:** Cliente (self-service) ou Admin/Operator (adicionando por ele, ex: cliente sem celular).
**Passos:** comanda abre no quick-login (1.2) → cliente ou admin adiciona itens (busca por nome/categoria, ou código de barras via câmera/leitor USB no admin) → total atualiza em tempo real (SignalR) nos dois lados.
**Resultado esperado:** painel do admin (`/admin/comanda`) mostra a comanda nova/atualizada sem precisar de F5.
**Pontos de atenção:** estoque decrementa atomicamente ao adicionar item (`ExecuteUpdateAsync` com guarda de negatividade) — duas adições simultâneas do mesmo produto não podem deixar estoque negativo.

### 2.2 Aplicar/remover pontos de fidelidade (se o programa estiver ativo)
**Ator:** Cliente, na própria comanda aberta.
**Pré-condição:** `SiteConfig.PontosFidelidadeAtivo = true` (senão a opção nem aparece); cliente tem saldo de pontos não expirado.
**Passos:** cliente escolhe quantos pontos aplicar → sistema abate do total da comanda e do saldo do cliente → pode remover antes do fechamento (devolve o saldo).
**Pontos de atenção:** não deixa aplicar pontos já expirados (`PointsExpiresAt`); não deixa aplicar duas vezes na mesma comanda sem remover antes; com o programa desligado, o endpoint rejeita mesmo que alguém tente forçar via API direto (defesa em profundidade, não só esconder botão).

### 2.3 Fechar comanda
**Ator:** Admin/Operator.
**Passos:** escolhe forma de pagamento (Pix/Dinheiro/Cartão/Crediário/Pontos/Cashback), opcionalmente um segundo método (split), desconto administrativo, e se emite NFC-e agora.
**Resultado esperado:** comanda vira `Fechada`, mesa libera, cliente ganha pontos (se aplicável e programa ativo), crediário é criado/acumulado se essa foi a forma escolhida, comprovante (com ou sem nota fiscal) fica disponível pra reimprimir depois.
**Pontos de atenção:**
- Se o admin marcou emitir NFC-e mas a loja não tem módulo Fiscal ou não está configurada: a venda completa normalmente, a nota fica `PendenteEmissao` com motivo, **nunca trava o fechamento**.
- Sem emitir nota (ou nota pendente), ainda sai um comprovante não-fiscal (térmico/PDF) — nunca fica sem nenhum papel.
- Pagar com Pontos/Cashback com saldo insuficiente rejeita ANTES de mexer no estoque/total.

### 2.4 Cancelar comanda
**Ator:** Admin.
**Resultado esperado:** itens não descontam mais do relatório, estoque reservado é devolvido, pontos que o cliente tinha aplicado voltam pro saldo dele.

---

## 3. Frente de Caixa (Venda Avulsa / PDV)

### 3.1 Registrar venda no balcão
**Ator:** Admin/Operator, sem precisar de um cliente identificado (venda anônima é permitida, exceto crediário/pontos/cashback que exigem cliente cadastrado).
**Passos:** wizard de 3 etapas (cliente opcional → itens → pagamento) → confirma → comprovante aparece na hora com opção de imprimir/PDF e emitir nota.
**Pontos de atenção:** mesmas regras de pontos/cashback/crediário da comanda (item 2.3) se aplicam aqui também — é o mesmo motor de pagamento.

### 3.2 Editar forma de pagamento de uma venda já fechada
**Ator:** Admin, pelo histórico do dia.
**Resultado esperado:** troca o método registrado sem re-executar o cálculo de pontos/estoque (só corrige o registro).

---

## 4. Estoque e Catálogo

### 4.1 Cadastrar produto
**Ator:** Admin/Operator com permissão de estoque.
**Passos:** nome, categoria (aba dentro da própria tela de Estoque, não uma tela separada), preço, custo, estoque mínimo, imagem, opcionalmente variantes (tamanho/cor).
**Pontos de atenção:** margem calculada automaticamente (`(preço-custo)/custo`); alerta de estoque baixo aparece assim que `StockQuantity <= MinimumStock`.

### 4.2 Ajustar estoque manualmente
**Ator:** Admin.
**Resultado esperado:** incremento/decremento direto, nunca deixa ficar negativo (rejeitado antes de aplicar).

### 4.3 Pré-venda / lista de espera
**Ator:** Cliente entra na fila de um produto zerado marcado como pré-venda; Admin avisa quando chega.
**Resultado esperado:** aviso automático (in-app + push + e-mail) pra fila inteira assim que o estoque sai de zero, uma vez só por pessoa.

---

## 5. Crediário

### 5.1 Abrir crediário (venda/comanda no fiado)
**Resultado esperado:** se o cliente já tem um crediário aberto, acumula nele (mesmo vencimento); senão cria um novo com vencimento em 30 dias.

### 5.2 Registrar pagamento parcial
**Ator:** Admin.
**Resultado esperado:** saldo devedor diminui, histórico de pagamento fica registrado; ao zerar, crediário muda pra quitado.

---

## 6. Fiscal (NFC-e)

### 6.1 Configurar emissão fiscal
**Ator:** Admin.
**Pré-condição:** loja precisa ter o módulo Fiscal habilitado (controlado pelo dono da plataforma em `/plataforma`).
**Passos:** CNPJ, razão social, endereço, upload do certificado A1 (senha do certificado nunca fica em texto puro — criptografado). Naturezas de operação (CFOP/CSOSN) configuráveis.
**Pontos de atenção:** certificados antigos (RC2/3DES) usam BouncyCastle como fallback — o OpenSSL 3 do Linux desativa esse algoritmo por padrão.

### 6.2 Emitir NFC-e no fechamento
Ver 2.3/3.1 — emissão nunca é automática, sempre opt-in por fechamento (checkbox), com pré-marcação configurável por forma de pagamento.

### 6.3 Cancelar nota autorizada
**Ator:** Admin, dentro da janela legal (30 min após emissão), com justificativa de 15+ caracteres.

### 6.4 Reprocessar nota pendente/rejeitada
Automático via job de retry em background; se a comanda/venda de origem foi cancelada enquanto a nota estava pendente, a nota é anulada em vez de reprocessada.

---

## 7. Programa de Fidelidade (opcional por loja)

### 7.1 Ligar/desligar
**Ator:** Admin, em Personalizar Site.
**Resultado esperado:** desligar não apaga saldo nem histórico de ninguém — só para de dar/aceitar pontos novos a partir de agora. Seção "Pontos" some da landing pública, das telas de pagamento e do perfil do cliente.

### 7.2 Ganhar pontos
Automático ao fechar comanda/venda com pagamento que não seja Crediário/Pontos/Cashback: 1 ponto por R$1 gasto, válido por 30 dias.

---

## 8. Portal do Contador

### 8.1 Vincular contador a uma loja
Dois fluxos: lojista convida por e-mail (vincula na hora se o contador já tem conta; senão fica como convite "cego" até o contador se cadastrar) OU contador solicita acesso pelo slug da loja (fica pendente até o lojista aprovar em Fiscal).

### 8.2 Acompanhar saúde fiscal
**Ator:** Contador.
**Resultado esperado:** vê validade do certificado A1, dias desde a última nota emitida, lembrete de DAS (dia 20) pra loja do Simples Nacional, mural de avisos bidirecional com o lojista.

---

## 9. Central do Dono da Plataforma

### 9.1 Provisionar loja nova
**Ator:** Dono da plataforma.
**Passos:** informa slug + e-mail/senha do admin inicial → sistema cria schema Postgres dedicado, roda migrations, cria o admin.
**Pontos de atenção:** provisionamento é serializado (semáforo) — dois cadastros simultâneos não colidem.

### 9.2 Ver overview financeiro/atividade
**Resultado esperado:** receita do mês agregada (soma dos fechamentos diários, não do fechamento mensal — evita atraso de até 30 dias no número), lojas ativas/suspensas, adoção de módulos, última atividade por loja.

### 9.3 Acessar o admin de uma loja (impersonação)
**Ator:** Dono da plataforma.
**Passos:** clica "Acessar admin" → ticket de uso único (90s de validade) → abre nova aba já autenticado no subdomínio certo.
**Pontos de atenção:** sessão de impersonação usa o próprio Id do dono como identidade (nunca o Id do admin real da loja) — sair da simulação nunca derruba a sessão de verdade de ninguém; expira sozinha em 20 min sem refresh.

---

## 10. LGPD

### 10.1 Cliente solicita seus dados
**Ator:** Titular de dados (cliente), sem precisar estar logado.
**Passos:** formulário público `/lgpd`, CPF validado (Módulo 11) → escolhe acesso, correção, exclusão ou portabilidade → admin responde dentro do prazo legal em `/admin/lgpd`.
**Resultado esperado:** exclusão anonimiza (nome vira "Usuário Deletado") mas não apaga histórico fiscal/financeiro (obrigação legal de guarda).

---

## Fluxos que ainda não existem (fora de escopo atual)

- Cobrança automática de assinatura da plataforma via gateway (Asaas/Stripe) — hoje é suspensão manual.
- Suporte/tickets integrado.
- Analytics de uso detalhado (horas de atividade, telas acessadas) — só existe o sinal barato de "login/venda recente" no overview da plataforma.
