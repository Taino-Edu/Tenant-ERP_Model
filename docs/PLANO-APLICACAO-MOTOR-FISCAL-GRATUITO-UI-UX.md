# Plano de aplicação — motor fiscal gratuito e UI/UX prática

## 1. Objetivo

Entregar um módulo fiscal simples para o lojista operar, seguro para o contador
validar e sustentável sem licenças pagas por emissão.

O projeto já possui a base técnica principal: emissão NFC-e com Zeus/DFe.NET,
comunicação direta com a SEFAZ, certificado A1, CSC, configuração por tenant,
regras tributárias por natureza/produto, integração IBPT, contingência, histórico,
cancelamento, inutilização e exportação de XML. O plano deve evoluir essa base, não
substituí-la.

> “Gratuito” significa sem licença de motor fiscal e sem intermediador cobrado por
> nota. Continuam existindo custos externos inevitáveis, como certificado A1,
> hospedagem, banco, domínio e suporte contábil.

## 2. Princípios do produto

1. **Venda nunca depende de uma tela fiscal complexa.** O PDV conclui a venda e a
   emissão ocorre no mesmo fluxo, com retorno claro de autorizada, pendente ou
   rejeitada.
2. **Sem adivinhação tributária.** Regra ausente bloqueia somente a emissão e mostra
   exatamente o que falta.
3. **Configuração progressiva.** Primeiro dados da empresa; depois certificado;
   regras; produtos; homologação; produção.
4. **Linguagem de negócio primeiro.** Siglas como CSC, CFOP e CSOSN aparecem com
   ajuda contextual e exemplos, sem esconder o valor técnico.
5. **Contador participa da validação.** O sistema prepara e verifica; o contador
   aprova a regra fiscal aplicável à empresa.
6. **Multi-tenant por desenho.** Credenciais, numeração, regras, filas e falhas são
   isoladas por loja.

## 3. Arquitetura gratuita recomendada

| Camada | Solução | Custo de licença |
|---|---|---:|
| Frontend | Next.js + Tailwind + componentes atuais | R$ 0 |
| API | ASP.NET Core | R$ 0 |
| Banco | PostgreSQL por schemas de tenant | R$ 0 |
| Emissão/assinatura | Zeus/DFe.NET já integrado | R$ 0 |
| Comunicação | Webservices oficiais da SEFAZ | R$ 0 por nota |
| Transparência tributária | API IBPT já integrada, conforme acesso do contribuinte | validar cadastro |
| Jobs | `BackgroundService` + fila persistida no PostgreSQL | R$ 0 |
| Observabilidade inicial | logs estruturados + health checks + painel interno | R$ 0 |
| Testes | xUnit + Playwright | R$ 0 |

Não adotar inicialmente Focus NFe, TecnoSpeed ou outro gateway pago. Manter uma
interface de provedor para permitir migração futura se o volume ou o custo de suporte
operacional justificarem.

## 4. Nova estrutura da experiência

A página atual `/admin/fiscal` concentra configuração, certificado, IBPT, naturezas,
notas, inutilização, contador e avisos. Ela deve virar um módulo com navegação própria:

```text
Fiscal
├── Visão geral
├── Configuração guiada
│   ├── Empresa
│   ├── Certificado e CSC
│   ├── Regras fiscais
│   ├── Produtos
│   └── Homologação e produção
├── Documentos fiscais
├── Pendências
├── Exportação para contador
└── Contador e acessos
```

### 4.1 Visão geral

Uma tela operacional, não um formulário. Deve responder em poucos segundos:

- o fiscal está pronto para emitir?;
- há notas pendentes ou rejeitadas?;
- o certificado está válido?;
- existem produtos sem configuração?;
- o ambiente é homologação ou produção?;
- qual é a próxima ação necessária?

Componentes:

