# Documentacao do Tenant-ERP

Este diretorio concentra a documentacao duravel do projeto. Arquivos que precisam
ficar ao lado do codigo que descrevem, como os READMEs de testes, schemas e deploy,
continuam em seus respectivos diretorios.

## Por onde comecar

Leia nesta ordem — os tres primeiros dizem o que e verdade hoje, e cada um
governa um recorte diferente. Fora dessa ordem, e facil decidir com base em
documento vencido.

1. [Status executivo](planejamento/STATUS.md) — estado verificado, o que entrou
   desde a revisao anterior e quais riscos estao abertos. **Comece aqui.**
2. [Backlog operacional](planejamento/BACKLOG.md) — fila continua: CRM,
   prospeccao, dados, QA, UX, infra.
3. [Escopo do rebuild (RB-01 a RB-05)](planejamento/REBUILD-ESCOPO-2026-08.md) —
   pagamentos, pedidos online, multi-CNPJ e comandas. **Manda sobre o backlog
   nesses cinco temas.**
4. [Auditoria de resiliencia e autenticacao](auditorias/AUDITORIA-RESILIENCIA-2026-09-08.md)
   — achados `RES-00x`/`AUTH-00x` com arquivo e linha. Sao os P0 reais.

> Todo documento de planejamento carrega a data da ultima revisao no cabecalho.
> Se estiver com mais de um mes, confira contra o codigo antes de decidir.

## Referencias principais

- [Arquitetura e fluxos](arquitetura/DOCUMENTACAO-COMPLETA.md)
- [Modelagem de dados](arquitetura/MODELAGEM-DE-DADOS.md)
- [Integracao REST multi-tenant](arquitetura/INTEGRACAO-API-MULTITENANT.md)
- [Casos de uso](produto/CASOS-DE-USO.md)
- [Guia de testes](testes/GUIA-DE-TESTES.md)
- [Google: sitemap enviado, alerta de segurança e próximas etapas](operacao/INDEXACAO-CHECKLIST.md)
- [Plano do MVP de Pedidos Online](planejamento/PLANO-MVP-PEDIDOS-ONLINE.md)

## Categorias

- `arquitetura/`: arquitetura, modelagem e estudos tecnicos.
- `auditorias/`: auditorias de carga, escala, seguranca operacional e acessibilidade.
- `fiscal/`: go-live, porte e operacao de NFC-e/NF-e.
- `historico/`: devlogs e registros de implementacoes anteriores.
- `negocio/`: briefings e materiais de produto/marketing.
- `operacao/`: Search Console e rotinas de operacao.
- `planejamento/`: backlog, status e planos ainda consultados.
- `produto/`: casos de uso e comportamento esperado.
- `testes/`: guias e evidencias de QA.

## Modulos exportaveis

Os pacotes de codigo Financeiro e Fiscal sao definidos em `packages/`. Use
`packages/export-module.ps1` para gerar arquivos ZIP reproduziveis sem duplicar
fontes dentro do repositorio.
