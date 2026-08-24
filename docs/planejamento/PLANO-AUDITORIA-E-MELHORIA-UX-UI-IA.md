# 🚀 Plano Oficial de Evolução: Auditoria de Marca (3E Systen), Padronização UX/UI & IA-tização

> **Repositório:** Tenant-ERP (`Tenant-ERP_Model`)
> **Data:** 2026-07-24
> **Objetivo:** Estabelecer o plano de ação para auditoria de domínios/marcas, aprimoramento de UX/UI mantendo 100% da identidade visual configurada no tenant, padronização de componentes e introdução de recursos inteligentes (IA).

---

## 📌 1. Escopo e Diretrizes Principais

### 1.1 Preservação Estrita da Identidade Visual
- **Zero Redesign do Zero:** A marca, paleta de cores e identidade visual existentes de cada tenant serão **100% respeitadas**.
- **Tokens de Cores do Tenant (`siteConfig`):** Manutenção integral de `site.colorPrimary`, `site.colorAccent`, `site.colorNavy`, `site.colorCard`, `site.colorBackground` e dos utilitários de contraste (`mixHex`, `getContrastText`).
- **Foco do Rework:** Refinamento de bordas, sombras sutis de profundidade (`shadow-sm`, `shadow-md`), estados de hover responsivos, micro-interações fluidas em 200ms e melhoria na hierarquia tipográfica.

### 1.2 Auditoria de Marca & Domínio (3E Systen)
- Transição global do antigo domínio/marca (`2esysten` / `2esysten.com.br`) para **`3E Systen`** e **`3esysten.com.br`**.
- Atualização de Nginx, middlewares de multitenancy, suíte de testes unitários em C# e documentações oficiais.
- Generalização de exemplos de nicho ("loja de card games") para a proposta abrangente de **ERP para Varejo & Serviços**.

---

## 🎨 2. Detalhamento de Componentes & UX/UI (`frontend/app/page.tsx`)

### 2.1 Padronização de Componentes
- **Cards de Produtos:**
  - Aplicação de cantos arredondados padronizados (`rounded-2xl`).
  - Iluminação sutil no hover (`hover:border-opacity-30`, transição de elevação `hover:-translate-y-1`).
  - Badges de estoque e descontos com indicadores táteis claros.
  - Botão de ação "Adicionar ao Carrinho" com estados de *loading* e confirmação visual.
- **Banners & Carrossel Hero:**
  - Transição de slides com fade/slide suave.
  - Indicadores de paginação e botões de pausa no hover refinados.
- **Modais de Detalhe de Produto & Anúncios:**
  - Desfoque de fundo (*backdrop blur*), fechamento com esc para acessibilidade e botão de suporte rápido via WhatsApp formatado.

### 2.2 IA-tização e Recursos Inteligentes (Smart Features)
- **Barra de Busca Inteligente Assistida por IA:**
  - Busca preditiva em tempo real com sugestão de tags e categorias populares.
- **Badge de Destaque IA:**
  - Selo inteligente para produtos com alta rotatividade ou recomendados pela IA da plataforma.
- **Micro-interações Cativantes:**
  - Contador de itens no carrinho animado com efeito de *pulse*.
  - Alternância de tema Dark/Light com transição suave e memorização em `localStorage`.

---

## 🛠️ 3. Mapeamento de Arquivos Afetados

### 3.1 Frontend Next.js (`/frontend`)
- `frontend/app/page.tsx` (Vitrine principal da loja)
- `frontend/app/institucional/layout.tsx` (Layout da marca)
- `frontend/app/institucional/page.tsx` (Landing page institucional)
- `frontend/components/plataforma/CreateTenantModal.tsx` (Modal de criação de tenant)
- `frontend/app/plataforma/tenants/[id]/page.tsx` (Painel de gerenciamento de tenant)
- `frontend/middleware.ts` (Middleware de roteamento por domínio)

### 3.2 Backend ASP.NET Core (`/CardGameStore`)
- `CardGameStore/Multitenancy/ITenantProvisioningService.cs`
- `CardGameStore/Multitenancy/TenantResolutionMiddleware.cs`
- `tests/unit/CardGameStore.Tests/Controllers/PlatformControllerDomainTests.cs`
- `tests/unit/CardGameStore.Tests/Multitenancy/TenantResolutionMiddlewareTests.cs`

### 3.3 Infraestrutura & Documentação
- `deploy/nginx/nginx.conf`
- `README.md`
- `docs/arquitetura/DOCUMENTACAO-COMPLETA.md`
- `docs/planejamento/STATUS.md`

---

## 🧪 4. Plano de Validação e Testes

1. **Testes Unitários de Backend:**
   ```bash
   dotnet test tests/unit/CardGameStore.Tests/CardGameStore.Tests.csproj
   ```
2. **Compilação e Linting do Frontend:**
   ```bash
   cd frontend && npm run build
   ```
3. **Validação de Responsividade:**
   - Verificação visual em resoluções Mobile (375px), Tablet (768px) e Desktop (1440px).
