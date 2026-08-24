# PLANO DE EXPANSÃO: ARQUITETURA DE DADOS, IA, CRM & MONETIZAÇÃO (Tenant-ERP / 2esysten)

> **Documento de Orientação Técnica e Arquitetural para Desenvolvedores e Agentes de IA**
> **Repositório:** `Tenant-ERP_Model`
> **Data:** 2026-07-22
> **Versão:** 2.0 (Re-análise Consolidada & Pragmática)

---

## 🎯 Executive Summary & Veredito Consolidado

A visão de produto para transformar o **Tenant-ERP** em uma plataforma SaaS de alta margem (**R$ 489,00/mês**) é altamente viável e promissora. Contudo, para garantir um lançamento seguro e lucrativo, o roadmap técnico foi ajustado para eliminar complexidades prematuras e focar na estabilidade fiscal e operacional.

### Princípios Inegociáveis
1. **Go-Live Fiscal Primeiro (P0):** A emissão de notas fiscais, vendas no balcão (PDV) e comandas têm prioridade absoluta. Nenhuma funcionalidade de IA ou analytics pode bloquear o caminho crítico de vendas do lojista.
2. **Uso dos Componentes Existentes:** O CRM de prospecção expandirá a estrutura já funcional em `/plataforma/leads`, sem criar telas ou tabelas duplicadas.
3. **Isolamento e Falha Graciosa de IA:** Se a API de IA ficar indisponível ou estourar a cota, a loja continua operando 100% normalmente.
4. **Governança de Custos de IA:** Toda chamada de IA é controlada via gateway interno (`ITenantAiGateway`) e registrada em tabela auditável (`AiUsageLedger`) no PostgreSQL.

---

## 🚀 1. Modelo de Negócios & Viabilidade Financeira

### 1.1 Estrutura de Precificação
- **Mensalidade Recorrente:** **R$ 489,00 / mês**
- **Taxa de Setup / Implantação:** **R$ 978,00** (2 mensalidades, com isenção do 1º mês de uso)
- **Público-Alvo:** PMEs do Varejo (lojas físicas, e-commerce, comércios locais).
- **Custo Operacional Meta por Tenant:** **< R$ 25,00 / mês** (Infraestrutura + APIs + IA).
- **Margem Bruta Alvo por Tenant:** **~ 88% (~ R$ 431,00/mês de lucro líquido por cliente)**.

---

## 🛡️ 2. Arquitetura de IA Segura & Governança de Custos

```
[Requisição HTTP no Chat do Admin ou Job Async]
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│ ITenantAiGateway (Resolve Tenant via Token JWT / Contexto)  │
└─────────────────────────────────────────────────────────────┘
                        │
      ┌─────────────────┴─────────────────┐
      ▼                                   ▼
┌───────────────────────────────┐   ┌─────────────────────────────┐
│ 1. Checa TenantAiPolicy       │   │ 2. Checa Cache Analítico    │
│    (Cota diária & modelo)     │   │    (PostgreSQL / Memória)   │
└───────────────────────────────┘   └─────────────────────────────┘
      │                                   │
      └─────────────────┬─────────────────┘
                        │ (Se autorizado & sem cache)
                        ▼
┌─────────────────────────────────────────────────────────────┐
│ Chamada HTTP para API Gemini 2.5 / 3.6 Flash                │
└─────────────────────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│ Gravacão Auditável em `AiUsageLedger` (Tokens & Custo BRL)  │
└─────────────────────────────────────────────────────────────┘
```

