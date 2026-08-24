# Gate de go-live fiscal — 25/07/2026

Este roteiro é bloqueante. O ambiente só deve mudar para **Produção** depois que
todos os itens de homologação estiverem registrados com data, responsável, número,
chave e protocolo. Nunca anotar senha do A1 ou token CSC neste arquivo.

## Antes do teste

- [ ] Backup do PostgreSQL concluído e restauração testada.
- [ ] Migration `20260721152500_HardenFiscalDocuments` aplicada em staging.
- [ ] Migrations `20260721202936_AddConfigurableTenantTaxProfiles` e
      `20260722114021_AddCestAndTaxTransparency` e
      `20260722120251_AddIbptAutomaticTaxFill` aplicadas em todos os schemas ativos.
- [ ] Tenant correto com módulo `fiscal` habilitado.
- [ ] CNPJ atual convertido pelo adaptador SEFAZ para 14 dígitos.
- [ ] Razão social, IE, endereço, CEP, município/IBGE e UF conferidos.
- [ ] Regime tributário confirmado como Simples Nacional.
- [ ] Certificado A1 válido, com chave privada e cadeia aceita no servidor Linux.
- [ ] CSC de homologação configurado e armazenado criptografado.
- [ ] Série e próximo número definidos pelo contador; não copiar produção às cegas.
- [ ] Contador confirmou quais produtos/operações usam CSOSN 201/202/203 e forneceu
      modalidade BC-ST, MVA, redução, alíquota e FCP aplicáveis; XML homologado confere
      base e valor de ICMS-ST antes da liberação em produção.
- [ ] Natureza padrão e NCM de todos os produtos do cenário conferidos.
- [ ] CEST de 7 dígitos conferido em todo produto com CSOSN 201/202/203/500.
- [ ] Empresa cadastrada no De Olho no Imposto/IBPT e token próprio da loja salvo na
      configuração fiscal; nunca registrar o token neste checklist.
- [ ] Sincronização IBPT concluída com versão/vigência atuais, zero produtos pendentes
      ou vencidos e conferência separada de um item nacional e um importado.
- [ ] Overrides manuais de percentuais/fonte, se necessários, documentados e validados
      pelo contador; a API IBPT fornece carga tributária aproximada e não define
      CSOSN/CST, CEST, ICMS-ST ou a tributação real da empresa.
- [ ] **IBS/CBS (Reforma Tributária, NT 2025.002):** implementação 2026 presente no
      motor e suportada pelo pacote `Zeus.Net.NFe.NFCe 2026.6.30.1332`. Confirmar no XML
      homologado `CST=000`, `cClassTrib=000001`, IBS-UF 0,1%, IBS-Mun 0%, CBS 0,9%,
      base líquida após `vDesc` e totalizadores iguais à soma dos itens. O motor bloqueia
      anos posteriores até que as respectivas alíquotas/regras sejam configuradas.

## Cenário obrigatório em homologação

- [ ] Emitir venda simples em dinheiro e obter `cStat=100`.
- [ ] Abrir o QR Code e conferir chave, CNPJ, série, número e valor.
- [ ] Validar o `nfeProc` salvo no validador/XML do contador.
- [ ] Emitir venda com desconto e confirmar `vProd`, `vDesc`, `vNF` e pagamentos.
- [ ] Confirmar `prod/CEST` nos itens sujeitos a ST e ausência da tag nos demais.
- [ ] Confirmar `det/imposto/vTotTrib`, `ICMSTot/vTotTrib`, texto em `infCpl` e valores
      por item/federal/estadual/municipal no cupom impresso.
- [ ] Confirmar que fonte e versão IBPT exibidas no cupom correspondem à sincronização
      vigente e que desconto reduz proporcionalmente a base dos tributos aproximados.
- [ ] Na mesma venda, confirmar que `gIBSCBS/vBC = vProd - vDesc` por item e que não
      ocorre rejeição 1104.
- [ ] Emitir venda com pontos/cashback e confirmar total líquido.
- [ ] Forçar rejeição corrigível, corrigir o cadastro e reprocessar mantendo nNF/cNF.
- [ ] Simular indisponibilidade, gerar contingência tpEmis=9 e conferir QR/chave.
- [ ] Restabelecer rede e confirmar retransmissão da mesma nota.
- [ ] Cancelar uma nota dentro de 30 minutos e obter `cStat=135/136`.
- [ ] Conferir `procEventoNFe`, estoque, pontos/cashback, crediário e alerta de reembolso.
- [ ] Inutilizar uma faixa de teste sem documento válido e guardar protocolo/XML.
- [ ] Exportar XMLs usando início=fim e abrir o ZIP no sistema do contador.

## Evidências

| Cenário | Data/hora | Responsável | Série/número | Chave/protocolo | Resultado |
|---|---|---|---|---|---|
| Autorização simples | | | | | |
| Desconto/pontos | | | | | |
| Rejeição/reprocessamento | | | | | |
| Contingência/retransmissão | | | | | |
| Cancelamento/estorno ERP | | | | | |
| Inutilização | | | | | |
| ZIP contador | | | | | |

## Liberação

- [ ] Nenhuma nota pendente ou contingência antiga no staging.
- [ ] Nenhum `ErpEstornoErro` pendente.
- [ ] Contador aprovou XML, numeração, tributação e série.
- [ ] Backup imediatamente anterior ao deploy confirmado.
- [ ] Responsável de plantão definido para as primeiras vendas de sábado.
- [ ] Mudança para Produção aprovada por: __________________ em ____/____ às ____:____.
