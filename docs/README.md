# Documentacao do Tenant-ERP

Este diretorio concentra a documentacao duravel do projeto. Arquivos que precisam
ficar ao lado do codigo que descrevem, como os READMEs de testes, schemas e deploy,
continuam em seus respectivos diretorios.

## Referencias principais

- [Arquitetura e fluxos](arquitetura/DOCUMENTACAO-COMPLETA.md)
- [Modelagem de dados](arquitetura/MODELAGEM-DE-DADOS.md)
- [Integracao REST multi-tenant](arquitetura/INTEGRACAO-API-MULTITENANT.md)
- [Casos de uso](produto/CASOS-DE-USO.md)
- [Guia de testes](testes/GUIA-DE-TESTES.md)
- [Status executivo](planejamento/STATUS.md)
- [Backlog](planejamento/BACKLOG.md)

## Categorias

- `arquitetura/`: arquitetura, modelagem e estudos tecnicos.
- `auditorias/`: auditorias de carga, escala e seguranca operacional.
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