- indicador principal: **Pronto para emitir**, **Requer atenção** ou **Bloqueado**;
- checklist de ativação com progresso real;
- cartões de autorizadas, rejeitadas e pendentes nas últimas 24 horas;
- alerta do certificado e da sincronização IBPT;
- lista curta de pendências, ordenada por impacto;
- botão primário dinâmico: “Continuar configuração”, “Corrigir 3 produtos” ou
  “Reprocessar notas”.

### 4.2 Configuração guiada

Usar um wizard de cinco etapas com salvamento automático e possibilidade de sair e
continuar depois.

1. **Empresa:** CNPJ, razão social, IE, endereço e regime; aplicar máscaras e validar
   enquanto digita.
2. **Certificado e CSC:** upload do A1, senha com exibição opcional, validade lida antes
   de salvar e explicação de onde obter o CSC.
3. **Regras fiscais:** presets apenas como rascunho; contador confirma CFOP, CSOSN,
   ICMS-ST e IBS/CBS. Campos avançados ficam recolhidos.
4. **Produtos:** tabela com filtros “pronto”, “incompleto” e “vencido”; correção em
   lote quando juridicamente segura; link direto para o produto problemático.
5. **Teste:** pré-voo, emissão em homologação, resultado explicado e checklist para
   ativar produção.

Cada etapa deve exibir: estado, campos pendentes, motivo da exigência e ação seguinte.

### 4.3 Documentos fiscais

- busca por número, chave, cliente ou origem;
- filtros visíveis por período e status;
- status com texto e ícone, nunca apenas cor;
- painel lateral de detalhes sem perder o filtro atual;
- ações contextuais: ver cupom, baixar XML, imprimir, cancelar ou reprocessar;
- rejeição traduzida para mensagem prática, preservando `cStat` e mensagem original
  em “Detalhes técnicos”.

### 4.4 Central de pendências

Agrupar problemas pelo que o usuário consegue resolver:

- **Configuração da loja:** certificado, CSC, ambiente ou endereço;
- **Cadastro de produto:** NCM, CEST ou tributos;
- **Regra fiscal:** natureza/CFOP/CSOSN sem cobertura;
- **Comunicação:** SEFAZ indisponível ou nota em contingência;
- **Ação do contador:** regra que exige validação profissional.

Cada item deve conter impacto, causa, botão de correção e indicação se a venda foi
preservada.

## 5. Fluxo ideal no PDV

1. Operador fecha a venda.
2. Checkbox “Emitir NFC-e” usa a preferência configurada, mas continua visível.
3. A venda é confirmada uma única vez.
4. O sistema apresenta um estado curto:
   - **Autorizada:** abrir/imprimir cupom;
   - **Processando:** venda concluída, emissão acompanhada em segundo plano;
   - **Contingência:** cupom disponível e retransmissão automática;
   - **Não emitida:** venda concluída, correção disponível no Fiscal.
5. O operador não recebe formulário tributário no balcão.

## 6. Componentes de UI reutilizáveis

- `FiscalHealthCard`: estado geral e ação recomendada;
- `FiscalSetupStepper`: progresso das cinco etapas;
- `FiscalIssueCard`: problema, impacto, responsável e ação;
- `DocumentStatusBadge`: status acessível com ícone/texto;
- `SefazResultPanel`: mensagem amigável + detalhes técnicos;
- `CertificateDropzone`: upload, leitura de validade e troca segura;
- `ProductFiscalReadinessTable`: filtros e correção direcionada;
- `EnvironmentBanner`: faixa persistente quando estiver em homologação;
- `DangerConfirmDialog`: confirmação digitada para produção, cancelamento e
  inutilização.

Requisitos de UX: responsivo, teclado, foco visível, contraste AA, mensagens sem
depender só de cor, skeletons no carregamento e preservação de filtros ao voltar.

## 7. Plano de implementação

### Fase 0 — segurança e homologação (obrigatória)

- concluir o checklist de `docs/GO-LIVE-FISCAL-2026-07-25.md`;
- homologar com certificado/CSC reais de teste e contador;
- validar autorização, rejeição, contingência, retransmissão, cancelamento,
  inutilização, QR Code, XML e numeração;
