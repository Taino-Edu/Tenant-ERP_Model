# Plano — Bling: comparação, migração e sincronização

**Estado:** `PLANEJAMENTO` · **Registrado em:** 2026-09-18

Três frentes, nesta ordem, porque cada uma alimenta a seguinte:

1. **Comparação** — onde o Octus ganha e onde perde do Bling. Decide para quem
   as outras duas servem.
2. **Migração** — o lojista que usa Bling traz o cadastro sem digitar nada. É a
   alavanca de aquisição: tira a maior barreira para trocar de sistema.
3. **Sincronização** — o lojista mantém o Bling (e os marketplaces ligados nele)
   e usa o Octus no balcão. Reaproveita a conexão e o mapeamento da migração.

> O que está aqui sobre o Bling vem da documentação pública e de análises de
> terceiros (fontes no fim). Ninguém da equipe testou o Bling por dentro nem
> criou app na conta de desenvolvedor dele — a Fase 0 existe para isso.

---

## 1. Comparação

### Onde cada um é forte

| | Bling | Octus |
|---|---|---|
| **Foco** | Retaguarda de e-commerce: pedidos de marketplace, notas, logística | Loja física e food service: balcão, comanda, vitrine própria |
| **Marketplaces** (Mercado Livre, Shopee, Amazon…) | Integração nativa, é o motivo de muita gente usar | Não tem. "Marketplace" no código é a vitrine da própria plataforma |
| **Nota fiscal** | NF-e (55), NFC-e (65) e NFS-e | NFC-e (65). Recebe NF-e de fornecedor, mas não emite modelo 55 |
| **Estoque** | Vários depósitos | Um estoque por produto, variantes só com tamanho e cor |
| **Financeiro** | Contas a pagar e a receber, conta digital | Contas a receber, crediário, fechamento de caixa, DRE. Não achei fornecedores nem contas a pagar |
| **Logística** | Etiquetas, remessas, integração com transportadoras | Não tem |
| **Comanda / restaurante** | Não é o foco | Módulo próprio, no plano base |
| **Vitrine e app com a marca da loja** | Não | Subdomínio próprio e PWA instalável |
| **Fidelidade, eventos, portal do contador, IA** | Não identificado | Tem (planos Rio e Mar) |
| **Implantação** | Autoatendimento | Autoatendimento pelo site (PR #134) |
| **Preço** | A partir de ~R$ 55/mês segundo análises de terceiros; planos modulares por volume de notas e usuários (**confirmar na página oficial**) | Lagoa R$ 129 · Rio R$ 269 · Mar R$ 487 |

### O que isso quer dizer

- **O Octus não concorre com o Bling de frente.** Quem vende em marketplace
  precisa do Bling e não vai trocar. Quem é loja física ou restaurante usa o
  Bling por falta de opção, e é esse que o Octus conquista.
- Daí as duas outras frentes:
  - **Migração** é para quem **sai** do Bling (loja física, sem marketplace).
  - **Sincronização** é para quem **fica** no Bling por causa do marketplace e
    quer o Octus no balcão. É um público menor e bem mais caro de atender.
- Buracos que aparecem na comparação e pesam em venda: **NF-e modelo 55**
  (atacado, venda para CNPJ) e **contas a pagar**. Viram itens de backlog, não
  fazem parte deste plano.

---

## 2. Migração (Bling → Octus)

### 2a. Planilha "formato Bling" — primeiro, e barato

O Bling exporta produtos e contatos em planilha. A importação por CSV já existe
(`ImportController`, componente `ImportarDados`). Falta reconhecer o layout do
Bling:

- Detectar pelas colunas do cabeçalho que o arquivo veio do Bling e mapear para
  o formato do Octus sozinho, sem o lojista montar planilha.
- Mesmo comportamento de hoje: importa as linhas válidas e lista as inválidas
  com o motivo.
- **Não depende de conta de desenvolvedor, app aprovado nem OAuth.** Dá para
  entregar em dias e já serve para o primeiro lojista que vier do Bling.

Precisa de uma exportação real do Bling (produtos e contatos) para mapear as
colunas certas. Não chutar o layout.

### 2b. "Conectar com o Bling" — pela API

Um botão em Integrações: o lojista entra na conta Bling dele (OAuth), escolhe o
que importar e acompanha o progresso.

**O que entra:**

| Bling | Octus | Observação |
|---|---|---|
| Categorias de produto | `ProductCategory` | |
| Produtos | `Product` | nome, descrição, preço, custo, GTIN → `Barcode`, NCM, CEST, imagens |
| Variações | `ProductVariant` | só atributos de tamanho e cor cabem hoje (ver lacunas) |
| Estoque | `StockQuantity` | o Bling tem vários depósitos: o lojista escolhe um ou soma |
| Contatos (clientes) | `User` (cliente) | nome, CPF, e-mail, telefone |
| Contas a receber em aberto | Crediário | só se o cliente já foi importado — mesma regra do `ImportCrediario` |

**Fica de fora, de propósito:** notas fiscais emitidas (são do CNPJ no Bling; o
histórico fiscal fica lá), pedidos antigos (viram só relatório, não operação) e
contas a pagar (o Octus não tem onde guardar).

**Como funciona por dentro:**

- **Tabela de vínculo** `external_links` no schema do tenant: provedor, tipo,
  id no Octus, id no Bling, hash do último estado. Rodar a importação de novo
  **atualiza em vez de duplicar**, e a sincronização (frente 3) nasce em cima
  dela.
- **Job em segundo plano** com barra de progresso. Limite do Bling: **3
  requisições por segundo e 120 mil por dia**. Listar é paginado; buscar o
  detalhe de 10 mil produtos, um por um, leva perto de uma hora. O job precisa
  sobreviver a deploy e retomar de onde parou — outro motivo para o worker com
  Hangfire da lista de eficiência.
- **Prévia antes de gravar:** "vamos criar 1.240 produtos, 3 com NCM inválido,
  18 clientes sem nome". O lojista confirma.
- **Credenciais do tenant**, cifradas com o `EncryptionService`. Hoje o
  `IntegrationConfig` já guarda `AccessToken`/`RefreshToken`/`ExpiresAt` por
  origem (usado pelo Banco Inter) e serve de base.

### Lacunas do cadastro do Octus

Aparecem já na migração e ficam piores na sincronização:

1. **Produto sem código (SKU) próprio** — só a variante tem `Sku`. O Bling
   identifica produto pelo `codigo`. Sem ele, produto sem variante não tem
   chave estável.
2. **Variante só com tamanho e cor** — o Bling aceita qualquer atributo
   (voltagem, sabor, tamanho do calçado…). Variação fora disso entra com nome
   livre e perde a estrutura.
3. **Cliente sem CNPJ e sem endereço** — contato pessoa jurídica do Bling perde
   o documento, e todo contato perde o endereço.
4. **Sem unidade de medida** (UN, KG, CX…) no produto.

Decidir na Fase 0 quais entram antes da migração pela API. As duas primeiras
são pré-requisito da sincronização.

---

## 3. Sincronização (Bling ⇄ Octus)

### O caso que justifica o esforço

A loja vende no balcão pelo Octus **e** em marketplace pelo Bling, com o
**mesmo estoque**. Sem sincronizar, vende no balcão a última unidade que o
Mercado Livre continua anunciando.

### Escopo da primeira versão

| O quê | Direção | Dono da verdade |
|---|---|---|
| Cadastro de produto e preço | Bling → Octus | **Bling** (quem usa Bling cadastra lá) |
| Venda no balcão (PDV, comanda) | Octus → Bling | Octus, como **baixa de estoque** |
| Pedido de marketplace | Bling → Octus | Bling, como **baixa de estoque** |
| Clientes | não sincroniza na v1 | |
| Notas fiscais | não sincroniza | cada sistema emite as suas |

**Estoque trafega como movimento, nunca como saldo.** "Saiu 1 unidade" e não
"o estoque agora é 7". Mandar saldo absoluto nos dois sentidos perde venda
quando as duas pontas vendem ao mesmo tempo — é o mesmo erro de atualização
perdida que o crediário concorrente já ensinou. O Bling v3 aceita lançamento
de entrada e saída em estoque.

### Como funciona

- **Webhooks do Bling** (produto, estoque, pedido) para reagir na hora. A
  documentação avisa que **o mesmo evento pode chegar duas vezes** e que **a
  ordem não é garantida**. Então:
  - cada evento é gravado com o id antes de processar, e repetido é ignorado;
  - o conteúdo do webhook é tratado como aviso: o job busca o estado atual na
    API em vez de confiar no que veio no evento.
- **Conciliação periódica** (a cada poucas horas): compara saldo dos dois lados
  e abre divergência para o lojista ver, em vez de corrigir sozinho. Cobre
  webhook perdido e fila parada.
- **Anti-eco:** baixa que veio do Bling não volta para o Bling. O vínculo
  guarda a origem de cada movimento.
- **Fila por tenant respeitando 3 req/s**, com nova tentativa e espera. Depende
  do worker separado (Hangfire).
- **Token:** o de acesso dura **6 horas** e o de renovação **30 dias**. Um job
  renova antes de vencer. Loja que ficar 30 dias sem renovar (servidor parado,
  token revogado) precisa reconectar: aviso por e-mail e no painel, não falha
  silenciosa.
- **Tela "Bling" em Integrações:** conectado/desconectado, último evento,
  divergências abertas, botão de conciliar agora.

### Riscos

- **Manutenção contínua:** API de terceiro muda (o changelog de webhooks do
  Bling existe por isso). Precisa de dono.
- **Suporte:** divergência de estoque vira chamado. A tela de divergências
  existe para o lojista resolver sem nós.
- **Volume:** 120 mil requisições por dia **por conta** cobrem uma loja com
  folga; conferir na Fase 0 se o limite é por conta do lojista ou por app.

---

## Fases

| Fase | O quê | Depende de | Tamanho (estimativa) |
|---|---|---|---|
| **0** | Criar conta de desenvolvedor e app no Bling; confirmar se app não listado conecta contas de terceiros ou se exige homologação; confirmar limites por conta ou por app; pegar exportações reais; decidir as lacunas | — | dias |
| **1** | Planilha "formato Bling" (2a) | exportação real | dias |
| **2** | Conectar com o Bling + importação pela API (2b), tabela `external_links`, SKU no produto | Fase 0, worker Hangfire | 1 a 2 semanas |
| **3** | Sincronização de estoque e pedidos (v1), beta com 1 ou 2 lojas | Fase 2, variantes genéricas | semanas |

**Recomendação de prioridade:** Fases 0 e 1 já. A Fase 2 quando aparecer o
primeiro lojista vindo do Bling com catálogo grande. A Fase 3 **só com um
lojista pagante pedindo**: o projeto ainda está sem clientes em volume, e
sincronização é o item mais caro de manter desta lista. Se sair, é recurso do
plano Mar ou adicional pago.

## Decisões em aberto

1. Sincronização entra no plano Mar ou é adicional cobrado à parte?
2. Estoque de vários depósitos no Bling: somar, escolher um, ou o Octus ganha
   depósitos?
3. As lacunas 1 a 4 entram antes da Fase 2 ou só as que a sincronização exige?
4. Quem mantém a integração quando a API do Bling mudar?

## Fontes

- [API do Bling — Limites](https://developer.bling.com.br/limites): 3 requisições por segundo e 120 mil por dia.
- [API do Bling — Webhooks](https://developer.bling.com.br/webhooks): eventos de produto, estoque e pedido; entrega repetida possível e sem garantia de ordem.
- [API do Bling — Autenticação](https://developer.bling.com.br/bling-api) e SDKs abertos ([bling-erp-api-js](https://github.com/AlexandreBellas/bling-erp-api-js), [bling_api_v3_oauth](https://github.com/vcsil/bling_api_v3_oauth)): OAuth 2 com código de autorização (vale 1 minuto), token de acesso de 6 horas e de renovação de 30 dias.
- [Bling — Planos e preços](https://www.bling.com.br/planos-e-precos) e [alteração de planos de abril de 2026](https://ajuda.bling.com.br/hc/pt-br/articles/30224184866583-Altera%C3%A7%C3%A3o-nos-planos-e-pre%C3%A7os-do-Bling-em-abril-de-2026): valores exatos a confirmar.
- Octus: `frontend/lib/planos.ts`, `ImportController.cs`, `IntegrationConfig.cs`, `Product.cs`, `ProductVariant.cs`, `User.cs`.
