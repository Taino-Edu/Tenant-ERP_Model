# KYC — Verificação de Maioridade para o Marketplace

**Status:** Esqueleto criado, implementação PENDENTE  
**Decisão pendente com:** Maikon  
**Contexto:** Marketplace de cartas entre usuários. Menores de 16 são absolutamente
incapazes de contratar (Código Civil art. 3º); 16-18 relativamente incapazes (art. 4º, I).
O ECA reforça proteção a menores em transações comerciais.

---

## Arquivos criados (esqueleto)

| Arquivo | O que é |
|---|---|
| `CardGameStore/Models/PostgreSQL/KycVerification.cs` | Modelo da tabela `kyc_verifications` |
| `CardGameStore/Services/Interfaces/IKycService.cs` | Interface do serviço (3 métodos de verificação) |
| `CardGameStore/Controllers/KycController.cs` | Controller com rotas definidas, lógica TODO |

**Falta criar (quando implementar):**
- `CardGameStore/Services/KycService.cs` — implementação concreta
- `frontend/app/cliente/verificacao/page.tsx` — tela do usuário
- `frontend/app/admin/kyc/page.tsx` — painel admin (lista + override manual)
- DDL no `Program.cs` para criar a tabela `kyc_verifications`
- Registrar `IKycService` no DI em `Program.cs`
- Adicionar `CanAccessMarketplaceAsync` como guard no `MarketplaceController`

---

## Opções de implementação — DECIDIR COM MAIKON

### Opção 1 — Autodeclaração (mais simples, zero custo)
- Usuário informa data de nascimento no perfil
- Sistema calcula a idade e bloqueia se < 18
- **Prós:** grátis, instantâneo, sem integração externa
- **Contras:** pessoa pode mentir a data → proteção fraca
- **Recomendado para:** loja local onde o dono conhece os clientes

### Opção 2 — CPF + BrasilAPI (recomendado)
- Usuário informa CPF e data de nascimento
- Sistema consulta `https://brasilapi.com.br/api/cpf/v1/{cpf}`
- Compara nome + data de nascimento retornados
- Se bater → aprova; se não bater → rejeita
- **Prós:** grátis, sem cadastro, valida que a data não foi inventada
- **Contras:** BrasilAPI pode ter instabilidade; dados da Receita têm delay de atualização
- **Recomendado para:** uso geral

### Opção 3 — Documento com foto (KYC completo, fase 3)
- Usuário envia foto do RG/CNH + selfie
- Processado por serviço externo (Idwall, Unico Check, Truora)
- **Prós:** verificação sólida, irrefutável
- **Contras:** custo por verificação (R$ 2–8), integração complexa, LGPD pesada
- **Recomendado para:** quando o volume de fraudes justificar o custo

---

## Perguntas para discutir com Maikon

1. **Qual opção de verificação implementar?** (1, 2 ou uma combinação)
2. **Verificação é obrigatória para todos?** Ou só para quem tentar anunciar/comprar no Marketplace?
3. **Prazo de validade da verificação?** Sugestão: 1 ano (usuário revalida anualmente)
4. **O que fazer com usuários já cadastrados?** Bloqueio gradual ou imediato?
5. **Admin pode aprovar manualmente?** (ex: menor atendido na loja com responsável presente)
6. **Conta de menor com tutela parental?** Quer suportar isso ou bloquear simplesmente todos < 18?
7. **BrasilAPI aceita os CPFs dos clientes atuais?** Alguns CPFs antigos podem ter dados desatualizados

---

## Fluxo proposto (Opção 2 — CPF)

```
Usuário clica "Anunciar" ou "Marcar interesse" no Marketplace
        ↓
GET /api/kyc/status
        ↓
status = "none" ou "expired"?
        ↓ sim
Exibe tela /cliente/verificacao
        ↓
Usuário informa CPF + data nascimento
        ↓
POST /api/kyc/cpf
        ↓
Backend chama BrasilAPI → compara dados
        ↓
idade < 18? → status = "rejected" → bloquear com mensagem legal
idade ≥ 18? → status = "approved" → liberar Marketplace
```

---

## Notas técnicas para quando implementar

- **Nunca armazenar CPF em texto puro** → hash SHA-256 + salt (igual ao IP nos logs de auditoria)
- **BrasilAPI endpoint:** `GET https://brasilapi.com.br/api/cpf/v1/{cpf}` — não requer autenticação
- **LGPD:** verificação é tratamento de dado sensível (dado biométrico indiretamente) → precisa
  de registro no ROPA e cláusula na política de privacidade
- **Tabela:** ver `KycVerification.cs` — DDL deve ser adicionado no bloco `ExecuteSqlRawAsync`
  do `Program.cs` com `IF NOT EXISTS` (padrão do projeto)
- **DI:** registrar `services.AddScoped<IKycService, KycService>()` em `Program.cs`
- **Guard no Marketplace:** antes de criar listagem ou marcar interesse, chamar
  `await _kyc.CanAccessMarketplaceAsync(userId)` → retorna 403 se não verificado

---

## Proteção atual (temporária, enquanto KYC não é implementado)

No modal de interesse e no modal de anúncio há um **checkbox de autodeclaração** que o usuário
deve marcar antes de prosseguir. Isso é a Opção 1 de forma simplificada — sem persistência no banco.
Serve como proteção mínima até o KYC real ser implementado.