- impedir produção enquanto o pré-voo não estiver aprovado.

**Saída:** motor tecnicamente liberado para uso controlado.

### Fase 1 — fundação da nova UX

- quebrar `/admin/fiscal` em rotas e componentes menores;
- criar endpoint agregador de saúde fiscal;
- implementar visão geral, navegação fiscal e banner de ambiente;
- manter compatibilidade com os endpoints existentes.

**Saída:** usuário encontra o estado fiscal e a próxima ação em até 30 segundos.

### Fase 2 — onboarding guiado

- implementar o wizard com progresso persistido calculado pelos dados reais;
- adicionar pré-voo sem comunicação externa para detectar configuração incompleta;
- criar validações inline, ajuda contextual e campos avançados recolhidos;
- incluir teste de homologação e gate explícito de produção.

**Saída:** uma nova loja consegue preparar o módulo sem navegar por uma tela longa.

### Fase 3 — operação e diagnóstico

- criar central de pendências;
- normalizar erros da SEFAZ em categorias acionáveis;
- melhorar histórico com busca, filtros e painel de detalhes;
- criar fila persistida, idempotência e rastreio de tentativas por nota;
- expor métricas de pendência, rejeição, contingência e tempo de autorização.

**Saída:** falhas são corrigidas sem depender de leitura de logs.

### Fase 4 — contador e escala

- oferecer pacote mensal de XML e resumo fiscal em um único fluxo;
- registrar confirmação do contador sobre regras críticas;
- permitir importação/exportação de regras fiscais por loja com validação;
- documentar inclusão de novos provedores/regimes sem constantes globais.

**Saída:** manutenção fiscal compartilhada e auditável.

## 8. Priorização do MVP

### P0 — antes de produção

- homologação SEFAZ completa;
- pré-voo e gate de produção;
- certificado/CSC com diagnóstico;
- fila idempotente e contingência verificadas;
- isolamento por tenant testado;
- mensagens acionáveis para falhas críticas.

### P1 — primeira experiência pública

- visão geral;
- wizard;
- central de pendências;
- histórico aprimorado;
- fluxo simples no PDV;
- exportação para contador.

### P2 — evolução

- edição segura em lote;
- confirmação formal de regras pelo contador;
- métricas e alertas avançados;
- provedores fiscais adicionais plugáveis;
- suporte a novos regimes somente após matriz de cobertura e homologação.

## 9. Critérios de aceite

- nenhum tenant acessa certificado, configuração, XML ou fila de outro tenant;
- uma falha fiscal nunca desfaz silenciosamente uma venda concluída;
- reprocessar não cria nota duplicada nem consome nova numeração indevidamente;
- produção não é habilitada sem certificado, CSC, emitente, regra padrão, produtos do
  cenário e emissão de homologação validados;
- todo erro mostra uma ação sugerida e conserva o retorno técnico original;
- operador emite uma NFC-e sem preencher campos fiscais no PDV;
- contador exporta os XMLs de um período em até três interações;
- principais fluxos possuem testes xUnit e Playwright.

## 10. Indicadores de sucesso

- tempo mediano para configurar uma nova loja;
- percentual de lojas prontas sem suporte manual;
- taxa de autorização na primeira tentativa;
- quantidade e idade máxima de notas pendentes;
- tempo médio de resolução de rejeições;
- percentual de produtos fiscalmente completos;
- incidentes de duplicidade, perda de numeração ou isolamento: meta zero;
- chamados de suporte por 1.000 emissões.

## 11. Primeira entrega recomendada

Executar um sprint curto com quatro entregas conectadas:

1. endpoint de saúde fiscal agregando configuração, certificado, IBPT, produtos e
   notas pendentes;
2. nova tela “Visão geral” com checklist e próxima ação;
3. wizard reutilizando os formulários e endpoints atuais;
4. pré-voo e teste Playwright do caminho homologação → emissão → cupom.

Essa sequência melhora a experiência sem reescrever o motor já existente e cria a
base para retirar, progressivamente, a página fiscal monolítica.