### 2.1 Componentes de Governança (`CardGameStore`)
- **`ITenantAiGateway`**: Interface única para acionar LLM. Nenhuma controller chama o Google diretamente.
- **`TenantAiPolicy`**: Entidade no catálogo global (`CatalogDbContext`) definindo cotas (ex: 50 requisições/dia) e modelos permitidos por plano.
- **`AiUsageLedger`**: Tabela de log auditável persistida no PostgreSQL registrando: `TenantId`, `Feature`, `Model`, `InputTokens`, `OutputTokens`, `EstimatedCost`, `Timestamp`.
- **Cache de Resultados Analíticos**: Em vez de cachear texto livre do chat (que tem baixa reusabilidade e risco de PII), o sistema faz cache de **estruturas de dados** (ex: Curva ABC ou resumo financeiro calculado) e só chama o Gemini para a síntese textual quando o cache expirar.

---

## 🤖 3. CRM de Prospecção & Bots de Inteligência de Mercado

O módulo de prospecção expandirá a entidade de `Lead` e a tela `/plataforma/leads` existentes no repositório.

### 3.1 Evolução da Entidade `Lead` (`CatalogDbContext`)
- Adição de campos: `Source` (GoogleMaps, Manual, Indicação), `DigitalPresence` (SemSite, SiteLegado, ECommerce), `OpportunityScore` (0 a 100), `PlaceId`, `Notes` e histórico de interações.

### 3.2 Scouting Bot (`Services/MarketScoutingService.cs`)
1. **Busca Local:** Utiliza Google Places API (aproveitando o crédito grátis mensal de **US$ 200,00 da conta GCP**) ou OpenStreetMap.
2. **Scanner HTTP em C#:** Faz checagem rápida no domínio do comércio para verificar presença de site e e-commerce.
3. **Score de Oportunidade:** Pontuação transparente baseada em:
   $$\text{Score} = (\text{Volume de Avaliações}) \times (\text{Fator de Categoria}) + (\text{Peso "Sem Site"})$$
4. **Alimentação do CRM:** Insere o lead diretamente em `/plataforma/leads` com deduplicação por telefone, e-mail e `place_id`.

---

## 📊 4. Estrutura de Dados & Evolução Gradual

### 4.1 Fase Atual (Outbox + Agregados no PostgreSQL)
- Manter o isolamento rigoroso por schema (`search_path`) no PostgreSQL 16.
- Utilizar **Outbox Pattern** e tabelas agregadas no `CatalogDbContext` para métricas da plataforma.
- **Privacidade LGPD:** Aplicar HMAC com chave secreta rotacionável para identificadores estáveis. Nunca enviar PII (CPF, Nome, Telefone) para prompts de IA.

### 4.2 Fase Futura (Medallion Lakehouse)
- A separação em camadas Bronze, Prata e Ouro e o uso de Redis distribuído são diferidos para quando a base atingir centenas de tenants ativos ou volumetria analítica pesada.

---

## 📅 5. Roadmap Executivo Unificado

```
[FASE 0: GO-LIVE FISCAL & ESTABILIDADE (AGORA)]
 ├── Homologação fiscal (NFC-e/NF-e com contador/SEFAZ)
 ├── Testes de backup/restauração e deploy VPS
 └── Validação da operação do PDV e Comandas
        │
        ▼
[FASE 1: GOVERNAÇA DE IA & CRM EXPANDIDO (0 a 30 DIAS)]
 ├── Gateway de IA (ITenantAiGateway + AiUsageLedger)
 ├── Limite de cota diária por tenant e fallback gracioso
 └── Evolução da tela /plataforma/leads existente
        │
        ▼
[FASE 2: PROSPECÇÃO AUTOMATIZADA & ANALYTICS (30 a 90 DIAS)]
 ├── Scouting Bot (Google Places API + Scanner HTTP C#)
 ├── Pontuação de Oportunidade de Leads
 └── Tabelas agregadas de saúde do tenant e churn risk
        │
        ▼
[FASE 3: ESCALA & AD-DONS DE IA (APÓS VALIDAÇÃO DE TRAÇÃO)]
 ├── Módulo Add-on "Radar de Tendências do Setor"
 └── Infraestrutura analítica avançada quando justificada por volume
```

---

*Documento consolidado após re-análise técnica e estratégica do repositório Tenant-ERP.*
