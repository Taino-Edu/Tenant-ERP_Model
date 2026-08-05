# Plano compartilhado — NFC-e pronta para produção

Data-base da revisão: **04/08/2026**
Escopo inicial: **loja física, venda presencial, operação interna e optante pelo Simples Nacional**.

> 📌 **Status de execução:** o progresso de cada cartão (feito / parcial / pendente)
> vive em um painel à parte — [`STATUS-GO-LIVE-NFCE.md`](STATUS-GO-LIVE-NFCE.md).
> Este plano permanece intacto como fonte da verdade sobre escopo e critérios; o
> painel é só o retrato do que já foi entregue. As seções 27 a 29 deste documento
> registram, em detalhe, o que foi concluído em código.

Este documento organiza a passagem da NFC-e para produção sem transformar o sistema em
um conjunto de bloqueios desnecessários. Quando for seguro continuar a venda, o sistema deve
continuar e deixar a pendência explícita, rastreável e conciliável. Regras que afetam a validade
do documento fiscal, porém, precisam produzir XML correto — aviso não corrige documento
tributariamente incorreto.

## 1. Resultado esperado

Ao final deste plano, o sistema deverá:

1. emitir NFC-e com pagamentos, tributos e identificação compatíveis com o cenário da venda;
2. nunca presumir que uma transmissão falhou sem consultar se a SEFAZ autorizou a chave;
3. imprimir ou exibir um DANFE NFC-e derivado do XML fiscal, conforme o manual vigente;
4. mostrar toda venda que ficou sem documento, rejeitada ou em contingência;
5. preservar XML, protocolo, eventos e evidências de forma recuperável;
6. informar claramente quais cenários estão certificados: Simples/presencial/interno primeiro;
7. permitir evolução de regras por configuração/tabelas versionadas, evitando datas-limite
   espalhadas pelo código.

## 2. Como Claude e Codex devem dividir o trabalho

- Cada cartão abaixo deve ter **um implementador e um revisor diferente**.
- Antes de começar, preencher `Responsável` e mudar o status para `Em andamento`.
- Não trabalhar simultaneamente no mesmo arquivo central. Em especial,
  `NfceEmissionService.cs` deve ter um responsável por vez.
- Fazer commits pequenos por cartão, sem misturar mudanças fiscais com UI ou refatorações gerais.
- Não usar `git reset`, `checkout --` ou limpeza de arquivos para resolver conflito: o worktree já
  contém trabalho em andamento que precisa ser preservado.
- Toda decisão fiscal fornecida pelo contador deve ser registrada em documento, com data,
  empresa/UF e responsável. Não guardar certificado, senha ou token no repositório.

Status permitidos: `Pendente`, `Em andamento`, `Em revisão`, `Homologado`, `Adiado`.

## 3. Quadro geral

| ID | Frente | Prioridade | Dependências | Status | Responsável | Revisor |
|---|---|---:|---|---|---|---|
| FIS-001 | Confirmar escopo fiscal da primeira loja | P0 | — | Pendente | — | — |
| FIS-002 | Corrigir códigos de pagamento | P0 | FIS-001 | Pendente | — | — |
| FIS-003 | Decidir tratamento fiscal dos pontos pré-aplicados | P0 | FIS-001 | Pendente | — | — |
| RES-001 | Tratar resultado incerto da autorização | P0 | — | Pendente | — | — |
| RES-002 | Persistir documento completo de contingência | P0 | RES-001 | Pendente | — | — |
| DAN-001 | Gerar DANFE a partir do XML fiscal | P0 | RES-002 | Pendente | — | — |
| DAN-002 | Validar impressão oficial e contingência | P0 | DAN-001 | Pendente | — | — |
| CON-001 | Conciliar vendas × documentos fiscais | P0 | — | Pendente | — | — |
| CON-002 | Alertas e painel de pendências | P1 | CON-001 | Pendente | — | — |
| CAD-001 | Auditoria fiscal do catálogo | P0 | FIS-001 | Pendente | — | — |
| XML-001 | Código de produto, GTIN e textos do XML | P1 | CAD-001 | Pendente | — | — |
| XML-002 | Validação XSD anterior à transmissão | P1 | — | Pendente | — | — |
| REG-001 | Corrigir totalizadores do regime normal | P0 para regime normal | FIS-001 | Pendente | — | — |
| RTC-001 | Estratégia IBS/CBS versionada | P0 para regime normal/2027 | FIS-001 | Pendente | — | — |
| UF-001 | Benefícios e regras específicas da UF | P1/condicional | FIS-001, CAD-001 | Pendente | — | — |
| ECOM-001 | Separar emissão presencial de entrega/e-commerce | P0 para e-commerce | FIS-001 | Pendente | — | — |
| OPS-001 | Credenciamento e configuração de produção | P0 | FIS-001 | Pendente | — | — |
| OPS-002 | Monitoramento, backup e recuperação | P0 | RES-001, CON-001 | Pendente | — | — |
| HOM-001 | Bateria final de homologação | P0 | cartões P0 do escopo | Pendente | — | — |
| PRD-001 | Piloto controlado em produção | P0 | HOM-001, OPS-001, OPS-002 | Pendente | — | — |

## 4. Fase 0 — congelar o cenário que será certificado

### FIS-001 — Confirmação assinada do escopo

Registrar com o contador e com a loja:

- UF, CNPJ, IE, município/IBGE, CNAE e credenciamento NFC-e;
- regime tributário atual e eventual excesso de sublimite;
- venda exclusivamente presencial e interna na primeira liberação;
- mercadorias efetivamente vendidas: cartas, boosters, acessórios, alimentos, bebidas etc.;
- uso de ICMS-ST, FCP, monofásico, redução, isenção ou benefício fiscal;
- natureza jurídica do cashback e dos pontos da loja;
- série e próximo número de produção.

**Aceite:** uma página aprovada pelo contador descreve o cenário. Tudo que estiver fora dela
fica marcado como “ainda não certificado”, sem fingir suporte fiscal.

## 5. Fase 1 — pagamentos e base tributável

### FIS-002 — Meios de pagamento

Implementar e testar:

- crediário próprio → `tPag=05`;
- pontos, cashback e crédito virtual → `tPag=19`;
- `xPag` somente quando o código utilizado realmente exigir descrição, como `99`;
- pagamento dividido com valores cuja soma seja exatamente o total da NFC-e;
- DANFE mostrando cada meio e seu respectivo valor;
- cartão/Pix integrado e não integrado conforme os dados realmente disponíveis.

Casos mínimos de teste: dinheiro, Pix, crédito, débito, crediário, cashback, pontos e cada
combinação aceita de pagamento dividido.

**Aceite técnico:** o XML serializado contém os códigos e valores esperados e passa no XSD.
**Aceite fiscal:** contador aprova um XML de crediário, um de cashback e um de pontos.

### FIS-003 — Pontos pré-aplicados

Hoje pontos aplicados antes do fechamento reduzem o total e viram desconto, enquanto pontos
selecionados como meio de pagamento preservam o valor cheio. Decidir com o contador se:

- são desconto incondicional e reduzem a base; ou
- são crédito virtual (`tPag=19`) e preservam a base cheia.

Depois da decisão, unificar o comportamento de comanda, venda avulsa, impressão, cancelamento
e estorno. Não deixar dois tratamentos fiscais para o mesmo benefício sem uma razão registrada.

**Aceite:** teste prova a base de ICMS, o total e o pagamento nos dois fluxos de venda.

## 6. Fase 2 — autorização, timeout e contingência

### RES-001 — Estado “resultado incerto”

Não converter automaticamente todo timeout em nova emissão offline. O fluxo deve ser:

1. persistir chave, número, XML assinado e identificador da tentativa antes do envio;
2. transmitir;
3. se a resposta for perdida, marcar resultado incerto;
4. consultar a situação da chave original;
5. se autorizada, recuperar e persistir o protocolo;
6. se rejeitada, registrar a rejeição;
7. somente seguir para a alternativa legal de contingência quando o destino da tentativa
   original estiver resolvido ou o procedimento oficial aplicável permitir.

Também tratar resposta de duplicidade consultando a chave, em vez de transformar uma nota já
autorizada em rejeitada localmente.

Para tornar isso testável, isolar as chamadas da biblioteca SEFAZ atrás de uma interface. Criar
simulações de: timeout antes do envio, timeout depois da autorização, duplicidade, rejeição e
retorno normal.

**Aceite:** no teste “SEFAZ autorizou e a resposta caiu”, termina existindo exatamente uma
NFC-e autorizada, com a mesma chave nos dois lados.

### RES-002 — Documento offline completo

Persistir o XML assinado entregue em contingência, não apenas chave e URL do QR Code. Preservar:

- `dhEmi`, `dhCont`, `xJust`, `tpEmis`, chave e QR Code;
- snapshot fiscal dos itens e pagamentos;
- XML transmitido e, depois, `nfeProc` autorizado;
- histórico das tentativas, sem sobrescrever a evidência anterior.

**Aceite:** reiniciar a aplicação ou perder o banco de cache não altera o documento entregue ao
consumidor; a retransmissão usa exatamente o documento offline original.

## 7. Fase 3 — DANFE NFC-e oficial

### DAN-001 — Fonte única: XML

Substituir o cupom reconstruído a partir do cadastro atual por um renderizador alimentado pelo
XML persistido:

- `nfeProc` quando autorizado;
- XML assinado offline quando em contingência;
- evento de cancelamento para sinalização de documento cancelado.

Admin e cliente devem usar o mesmo componente/base de dados fiscal. Não imprimir documento
rejeitado ou pendente como se fosse DANFE válido.

Incluir ao menos todas as divisões e informações obrigatórias do Manual DANFE NFC-e vigente:
cabeçalho, identificação oficial, detalhes mínimos dos itens, totais, quantidade total,
pagamentos/valores, consulta por chave, QR Code, consumidor, identificação da NFC-e e protocolo.

**Aceite:** mudar razão social, endereço ou produto depois da emissão não muda a reimpressão.

### DAN-002 — Verificação visual e física

- testes de snapshot do conteúdo;
- impressão em 80 mm e largura menor suportada;
- nomes longos, acentos e grande quantidade de itens;
- QR Code lido por dois celulares e consultado no portal autorizador;
- DANFE normal, contingência e cancelado;
- conferência do contador com o Manual versão vigente.

**Aceite:** checklist assinado e amostras em PDF/impressas anexadas à evidência de homologação.

## 8. Fase 4 — conciliação sem bloquear o caixa

### CON-001 — Vendas sem documento não podem ficar invisíveis

Criar uma consulta diária que classifique cada comanda/venda tributável como:

- autorizada;
- autorizada em contingência aguardando transmissão;
- pendente/resultado incerto;
- rejeitada;
- cancelada;
- **sem registro fiscal criado**.

O mecanismo não precisa impedir o fechamento. Precisa registrar claramente a escolha de não
emitir, quem a fez, quando e a justificativa, além de disponibilizar a diferença ao administrador
e ao contador.

**Aceite:** uma venda fechada com a opção fiscal desmarcada aparece no relatório no mesmo dia.

### CON-002 — Alertas operacionais

Alertar por severidade e idade:

- resultado incerto: imediato;
- contingência: acompanhamento contínuo até autorização;
- venda sem documento: fechamento diário;
- rejeição: motivo e ação sugerida;
- lacuna de numeração: relatório para decisão de reuso/correção ou inutilização;
- falha de exportação/backup: imediata.

Alertas devem ter deduplicação, responsável e confirmação de resolução.

## 9. Fase 5 — catálogo e conteúdo do XML

### CAD-001 — Saneamento fiscal antes do piloto

Exportar todos os produtos ativos e fazer o contador validar:

- NCM vigente;
- CEST quando aplicável e correlação com NCM/segmento;
- CFOP;
- CSOSN/CST;
- origem da mercadoria;
- ICMS-ST, MVA, FCP e retenção anterior quando aplicáveis;
- PIS/COFINS quando fora do Simples;
- benefícios/desoneração;
- classificação IBS/CBS aplicável;
- percentuais e vigência da transparência tributária.

O sistema pode mostrar pendências e inconsistências sem inventar classificação fiscal.

**Aceite:** zero produto do piloto sem aprovação; arquivo aprovado guardado com versão e data.

### XML-001 — Identificação real dos itens

- usar SKU/código interno estável em `cProd`, nunca a posição do item na nota;
- informar GTIN válido quando o produto realmente possuir GTIN; usar `SEM GTIN` somente quando
  aplicável;
- respeitar limites e caracteres permitidos de `xProd` e demais textos;
- preservar unidade comercial/tributável real quando o catálogo deixar de trabalhar apenas
  com unidades inteiras;
- testar o nome real do primeiro item também, pois homologação o substitui pela frase obrigatória.

### XML-002 — XSD e regras anteriores à reserva definitiva

Ativar validação local com o pacote de schemas vigente e versionado. Executar a validação antes
da transmissão e, sempre que possível, antes de consumir numeração. Manter a resposta da SEFAZ
como autoridade final; a validação local serve para reduzir erros evitáveis.

**Aceite:** fixtures válidas passam e XML com total, texto ou grupo inválido falha com mensagem
compreensível e sem ser confundido com indisponibilidade da SEFAZ.

## 10. Fase 6 — regimes e cenários ainda não certificados

### REG-001 — Lucro Presumido e Lucro Real

Antes de anunciar suporte:

- somar `vBC`, `vICMS`, `vICMSDeson`, `vFCP`, `vBCST`, `vST`, `vFCPST`, `vPIS` e `vCOFINS`
  a partir dos itens corretos;
- validar fórmulas de CST 00/10/20/30/40/41/50/60/70/90;
- implementar motivos de desoneração e benefícios quando exigidos;
- conferir inclusão de ST/FCP no total da nota;
- criar XML completo por CST suportado e obter aprovação do contador.

Enquanto não concluído, a interface e a documentação devem dizer “não certificado para emissão”,
sem afirmar que os testes unitários dos itens equivalem a uma NFC-e completa válida.

### RTC-001 — IBS/CBS sem bloqueio fixo por ano

- remover a dependência de uma exceção fixa que derruba toda emissão a partir de 2027;
- representar vigência, CST, `cClassTrib`, alíquotas e indicadores em regras/tabelas versionadas;
- diferenciar Simples, excesso de sublimite, opção pelo regime regular e regime normal;
- registrar a fonte oficial e a versão usada em cada alteração;
- criar testes por data de emissão e regime;
- acompanhar NT, Informe Técnico, schemas e cronograma oficial antes de cada liberação.

**Aceite:** virar a data em teste não causa parada geral; o XML muda somente conforme a regra
versionada aplicável ao contribuinte.

### UF-001 — Regras estaduais

Criar uma matriz por UF para `cBenef`, desoneração, credenciamento, cancelamento, contingência,
responsável técnico e validações facultativas. Implementar apenas as UFs vendidas/comercializadas,
sem presumir que autorização em uma UF certifica todas as demais.

### ECOM-001 — Entrega e marketplace

Criar um fluxo fiscal próprio antes de emitir vendas não presenciais:

- `indPres`, `idDest`, endereço do destinatário e indicador de intermediador;
- CFOP interno/interestadual;
- frete e transportador quando aplicável;
- regras de consumidor final e tributação de destino;
- eventual uso de NF-e modelo 55 quando NFC-e não for adequada.

Até esse cartão ser homologado, o escopo fiscal certificado permanece presencial e interno.

## 11. Fase 7 — produção e operação

### OPS-001 — Configuração de produção

- credenciamento ativo no ambiente de produção da UF;
- CNPJ/IE/UF/município coerentes no cadastro centralizado;
- certificado A1 válido, com chave privada, cadeia confiável e titular compatível;
- endpoints, TLS, DNS, relógio e timezone verificados no servidor real;
- série e próximo número definidos pelo contador e confrontados com o histórico da SEFAZ;
- QR Code v3 consultado no autorizador correto;
- nenhum segredo de homologação copiado automaticamente para produção;
- permissões administrativas e trilha de auditoria da mudança de ambiente.

### OPS-002 — Continuidade e guarda

- backup automático dos XMLs autorizados, offline e eventos;
- cópia fora do banco/servidor principal;
- teste real de restauração;
- exportação mensal ao contador com confirmação de entrega;
- métricas: autorizações, rejeições por código, latência, contingências, resultados incertos,
  vendas sem nota e falhas de backup;
- procedimento escrito para certificado vencido, SEFAZ indisponível, banco indisponível e
  divergência de numeração.

## 12. Fase 8 — homologação e piloto

### HOM-001 — Matriz mínima

Executar no ambiente da mesma UF:

1. dinheiro, Pix, crédito, débito, crediário, cashback e pontos;
2. pagamento dividido em todas as combinações permitidas;
3. desconto administrativo e tratamento escolhido para pontos;
4. produto nacional, importado, com e sem GTIN;
5. produto normal e produto sujeito a ST, se fizerem parte da loja;
6. CPF identificado e consumidor não identificado;
7. nome longo, acentos e venda com muitos itens;
8. rejeição corrigível usando a mesma chave quando legalmente aplicável;
9. timeout antes do envio e depois da autorização;
10. contingência offline, reinício do serviço e retransmissão;
11. cancelamento e estorno ERP;
12. lacuna e inutilização;
13. DANFE e QR Code;
14. conciliação diária e restauração do backup.

Para cada caso guardar: data, responsável, XML, protocolo, série/número, resultado esperado,
resultado observado e aprovação.

### PRD-001 — Piloto controlado

- escolher período com contador e responsável técnico disponíveis;
- começar com poucas vendas de baixo valor, sem promoções fiscais excepcionais;
- consultar cada chave diretamente no portal autorizador;
- comparar venda, pagamento, DANFE, XML e estoque;
- executar conciliação no meio e no fim do dia;
- não “apagar e tentar de novo” em divergência: preservar evidências e consultar a SEFAZ;
- ampliar gradualmente somente após fechar o primeiro lote sem divergências.

## 13. Ordem recomendada de execução

### Bloco A — pode ser dividido entre os dois agentes

- Agente 1: FIS-001, FIS-002 e FIS-003.
- Agente 2: CON-001 e desenho de CON-002.
- Depois, revisão cruzada.

### Bloco B — arquivo central, execução serial

- RES-001;
- RES-002;
- DAN-001 e DAN-002.

O desenho de testes de RES-001 pode ocorrer em paralelo, mas a edição do serviço central deve
ter apenas um responsável.

### Bloco C — catálogo e qualidade

- CAD-001 com o contador;
- XML-001;
- XML-002;
- OPS-001 e OPS-002.

### Bloco D — certificação

- HOM-001;
- correções encontradas;
- PRD-001.

### Bloco futuro/condicional

- REG-001 antes de qualquer tenant fora do Simples;
- RTC-001 antes da vigência aplicável ou de opção pelo regime regular;
- UF-001 antes de vender em outra UF;
- ECOM-001 antes de usar a emissão fiscal em entrega/marketplace.

## 14. Definição de pronto por cartão

Um cartão só muda para `Homologado` quando tiver:

- implementação e revisão cruzada;
- testes unitários e de integração proporcionais ao risco;
- XML ou evidência visual quando aplicável;
- nenhuma regressão na suíte existente;
- documentação operacional atualizada;
- aprovação do contador quando envolver interpretação tributária;
- nenhuma credencial ou dado pessoal real incluído em teste, log ou commit.

## 15. Decisões que não devem ser tomadas apenas pelo código

- NCM, CEST e enquadramento tributário de cada mercadoria;
- pontos como desconto ou crédito virtual;
- incidência e cálculo de ICMS-ST/FCP;
- benefício fiscal e `cBenef`;
- regime e situação de sublimite;
- série/número inicial de produção;
- prazo/procedimento excepcional permitido pela UF;
- obrigatoriedade de NFC-e ou NF-e no canal de venda.

Essas decisões pertencem ao emitente e ao contador. O sistema deve armazená-las, aplicá-las,
versioná-las e mostrar inconsistências — não inventá-las silenciosamente.

---

## 16. Parecer técnico-fiscal simulado

### 16.1 Natureza e limite deste parecer

Esta seção simula duas revisões complementares:

- **visão de contador:** enquadramento tributário, coerência de bases, CST/CSOSN, cadastro de
  mercadorias, obrigações acessórias e reflexos na apuração;
- **visão de agente fiscal:** idoneidade formal do documento, credenciamento, leiaute, numeração,
  autorização, contingência, DANFE, guarda e rastreabilidade.

Ela não transforma o autor deste documento em contador registrado, auditor da SEFAZ ou agente
público, não constitui consulta tributária formal e não substitui o parecer do contador responsável
nem a legislação específica da UF. A SEFAZ não publica uma “nota de certificação” de emissores.
A pontuação abaixo é uma metodologia interna de comparação baseada nos mesmos grupos de controle
que a legislação, o MOC e as regras de autorização examinam.

### 16.2 Premissas usadas na avaliação

O resultado abaixo só vale para a seguinte hipótese:

- pessoa jurídica com CNPJ numérico regular;
- loja optante pelo Simples Nacional, sem excesso de sublimite;
- comércio varejista em estabelecimento físico;
- venda presencial, interna, para consumidor final;
- NFC-e modelo 65;
- sem entrega, marketplace, intermediador ou operação interestadual;
- sem benefício fiscal, desoneração ou regime especial ainda não documentado;
- catálogo composto principalmente por mercadorias vendidas em unidade;
- UF, credenciamento, CNAE, IE, série e numeração ainda dependentes de confirmação externa.

Se qualquer premissa mudar, a avaliação deve ser refeita. Em especial, este parecer não libera
Lucro Presumido/Real, e-commerce, outra UF ou produto com tributação específica apenas porque a
emissão presencial do Simples foi homologada.

### 16.3 Escala comparativa

| Faixa | Classificação interna | Interpretação |
|---:|---|---|
| 90–100 | A — controlado | Evidências suficientes para piloto, sujeito à confirmação externa |
| 75–89 | B — controlado com ressalvas | Pode avançar após fechar ressalvas documentadas |
| 60–74 | C — homologação incompleta | Há risco relevante; não recomendado para produção plena |
| 40–59 | D — não conforme para go-live | Existem lacunas capazes de produzir documento impróprio ou operação sem cobertura |
| 0–39 | E — crítico | Ausência de controles essenciais ou cenário não implementado |

Uma nota alta não convalida tributação. Conforme o Ajuste SINIEF 19/16, a Autorização de Uso é
resultado de regras formais e **não implica convalidação das informações tributárias**.

### 16.4 Resultado executivo em 04/08/2026

| Eixo de avaliação | Peso | Pontos atuais | Diagnóstico |
|---|---:|---:|---|
| Credenciamento e identidade do emitente | 10 | 6 | Valida dados básicos e certificado, mas falta evidência do cadastro real de produção |
| Leiaute, assinatura e XML | 15 | 10 | Montagem e assinatura existem; XSD local está desligado e há dados simplificados |
| Tributação do Simples e cadastro de itens | 20 | 12 | Há CSOSN/ST/IBPT, mas a validade material depende do catálogo e do contador |
| Pagamentos e composição do total | 10 | 5 | Dinheiro/cartão/Pix funcionam; crediário/cashback/pontos estão classificados como 99 |
| DANFE NFC-e | 10 | 3 | O cupom é funcional, mas não é renderizado como DANFE oficial a partir do XML |
| Autorização, timeout e contingência | 10 | 5 | Há retry e offline; resposta perdida pode gerar destino fiscal ambíguo |
| Numeração, cancelamento e inutilização | 10 | 8 | Reserva atômica e eventos estão bem tratados; faltam provas de produção e reconciliação de chave |
| Guarda, conciliação e rastreabilidade | 10 | 5 | XML/eventos e exportação existem; venda sem registro fiscal pode ficar invisível |
| Monitoramento e operação | 5 | 3 | Existem alertas parciais; falta visão completa e ensaio de recuperação |
| **Total** | **100** | **57** | **D — não conforme para go-live pleno** |

#### Leitura pela ótica da autorização SEFAZ

**Avaliação técnica estimada: 66/100 — homologação incompleta.** O projeto já possui assinatura,
chave, série/número, transmissão, protocolo, cancelamento, inutilização e contingência. Os maiores
riscos de autorização são conteúdo real de produção, pagamento impreciso, ausência de validação
local, resultado incerto após timeout e cadastros não confrontados com as tabelas oficiais.

#### Leitura pela ótica tributária

**Avaliação material estimada: 49/100 — parecer com ressalva impeditiva.** A lógica do Simples é
uma base utilizável, mas ainda não existe evidência suficiente de que NCM, CEST, CFOP, CSOSN,
origem e eventual ST estejam corretos para todos os produtos. Pontos têm dois tratamentos fiscais.
Autorização em homologação não resolve essas matérias.

#### Parecer conclusivo simulado

> **Não recomendado para produção plena na condição atual.** Recomendado concluir os cartões P0
> do escopo inicial, obter aprovação formal do contador sobre o catálogo e os XMLs, executar a
> matriz HOM-001 e somente então realizar piloto controlado. O parecer pode ser revisto para
> “favorável com ressalvas” quando pagamentos, resultado incerto, DANFE, conciliação e catálogo
> estiverem homologados.

O suporte ao regime normal recebe avaliação separada de **20/100 — não certificado**, porque os
itens calculam tributos que ainda não são consolidados corretamente nos totalizadores completos da
NFC-e. Ele não deve herdar a nota do cenário Simples.

## 17. Base de comparação inspirada nos controles da SEFAZ

### 17.1 Camada formal de autorização

A revisão formal segue os elementos do Ajuste SINIEF 19/16 e do MOC:

| Controle | O que a fiscalização/autorizador verifica | Evidência esperada no projeto |
|---|---|---|
| Credenciamento | Emitente previamente credenciado na UF | Consulta/certidão ou tela oficial arquivada |
| Autoria | Assinatura qualificada e certificado compatível | XML assinado e cadeia validada no servidor real |
| Leiaute | XML no schema vigente | relatório de validação XSD e versão do pacote |
| Identidade | CNPJ/CPF, IE, UF, município e CRT coerentes | ficha cadastral confrontada com CCC/UF |
| Numeração | série e número sequenciais e únicos | relatório de sequência, lacunas e inutilizações |
| Conteúdo | regras de validação do MOC e Notas Técnicas | XMLs de cenários e respectivos `cStat` |
| Pagamentos | grupo obrigatório e códigos vigentes | XML por meio e pagamento dividido |
| Produtos | NCM, CEST e GTIN quando aplicáveis | planilha do catálogo aprovada e XML amostral |
| DANFE | representação conforme manual e vinculada ao XML | PDF/impressão e leitura do QR Code |
| Contingência | emissão offline, justificativa e transmissão posterior | XML original, histórico e autorização final |
| Eventos | cancelamento/inutilização com protocolo | `procEventoNFe` e XML de inutilização |
| Guarda | documento e protocolo preservados | restauração de backup e exportação conferida |

### 17.2 Camada material de tributação

Esta camada não é convalidada pela autorização e deve ser revisada como apuração tributária:

| Controle tributário | Pergunta de auditoria | Comparação exigida |
|---|---|---|
| Regime | O CRT e o regime cadastrado correspondem à situação real? | opção do Simples, sublimite e cadastro estadual |
| Materialidade | A mercadoria foi classificada corretamente? | descrição comercial × NCM vigente × documento de entrada |
| ST/CEST | O item pertence ao segmento e à lista aplicável? | NCM/descrição × CEST × Convênio 142/18 × norma da UF |
| Natureza | O CFOP representa a operação praticada? | canal, origem/destino, entrega e finalidade |
| Situação ICMS | CSOSN/CST e origem são compatíveis? | regime × mercadoria × benefício × tributação anterior |
| Base e total | Descontos, ST, FCP e pagamentos fecham? | itens × totalizadores × valor da venda × recebimentos |
| Fidelidade | Pontos/cashback são desconto ou crédito? | regulamento do programa × contabilização × XML |
| Transparência | Tributos aproximados estão vigentes? | Lei 12.741/2012 × fonte/versão utilizada |
| RTC | IBS/CBS seguem regime e vigência aplicável? | legislação, NT, tabelas CST/`cClassTrib` e cronograma |
| Escrituração | O XML exportado fecha com vendas e apuração? | ERP × XML autorizado × relatório do contador |

### 17.3 Evidência mínima para mudar a classificação

Uma linha só pode receber pontuação integral quando houver três provas:

1. **prova de implementação:** teste automatizado ou inspeção do código;
2. **prova fiscal:** XML/DANFE/protocolo do cenário;
3. **prova de mérito:** aprovação do contador ou fonte oficial que determine o tratamento.

Passar em teste unitário sem XML não vale como homologação. Ter `cStat=100` sem aprovação do
cadastro não vale como conformidade tributária. Ter aprovação verbal sem evidência versionada não
vale como controle auditável.

## 18. Motivos detalhados dos achados

| Achado | Evidência atual | Motivo fiscal/operacional | Cartão corretivo |
|---|---|---|---|
| Crediário, pontos e cashback em `99` | `MapFormaPagamento` usa `fpOutro` como padrão | Há códigos específicos vigentes; “Outros” reduz a qualidade declaratória | FIS-002 |
| Pontos têm dois tratamentos | `PointsApplied` reduz o total; meio “Pontos” não reduz | Pode alterar indevidamente a base tributável se o contrato econômico for o mesmo | FIS-003 |
| Timeout vira offline imediatamente | `TransmitirAsync` remonta `tpEmis=9` após exceção de rede | A primeira chave pode ter sido autorizada e a resposta ter se perdido | RES-001 |
| Contingência não preserva todo o XML entregue | Persistência prioriza chave/QR/status | A retransmissão e a reimpressão devem reproduzir o documento original | RES-002 |
| Cupom não é DANFE oficial | comentário e layout do frontend reconhecem simplificação | A representação deve seguir o manual e refletir fielmente o XML | DAN-001/002 |
| Venda pode não criar nota | emissão depende de escolha no fechamento | Sem registro fiscal, a venda não aparece nem como pendente | CON-001 |
| NCM/CEST/CFOP validados só por formato | sanitizadores verificam tamanho/dígitos | Código formalmente válido pode ser materialmente incorreto ou revogado | CAD-001 |
| `cProd` é a posição do item | produto recebe `000001`, `000002` etc. | Dificulta cruzamento estável com estoque, entradas e escrituração | XML-001 |
| GTIN sempre sai como `SEM GTIN` | montagem ignora `Product.Barcode` | Ajuste 19/16 exige GTIN quando o produto o possui | XML-001 |
| Schema local desligado | `ValidarSchemas=false` | Erros evitáveis chegam à SEFAZ depois da reserva do número | XML-002 |
| Totalizadores do regime normal incompletos | totais gerais permanecem zerados em grupos calculados por item | Soma do documento pode divergir dos tributos dos itens | REG-001 |
| IBS/CBS controlado por condição fixa de ano | produção 2027 cai em exceção até alteração manual | Mudança de calendário pode parar todas as emissões | RTC-001 |
| Ausência de `cBenef` e desoneração completa | grupos não são representados | Operação com benefício pode autorizar incompleta ou ser rejeitada conforme UF | UF-001 |
| Tudo sai interno/presencial/sem frete | `idDest`, `indPres` e transporte são fixos | Venda não presencial ou interestadual exige conteúdo diferente | ECOM-001 |
| Configuração externa não foi provada | código valida preenchimento, não o credenciamento real | Homologação e produção possuem cadastros e permissões próprios | OPS-001 |
| Alertas não cobrem venda sem nota | partem de `NotaFiscalEmitida` existente | O universo fiscal pode ser menor que o universo de vendas | CON-001/002 |

## 19. Fontes oficiais e vínculo com o plano

As fontes devem ser verificadas novamente na data de cada homologação, porque Notas Técnicas,
schemas, tabelas e legislação estadual podem mudar.

### 19.1 Constituição operacional da NFC-e

1. [Ajuste SINIEF 19/16 — texto consolidado](https://www.confaz.fazenda.gov.br/legislacao/ajustes/2016/AJ_019_16)
   - institui NFC-e e DANFE NFC-e;
   - exige credenciamento prévio;
   - define XML, assinatura, numeração, NCM, CEST, GTIN, destinatário e pagamentos;
   - declara expressamente que autorização formal não convalida a tributação;
   - fundamenta FIS-001, CAD-001, XML-001, OPS-001 e toda a avaliação.

2. [MOC 7.0 — Anexo I, leiaute e regras de validação](https://www.nfe.fazenda.gov.br/portal/exibirArquivo.aspx?conteudo=J+I+v4eN00E%3D)
   - define campos, grupos, tamanhos, ocorrências e regras de rejeição;
   - fundamenta FIS-002, XML-002, REG-001, UF-001 e os testes de XML.

3. [Portal oficial de manuais NF-e/NFC-e](https://www.nfe.fazenda.gov.br/portal/listaConteudo.aspx?AspxAutoDetectCookieSupport=1&tipoConteudo=ndIjl+iEFdE%3D)
   - ponto de consulta da versão vigente do MOC, DANFE, contingência e boas práticas;
   - deve ser consultado antes da homologação final.

4. [FAQ oficial — validações e alcance da Autorização de Uso](https://www.nfe.fazenda.gov.br/Portal/perguntasFrequentes.aspx?AspxAutoDetectCookieSupport=1&tipoConteudo=auR4yGlWmRY%3D)
   - distingue validação formal de mérito tributário;
   - orienta tratamento de documentos transmitidos com retorno pendente;
   - fundamenta RES-001 e a separação das duas notas de avaliação.

### 19.2 DANFE, QR Code e contingência

5. [Manual de Padrões Técnicos do DANFE NFC-e e QR Code — versão 6.0](https://www.nfe.fazenda.gov.br/PORTAl/exibirArquivo.aspx?conteudo=k%2FIuuaW4YiY%3D)
   - define divisões, conteúdo mínimo, consumidor, protocolo e QR Code;
   - fundamenta DAN-001 e DAN-002.

6. [NT 2025.001 — QR Code NFC-e versão 3](https://hom.nfe.fazenda.gov.br/portal/exibirArquivo.aspx?conteudo=uthum2eC3is%3D)
   - especifica QR Code v3 e suas regras de validação;
   - fundamenta OPS-001 e a validação do QR normal/offline.

7. [Portal oficial — Manual de Contingência Offline NFC-e](https://www.nfe.fazenda.gov.br/portal/listaConteudo.aspx?AspxAutoDetectCookieSupport=1&tipoConteudo=ndIjl+iEFdE%3D)
   - fonte da versão vigente do procedimento offline;
   - fundamenta RES-001, RES-002 e HOM-001.

### 19.3 Pagamentos e identificação dos produtos

8. [Informe oficial de publicação da Tabela de Meios de Pagamento v.1.11](https://hom.nfe.fazenda.gov.br/portal/informe.aspx?AspxAutoDetectCookieSupport=1&Informe=DssydLvB4Ds%3D&ehCTG=false)
   - registra a atualização publicada em 06/03/2026;
   - fundamenta a obrigação de consultar a tabela vigente em FIS-002.

9. [Tabela oficial vigente — Documentos diversos do Portal NF-e](https://hom.nfe.fazenda.gov.br/portal/listaConteudo.aspx?AspxAutoDetectCookieSupport=1&tipoConteudo=%2FNJarYc9nus%3D)
   - domínio oficial dos códigos de pagamento;
   - base para crediário `05` e fidelidade/cashback/crédito virtual `19`.

10. [NT 2021.003 — validação de GTIN](https://www.nfe.fazenda.gov.br/Portal/exibirArquivo.aspx?conteudo=iMqXryAlBn4%3D)
    - exige `cEAN`/`cEANTrib` quando o produto possui GTIN e disciplina validação no CCG;
    - fundamenta XML-001.

11. [Convênio ICMS 142/18 — substituição tributária e CEST](https://www.confaz.fazenda.gov.br/legislacao/convenios/2018/CV142_18)
    - relaciona segmentos, mercadorias e CEST no regime de ST;
    - deve ser combinado com a legislação da UF;
    - fundamenta CAD-001, REG-001 e UF-001.

### 19.4 Transparência tributária e Reforma Tributária

12. [Lei 12.741/2012 — transparência dos tributos](https://www.planalto.gov.br/ccivil_03/_ato2011-2014/2012/lei/l12741.htm)
    - fundamenta a informação de tributos aproximados ao consumidor;
    - base de CAD-001 e DAN-001.

13. [Portal nacional — documentos vigentes da RTC](https://www.nfe.fazenda.gov.br/portal/listaConteudo.aspx?AspxAutoDetectCookieSupport=1&tipoConteudo=6WfrpZYE4Ik%3D)
    - reúne as versões vigentes da NT 2025.002;
    - fundamenta RTC-001.

14. [Orientações da Receita Federal para 2026](https://www.gov.br/receitafederal/pt-br/acesso-a-informacao/acoes-e-programas/programas-e-atividades/reforma-tributaria-do-consumo/orientacoes-2026)
    - descreve obrigações acessórias, destaque e fase de transição;
    - precisa ser lida junto ao regime do contribuinte e ao cronograma vigente.

15. [Orientação do CGIBS sobre o Simples em 2026](https://cgibs.gov.br/reforma-tributaria-comeca-em-2026-com-periodo-de-adaptacao-destaque-informativo-dos-novos-tributos-e-dispensa-de-penalidades)
    - diferencia o Simples Nacional do regime regular no primeiro ano da transição;
    - fundamenta a separação entre o escopo inicial e REG-001/RTC-001.

16. [Comunicado RFB/CGIBS de 27/07/2026 sobre cronograma](https://www.gov.br/receitafederal/pt-br/assuntos/noticias/2026/julho/receita-federal-e-comite-gestor-do-ibs-divulgarao-cronograma-para-emissao-dos-documentos-fiscais-eletronicos-da-reforma-tributaria)
    - demonstra que datas e etapas ainda exigem acompanhamento oficial;
    - justifica não espalhar datas rígidas pelo motor fiscal.

### 19.5 Fonte estadual ainda obrigatória

Antes do go-live deve ser acrescentado a esta lista o conjunto oficial da UF efetiva:

- credenciamento e habilitação de produção;
- portal/endpoint autorizador e consulta do QR Code;
- prazo e procedimento de cancelamento;
- contingência offline;
- `cBenef`, desoneração, FCP, ICMS-ST e regras facultativas;
- eventual exigência de responsável técnico, equipamento ou programa credenciado;
- regras de entrega, identificação do consumidor e limites reduzidos pela UF.

Sem informar a UF não é possível concluir essa camada. FIS-001 só estará completo quando os links
estaduais e a data de consulta forem registrados.

## 20. Passo a passo de execução e avaliação

### Passo 1 — abrir a pasta de evidências

Criar uma estrutura controlada fora do código público para armazenar:

- ficha cadastral e comprovantes da UF;
- planilha fiscal aprovada;
- XMLs, protocolos e eventos de homologação;
- PDFs/amostras do DANFE;
- atas de decisão do contador;
- relatórios de testes e restauração.

Não armazenar PFX, senha, token CSC ou dados pessoais desnecessários. No repositório devem ficar
apenas templates sem segredo e referências aos responsáveis.

**Saída:** índice de evidências com proprietário, data e validade.

### Passo 2 — fechar FIS-001 com o contador

1. preencher todas as premissas da seção 16.2;
2. consultar cadastro e credenciamento na UF;
3. confirmar Simples, sublimite e CNAE;
4. confirmar escopo presencial/interno;
5. listar exceções de mercadoria;
6. registrar série/número de produção;
7. anexar fontes estaduais.

**Saída:** termo de escopo aprovado.
**Critério:** qualquer cenário não descrito permanece “não certificado”.

### Passo 3 — extrair e auditar o catálogo

1. exportar todos os produtos ativos com descrição, código, GTIN, NCM, CEST e natureza;
2. separar por família comercial;
3. confrontar com notas de entrada e cadastro do fornecedor;
4. validar NCM vigente;
5. validar CEST/segmento e legislação estadual;
6. validar CFOP, CSOSN, origem, ST/FCP e transparência;
7. devolver as correções ao cadastro;
8. versionar a planilha aprovada.

**Saída:** CAD-001 homologado e amostra mínima por tratamento tributário.

### Passo 4 — implementar e provar pagamentos

1. concluir FIS-002;
2. decidir FIS-003;
3. criar testes de cada meio e split;
4. gerar XML de cada cenário;
5. conferir soma de `vPag`, `vNF`, descontos e troco;
6. submeter três XMLs-chave ao contador: crediário, cashback e pontos.

**Saída:** matriz de pagamento com XML esperado/observado.

### Passo 5 — corrigir a máquina de estados da autorização

1. abstrair o cliente SEFAZ;
2. persistir tentativa antes da chamada;
3. introduzir estado de resultado incerto;
4. consultar chave após perda de resposta;
5. recuperar protocolo de autorização já existente;
6. tratar duplicidade por reconciliação;
7. entrar em contingência somente pelo procedimento válido;
8. preservar o XML offline completo;
9. testar reinício do processo em cada estado.

**Saída:** RES-001/002 homologados com testes de falha controlada.

### Passo 6 — substituir o cupom pelo DANFE baseado no XML

1. ler `nfeProc` ou XML offline persistido;
2. montar DTO fiscal imutável;
3. implementar todas as divisões do manual;
4. mostrar meios e valores individuais;
5. incluir consumidor e protocolo quando existentes;
6. diferenciar normal, contingência e cancelado;
7. impedir que documento rejeitado pareça autorizado;
8. testar impressão e QR Code.

**Saída:** DAN-001/002 com parecer visual e fiscal.

### Passo 7 — implementar conciliação diária

1. listar todas as vendas tributáveis fechadas;
2. relacionar cada uma à nota fiscal;
3. classificar as sete situações de CON-001;
4. mostrar diferenças de valor e pagamento;
5. registrar opção de não emissão, usuário e justificativa;
6. alertar pendências e atribuir responsável;
7. exportar o relatório ao contador.

**Saída:** uma venda propositalmente sem nota aparece no mesmo dia e possui trilha de resolução.

### Passo 8 — validar XML e conteúdo antes da transmissão

1. instalar/versionar schemas oficiais;
2. habilitar validação local;
3. criar fixtures válidas e inválidas;
4. validar textos, GTIN, totais e grupos condicionais;
5. diferenciar erro de schema, configuração, rejeição e conectividade;
6. provar que falha local não aciona contingência.

**Saída:** XML-001/002 homologados.

### Passo 9 — executar homologação como auditoria

Para cada linha de HOM-001:

1. preparar dados e resultado esperado;
2. emitir;
3. guardar XML enviado e processado;
4. registrar `cStat`, protocolo, chave, série/número;
5. validar schema;
6. conferir item, imposto, total e pagamento;
7. imprimir e ler QR Code;
8. conferir efeitos no ERP;
9. obter assinatura do executor e revisor;
10. abrir cartão corretivo para qualquer divergência.

**Saída:** dossiê de homologação completo, não apenas lista de testes “passou”.

### Passo 10 — reavaliar a pontuação

Recalcular a seção 16.4 usando somente evidências. Metas mínimas para avançar:

- total geral igual ou superior a 80;
- nenhum eixo P0 abaixo de 75% do próprio peso;
- autorização/contingência sem cenário de chave ambígua;
- pagamentos, DANFE e conciliação homologados;
- catálogo do piloto integralmente aprovado;
- regime normal/e-commerce fora do escopo claramente identificados.

Essa meta é governança interna, não limite legal criado pela SEFAZ.

### Passo 11 — ensaio de produção sem venda real

1. aplicar migrations em staging igual à produção;
2. restaurar backup recente;
3. validar certificado no sistema operacional do servidor;
4. conferir DNS/TLS/relógio/endpoints;
5. confirmar credenciamento, série e numeração;
6. executar smoke tests que não consumam documento real quando possível;
7. simular indisponibilidade e restauração;
8. confirmar plantão e contatos.

**Saída:** OPS-001/002 aprovados.

### Passo 12 — piloto e fechamento assistido

1. emitir poucas vendas reais de baixo risco;
2. consultar cada chave no autorizador da UF;
3. comparar venda, XML, DANFE, pagamento e estoque;
4. executar conciliação após cada pequeno lote;
5. exportar XML ao contador;
6. fechar o dia comparando valores e sequência;
7. registrar incidente sem apagar evidências;
8. aumentar o volume somente depois da revisão conjunta.

**Saída:** PRD-001 aprovado e novo parecer assinado.

## 21. Modelo de ficha de avaliação por cenário

Copiar esta tabela para cada teste de homologação ou produção:

| Campo | Preenchimento |
|---|---|
| Cenário | Ex.: venda presencial com cashback integral |
| Escopo fiscal | Simples Nacional, operação interna, consumidor final |
| Produto/NCM/CEST | códigos e versão da validação |
| CFOP/CSOSN/origem | esperado e fundamento |
| Base/tributos | cálculo esperado |
| Pagamento | `tPag`, valor, integração e split |
| Documento | série, número, chave e `tpEmis` |
| Retorno | `cStat`, motivo e protocolo |
| DANFE/QR | conferido por quem e quando |
| ERP | estoque, financeiro, fidelidade e conciliação |
| Fonte oficial | link, versão e data de consulta |
| Evidências | localização do XML/PDF/log seguro |
| Resultado | Conforme / Conforme com ressalva / Não conforme |
| Implementador | nome/data |
| Revisor técnico | nome/data |
| Contador responsável | nome/CRC/data |
| Ação corretiva | cartão e prazo |

## 22. Modelo de parecer final para assinatura

> Com base no escopo descrito, nas fontes oficiais consultadas, nos XMLs e protocolos anexos,
> nos testes de contingência, na conferência do DANFE e na conciliação entre vendas e documentos,
> o sistema foi avaliado como: **[favorável / favorável com ressalvas / não favorável]** para o
> cenário **[descrever]**, na UF **[UF]**, sob o regime **[regime]**. Esta conclusão não abrange
> operações, produtos, regimes ou UFs não listados. Pendências remanescentes: **[listar]**.

Assinaturas esperadas:

- responsável da empresa emitente;
- contador responsável e CRC;
- responsável técnico pela implementação;
- revisor técnico;
- data e versão do sistema avaliado.

## 23. Recorte comercial — PMEs do Simples Nacional em 2026

### 23.1 Público-alvo certificado nesta etapa

O foco comercial inicial do ERP são PMEs. Para a primeira certificação fiscal, o sistema será
avaliado especificamente para tenants que atendam simultaneamente a estas condições:

- pessoa jurídica optante pelo Simples Nacional, `CRT=1`;
- sem excesso de sublimite;
- sem opção pelo regime regular de IBS/CBS;
- comércio varejista;
- venda presencial em estabelecimento físico;
- operação interna, dentro da mesma UF;
- consumidor final;
- mercadorias em unidade, com cadastro fiscal aprovado;
- credenciamento NFC-e modelo 65 ativo na UF;
- certificado, série, numeração e demais credenciais pertencentes ao próprio tenant.

O desenvolvedor fornece a plataforma e realiza o processamento técnico. A loja cliente continua
sendo o emitente e responsável pelo conteúdo fiscal, com participação de seu contador. Certificado,
numeração, credenciamento e configuração tributária nunca devem ser compartilhados entre tenants.

### 23.2 Condições para considerar o produto adequado em 2026

Para esse público e somente nesse escopo, o produto poderá receber parecer interno de
**“adequado para operação controlada em 2026”** depois que os seguintes grupos estiverem
homologados:

1. pagamentos: crediário `05`, fidelidade/cashback/crédito virtual `19`, split e totais;
2. decisão fiscal documentada para pontos pré-aplicados;
3. consulta e reconciliação da chave após timeout ou resposta perdida;
4. contingência preservando o XML original completo;
5. DANFE NFC-e oficial derivado do XML fiscal;
6. conciliação diária de vendas com documentos, inclusive venda sem nota criada;
7. catálogo aprovado: NCM, CEST, CFOP, CSOSN, origem, ST/FCP e GTIN quando aplicáveis;
8. validação com schemas vigentes e erros separados de indisponibilidade;
9. configuração e credenciamento de produção comprovados por tenant e UF;
10. bateria HOM-001, backup/restauração e piloto PRD-001 concluídos.

Isso não representa risco fiscal zero nem certificação da SEFAZ. Representa um nível técnico e
documental defensável, com responsabilidades, evidências e limites conhecidos.

### 23.3 Funcionalidades que não precisam impedir o piloto desse recorte

Desde que estejam claramente identificadas como não certificadas e não sejam utilizadas pelos
tenants do piloto, podem continuar em outra frente:

- Lucro Presumido e Lucro Real;
- totalizadores e apuração completa do regime normal;
- opção do Simples pelo regime regular de IBS/CBS;
- regras de IBS/CBS aplicáveis a partir de 2027;
- Imposto Seletivo;
- regimes diferenciados, monofasia e crédito presumido da RTC;
- transição progressiva de ICMS/ISS para IBS entre 2029 e 2032;
- venda interestadual;
- entrega, marketplace e intermediador;
- benefícios e regras de UFs ainda não homologadas;
- vinculação futura de pagamentos e split payment tributário.

Não implementar esses cenários agora é diferente de tratá-los silenciosamente como se fossem o
cenário simples. A interface, o onboarding e a documentação precisam declarar o limite de suporte.

### 23.4 Regra de entrada de cada tenant

Antes de habilitar produção para uma PME, registrar:

| Verificação | Resultado obrigatório |
|---|---|
| Regime atual | Simples Nacional, `CRT=1` |
| Sublimite | Sem excesso |
| IBS/CBS | Sem opção pelo regime regular no período |
| Canal | Presencial |
| Destino | Operação interna |
| Credenciamento | NFC-e produção ativa na UF |
| Certificado | Válido e pertencente ao tenant |
| Série/número | Confirmados com histórico e contador |
| Catálogo | Aprovado para os produtos que serão vendidos |
| UF | Homologada na matriz estadual do ERP |
| Evidências | XML, DANFE, QR, protocolo e conciliação aprovados |

Se uma condição não for atendida, o tenant não herda automaticamente a certificação do recorte.
Ele deve seguir a frente correspondente: REG-001, RTC-001, UF-001 ou ECOM-001.

### 23.5 Decisão de go-live para 2026

| Situação | Parecer |
|---|---|
| Pendências P0 atuais ainda abertas | Não favorável |
| Correções prontas apenas em testes unitários | Não favorável; falta homologação documental |
| P0 corrigidos, catálogo aprovado e homologação concluída | Favorável para piloto controlado |
| Piloto conciliado sem divergências | Favorável com monitoramento para o escopo certificado |
| Tenant fora do Simples, com sublimite, e-commerce ou outra UF não homologada | Não certificado |
| Chegada de 2027 sem RTC-001 concluído | Não favorável para continuidade automática |

### 23.6 Prioridade resumida de desenvolvimento

Para atingir o go-live desse mercado em 2026, executar nesta ordem:

`pagamentos → pontos → timeout/chave → contingência → DANFE → conciliação → catálogo/XSD → configuração por UF → homologação → piloto`.

REG-001 e RTC-001 continuam no planejamento desde já, mas não devem atrasar o piloto de tenants
que comprovadamente permaneçam no recorte Simples/presencial/interno durante 2026.

---

## 24. Registro de auditoria cruzada após alterações do motor fiscal — 04/08/2026

Esta seção registra a revisão das observações produzidas durante a implementação assistida. Ela
não substitui os achados, notas ou cartões anteriores: acrescenta evidências sobre o estado atual
do código e corrige interpretações que poderiam levar a uma implementação fiscal incorreta.

### 24.1 Pagamentos — confirmação e distinção entre `05`, `19` e `21`

A conferência da tabela nacional vigente e dos enums do pacote fiscal confirma:

| `tPag` | Uso no recorte certificado | Observação |
|---|---|---|
| `05` | cartão da loja, crediário digital e outros crediários próprios | inclui crediário com ou sem carnê; não usar para cartão bandeirado |
| `12` | vale-presente | não representa cashback nem programa de pontos |
| `19` | programa de fidelidade, cashback e crédito virtual | usar quando o benefício for tratado juridicamente como meio de pagamento |
| `21` | crédito em loja decorrente de valor pago anteriormente, troca ou devolução | não confundir com crediário concedido para pagamento futuro |

Portanto, FIS-002 permanece correto em exigir crediário próprio em `05` e fidelidade/cashback em
`19`. A presença dos enums na Zeus não escolhe o código automaticamente: `MapFormaPagamento`
continua sendo responsabilidade do adaptador do ERP.

**Fonte complementar:** [Portal SVRS — tabela e Informe Técnico 2024.002 v1.11](https://dfe-portal.svrs.rs.gov.br/Nfce/Documentos).

### 24.2 REG-001 — regressão de escopo confirmada no código em desenvolvimento

As alterações em andamento passaram a montar grupos de item para Lucro Presumido e Lucro Real,
mas o documento completo ainda não fecha os totalizadores:

- `SomarTotaisIcms` reconhece somente `ICMSSN201` e `ICMSSN202`;
- `ICMSTot.vBC`, `vICMS`, `vICMSDeson` e `vFCP` continuam fixados em zero;
- `ICMSTot.vPIS` e `vCOFINS` continuam fixados em zero;
- os testes adicionados exercitam principalmente os grupos de item e não constituem prova de um
  XML completo, totalizado, serializado e validado;
- defaults gerais de CST/alíquota de PIS e COFINS não podem substituir a classificação confirmada
  pelo contador para operações monofásicas, alíquota zero, isenção, suspensão ou outros tratamentos.

Isso cria divergência entre os tributos destacados nos itens e os totais do documento. O regime
normal permanece **20/100 — não certificado**, independentemente de os testes isolados passarem.

**Ação imediata:** restaurar a guarda de pré-voo que bloqueia emissão em Lucro Presumido e Lucro
Real antes da reserva do número. O cadastro desses regimes pode continuar disponível. A emissão
só deve ser reaberta depois de REG-001 produzir XML completo por CST, passar no XSD e receber
aprovação fiscal.

### 24.3 Achados anteriores reconfirmados

A inspeção cruzada reconfirmou, no estado auditado:

- meios internos ainda caindo no padrão `tPag=99`;
- `cProd` derivado da posição do item, sem identidade estável do produto;
- `cEAN` e `cEANTrib` sempre enviados como `SEM GTIN`, mesmo havendo código de barras cadastrado;
- `ValidarSchemas=false` nos fluxos de emissão e distribuição;
- condição fixa de ano que impede a continuidade do IBS/CBS sem atualização manual;
- NCM/CEST validados principalmente por formato, sem provar vigência ou enquadramento material.

Esses pontos já estão vinculados, respectivamente, a FIS-002, XML-001, XML-002, RTC-001 e
CAD-001. A confirmação não altera a prioridade nem transforma código em evidência fiscal.

### 24.4 Ampliação da matriz de QR Code

Acrescentar a HOM-001 testes negativos e positivos do QR Code versão vigente, sempre gerado a
partir do mesmo XML que será assinado, transmitido, armazenado e impresso:

| Código | Cenário que o teste deve cobrir |
|---|---|
| `396` | parâmetro obrigatório inexistente no QR Code |
| `397` | parâmetro do QR Code divergente do XML da nota |
| `445` | assinatura informada indevidamente em emissão normal |
| `474` | assinatura ausente em emissão de contingência offline |
| `583` | assinatura da contingência divergente do valor calculado |

A rejeição `407` não deve ser tratada como requisito ativo do go-live de 2026: a regra por UF foi
retirada na evolução da NT 2025.001 após a migração para o QR Code versão 3. Ela pode permanecer
apenas como referência histórica de compatibilidade, nunca como prova atual de homologação.

As rejeições acima não são exclusivas de produção. Devem ser exercitadas em homologação quando o
ambiente autorizador permitir; produção acrescenta a prova do CSC, credenciamento, endpoints e
configurações reais do tenant.

**Fontes:** [NT 2025.001 — QR Code versão 3](https://hom.nfe.fazenda.gov.br/portal/exibirArquivo.aspx?conteudo=uthum2eC3is%3D) e [Portal SVRS — documentos NFC-e](https://dfe-portal.svrs.rs.gov.br/Nfce/Documentos).

### 24.5 Prazo e dívida operacional da contingência

RES-002 e CON-002 devem controlar também a idade de cada NFC-e offline. A regra operacional deve
ser transmitir imediatamente após cessar o impedimento, com alertas crescentes e bloqueio de
silêncio operacional. O prazo-limite não deve ser codificado como constante nacional sem conferir
a modalidade e a UF: materiais nacionais tratam a contingência offline com referência de 24 horas,
enquanto disciplinas estaduais podem estabelecer procedimento ou prazo próprio.

Para cada UF certificada, registrar:

1. fundamento normativo vigente;
2. modalidade de contingência aceita;
3. prazo de transmissão posterior;
4. prazo e procedimento de cancelamento por substituição;
5. alertas internos anteriores ao limite;
6. escalonamento ao responsável da loja e ao suporte fiscal;
7. evidência de retransmissão, autorização ou tratamento da rejeição.

**Fontes:** [Manual oficial de contingência offline NFC-e](https://www.nfe.fazenda.gov.br/portal/exibirArquivo.aspx?conteudo=q7bExS0dtAE%3D) e legislação específica da UF cadastrada em UF-001.

### 24.6 Decisão sobre pontos permanece externa ao código

A auditoria não resolve FIS-003. Pontos concedidos como desconto incondicional e pontos usados
como crédito virtual podem produzir efeitos distintos sobre total, pagamento e base tributável.
A definição depende do regulamento contratual do programa e da orientação do contador do emitente.
Até a decisão ser registrada, o sistema não deve manter dois tratamentos fiscais silenciosos para
o mesmo benefício econômico nem escolher um deles por conveniência técnica.

### 24.7 LIB-001 — papel, versão e homologação da biblioteca Zeus

O projeto referencia `Zeus.Net.NFe.NFCe` na versão `2026.6.30.1332`. Na data desta auditoria, o
NuGet publica `2026.7.16.1250` como versão mais recente. Isso não torna a versão instalada
automaticamente inválida nem autoriza atualização direta em produção; cria uma tarefa de análise
de diferenças, schemas, correções e possíveis mudanças de comportamento.

**O que deve ser delegado à Zeus:**

- representação das classes do leiaute NF-e/NFC-e;
- serialização e desserialização do XML;
- assinatura digital e utilitários técnicos;
- consumo dos webservices de autorização, consulta, cancelamento e inutilização;
- enums e estruturas existentes no pacote, inclusive pagamentos e grupos de IBS/CBS;
- validação XSD e geração de QR/DANFE quando o componente escolhido oferecer o recurso e o
  resultado for homologado contra os manuais oficiais.

**O que não deve ser atribuído automaticamente à Zeus:**

- escolher CRT, CFOP, CSOSN/CST, NCM, CEST, origem, `cBenef` ou `cClassTrib`;
- decidir incidência, benefício, ST/FCP ou natureza jurídica de pontos/cashback;
- converter os meios comerciais do ERP para `tPag` sem um mapeamento explícito;
- determinar valores, descontos, divisão de pagamentos ou totalizadores do negócio;
- controlar concorrência da numeração, resultado incerto, contingência, persistência e conciliação;
- provar conformidade fiscal apenas porque o objeto foi serializado ou autorizado pela SEFAZ.

O estado atual não aproveita toda a proteção possível da biblioteca: `ValidarSchemas=false` está
configurado nos dois fluxos auditados. Além disso, a existência de enums como `05`, `19` e `21`
não corrige o XML enquanto `MapFormaPagamento` continuar selecionando `99`.

**Plano de aplicação da biblioteca:**

1. inventariar quais APIs da Zeus são usadas para XML, assinatura, QR, transmissão e eventos;
2. comparar as alterações entre `2026.6.30.1332` e a versão candidata, incluindo dependências e
   Notas Técnicas atendidas;
3. atualizar somente em branch própria, com versão fixada e caminho de retorno conhecido;
4. ativar XSD compatível e versionado, mantendo a SEFAZ como autoridade final;
5. executar testes de regressão por snapshot do XML completo, não apenas dos objetos de item;
6. testar autorização, consulta após timeout, cancelamento, inutilização e contingência;
7. comparar o DANFE atual com a impressão nativa ou pacote oficial da família Zeus e escolher o
   resultado que passe no manual, sem manter duas fontes fiscais divergentes;
8. registrar versão homologada por ambiente e política de atualização periódica.

**Aceite de LIB-001:** versão fixada, dependências inventariadas, XSD ativo, XMLs completos do
recorte certificado aprovados, QR normal/offline conferido e nenhuma regra fiscal material deixada
como default silencioso da biblioteca ou do ERP.

**Fontes:** [repositório oficial ZeusFiscal](https://github.com/Hercules-NET/ZeusFiscal) e
[NuGet — Zeus.Net.NFe.NFCe](https://www.nuget.org/packages/Zeus.Net.NFe.NFCe).

### 24.8 Stack open source — decisão revisada após inspeção dos pacotes

A inspeção do arquivo NuGet realmente instalado corrige uma hipótese da seção anterior: o pacote
`Zeus.Net.NFe.NFCe 2026.6.30.1332` contém as DLLs `DFe.Classes`, `DFe.Utils`, `DFe.Wsdl`,
`NFe.Classes`, `NFe.Servicos`, `NFe.Utils` e `NFe.Wsdl.Standard`, mas **não contém uma DLL de
DANFE**. O repositório da Zeus possui projetos de impressão separados; ter o código no ecossistema
não significa que o renderizador esteja instalado ou homologado nesta aplicação.

#### Bibliotecas aprovadas para permanecer no núcleo

| Componente | Versão auditada | Licença/fonte | Uso decidido |
|---|---:|---|---|
| `Zeus.Net.NFe.NFCe` | `2026.6.30.1332` | LGPL-2.1 no repositório `ZeusAutomacao/DFe.NET` | classes fiscais, serialização, assinatura, QR fiscal, webservices e eventos |
| `QRCoder` | `1.4.3` | MIT declarada no próprio `.nuspec` | renderização de imagem QR quando necessária; não define o conteúdo fiscal da URL |
| `System.Xml.Schema` | runtime .NET 8 | código aberto do .NET | validar contra cópia versionada dos XSDs oficiais |
| XSDs oficiais NF-e/NFC-e | versão vinculada à NT homologada | publicação oficial | dados de validação; não são motor de regras tributárias |

O conteúdo e a assinatura da URL fiscal devem vir do XML/Zeus. O `QRCoder` apenas transforma essa
string pronta em imagem; ele não pode remontar chave, ambiente, CSC ou assinatura.

#### DANFE NFC-e — biblioteca ainda não selecionada

Para o backend atual, que roda em Docker Linux sobre .NET 8, nenhuma alternativa deve ser marcada
como aprovada antes de um *spike* reproduzível:

| Candidato open source | Situação | Motivo |
|---|---|---|
| `NFe.Danfe.Nativo` do repositório Zeus | candidato | existe no código-fonte, porém não está no pacote principal instalado; é necessário definir empacotamento, atualização e compatibilidade Linux |
| `Zeus.Net.NFe.NFCe.Danfe.OpenFastReport` + `FastReport.OpenSource` | candidato com ressalva | ambos possuem fonte aberta, mas a própria matriz do projeto não comprova essa combinação em .NET 8/Linux e há dependências de fontes/renderização |
| `Gerene.DFe.EscPos` | candidato para agente local de impressão | atende NFC-e térmica 58/80 mm, porém depende da família Hercules e de acesso local à impressora; não deve ser acoplado diretamente ao contêiner SaaS |
| `Zeus.Net.NFe.Danfe.Html` | não aprovado para este fim | o pacote e os templates auditados são apresentados para DANFE NF-e modelo 55; não há prova suficiente de DANFE NFC-e modelo 65 |
| `Zeus.Net.NFe.Danfe.QuestPdf` | fora do recorte estritamente open source | é multiplataforma, mas herda condições comerciais do QuestPDF; só poderia entrar após revisão formal de licença |
| FastReport pago/Skia pago | fora do recorte | contraria o requisito de stack exclusivamente open source |

O DANFE atual também não é aprovado por exclusão. Até escolher a biblioteca, DAN-001/002 continuam
P0: a saída deve nascer do XML autorizado ou do XML offline imutável e passar por comparação visual
e funcional com o Manual de Padrões Técnicos.

#### Prova obrigatória para escolher o renderizador

1. gerar DANFE de NFC-e normal e de contingência diretamente do XML;
2. executar dentro da mesma imagem Linux usada em produção;
3. validar 58 mm, 80 mm, muitos itens, acentos, descontos e pagamentos divididos;
4. validar QR Code v3, protocolo, chave, consumidor e mensagem de contingência;
5. reiniciar o contêiner e reproduzir byte a byte ou visualmente a mesma saída;
6. medir memória, tempo e dependências nativas/fontes;
7. revisar licença de todas as dependências transitivas;
8. obter aprovação visual e fiscal antes de substituir o cupom atual.

#### Controle de licenças

O `.nuspec` da versão instalada da Zeus não declara o campo SPDX de licença, embora a origem
indicada pelo pacote seja o repositório `ZeusAutomacao/DFe.NET`, classificado como LGPL-2.1. Antes
da distribuição, registrar o repositório, tag/commit, texto da licença, avisos e obrigações das
dependências. Não alterar ou incorporar código da biblioteca no ERP sem avaliar o efeito da LGPL.

**Conclusão revisada:** para emissão, permanecer com **Zeus + .NET/XSD oficial + QRCoder**. Para
DANFE, a Zeus **tem implementações no repositório**, mas o pacote instalado não as traz e ainda não
há opção aprovada para o ambiente Linux. A seleção depende do spike de DAN-001/002; não será feita
por suposição.

**Fontes:** [ZeusAutomacao/DFe.NET](https://github.com/ZeusAutomacao/DFe.NET),
[FastReport.OpenSource](https://github.com/FastReports/FastReport),
[Gerene.DFe.EscPos](https://www.nuget.org/packages/Gerene.DFe.EscPos) e
[QRCoder](https://github.com/codebude/QRCoder).

### 24.9 Execução do DANFE em Windows — opções de implantação

Uma imagem Windows pode executar o renderizador da Zeus, mas não dentro do host Docker Linux atual
como um contêiner comum. Contêineres compartilham o kernel do host: Windows Container exige host
Windows ou isolamento Hyper-V sobre infraestrutura Windows compatível. O `System.Drawing.Common`
usado por renderizadores legados é suportado oficialmente apenas em Windows nas versões atuais do
.NET, portanto adicionar `libgdiplus` ao contêiner Linux não é uma solução homologável para .NET 8.

#### Opção A — agente Windows na loja — recomendação principal

Instalar um serviço pequeno no computador Windows do PDV:

1. o servidor entrega o XML autorizado ou o XML offline imutável;
2. o agente valida a integridade e desserializa o XML;
3. a implementação de DANFE NFC-e da família Zeus renderiza o documento;
4. o agente imprime pela fila Windows ou envia ESC/POS à impressora térmica;
5. o agente devolve confirmação, erro e identificador da impressão ao ERP.

Vantagens: acesso real à impressora, menor latência, funcionamento durante queda de internet e
compatibilidade natural com GDI/fontes. O agente não deve receber certificado A1, senha ou CSC;
o QR Code completo deve vir do XML preparado pelo motor fiscal. Atualizações precisam ser
assinadas, versionadas e distribuídas por canal seguro.

#### Opção B — microserviço Windows central — alternativa aceitável

Manter a aplicação, banco e emissão no Linux e criar uma VM/VPS Windows separada exclusivamente
para renderização:

```text
API fiscal Linux → XML imutável → Renderizador Windows → PDF/PNG → API/PDV
```

Essa VM pode executar um Windows Container baseado em `servercore`, desde que o host Windows e a
imagem sejam compatíveis. Se a VPS Linux atual permitir virtualização aninhada, é possível hospedar
uma VM Windows nela; caso contrário, será necessário um segundo VPS/nó Windows. Também é exigida
licença válida do Windows host.

O microserviço deve ser sem estado e sem credenciais fiscais. Requisitos mínimos:

- aceitar somente XML de tamanho limitado e com parsing seguro contra entidades externas;
- autenticação de serviço, autorização por tenant e comunicação TLS;
- não registrar XML completo, CPF, certificado ou CSC em logs;
- gerar a saída somente a partir do XML recebido;
- devolver hash da entrada, versão do renderizador e hash da saída;
- limitar CPU, memória, concorrência e tempo de execução;
- possuir healthcheck, fila/retry e atualização mensal da imagem Windows;
- ter teste visual após atualização do host, da imagem ou da Zeus.

#### Opção C — trocar toda a VPS para Windows — não recomendada

É tecnicamente possível migrar todos os contêineres para um host Windows, mas isso amplia a mudança
para banco, proxy, volumes, backup e operação apenas para resolver a impressão. O benefício não
compensa o risco enquanto emissão, PostgreSQL e frontend já funcionam no ambiente Linux.

#### Decisão proposta

Para lojas físicas, realizar primeiro o spike do **agente Windows local**, pois ele resolve DANFE,
impressora e contingência no mesmo ponto. O microserviço Windows central fica como plano B para
geração de PDF, reimpressão e lojas sem agente. Em ambos os casos, DAN-001/002 só pode ser concluído
depois da comparação com o manual e da matriz normal/offline/cancelada.

**Fontes:** [requisitos oficiais de Windows Containers](https://learn.microsoft.com/en-us/virtualization/windowscontainers/deploy-containers/system-requirements),
[compatibilidade entre host e imagem Windows](https://learn.microsoft.com/en-us/virtualization/windowscontainers/deploy-containers/version-compatibility),
[licença das imagens Windows](https://learn.microsoft.com/en-us/virtualization/windowscontainers/images-eula) e
[restrição do System.Drawing.Common fora do Windows](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/6.0/system-drawing-common-windows-only).

### 24.10 Restrição confirmada — Hostinger KVM 8

O ambiente informado para produção é um VPS Hostinger KVM 8, atualmente anunciado com 8 vCPU,
32 GB de RAM e 400 GB NVMe. Os recursos seriam suficientes para um renderizador, mas a política da
infraestrutura impede a solução Windows dentro do mesmo VPS:

- a Hostinger declara que **não oferece virtualização aninhada** nos planos VPS;
- os templates disponibilizados são baseados em Linux;
- a Hostinger declara que não fornece Windows VPS;
- não é permitido enviar ou instalar uma imagem/ISO própria do sistema operacional.

Consequentemente, nessa hospedagem não é possível executar de forma suportada:

- uma VM Windows interna por KVM/Hyper-V;
- um host Windows Container dentro do Docker Linux;
- uma reinstalação do mesmo VPS com Windows Server por ISO própria.

Wine, emulação de GDI ou alterações não suportadas de `System.Drawing.Common` não devem ser usadas
para o DANFE fiscal de produção, pois acrescentariam uma camada sem suporte e difícil de reproduzir.

**Decisão para esse ambiente:** manter emissão, banco e frontend no KVM 8 Linux. Para o DANFE,
escolher entre:

1. agente Windows local no computador da loja — alternativa preferencial para impressão e
   contingência;
2. nó Windows externo em outro provedor — alternativa para renderização central de PDF/PNG;
3. renderizador comprovadamente multiplataforma e open source — somente após o spike DAN-001/002.

A opção de nó externo pode ser integrada como destino remoto privado do Docker atual, mas não
deve receber certificado, CSC ou autoridade de emissão: somente XML imutável para renderização.

**Fontes:** [Hostinger — virtualização aninhada não suportada](https://www.hostinger.com/support/10429687-is-nested-virtualization-supported-in-hostinger/),
[Hostinger — ausência de Windows VPS](https://www.hostinger.com/br/support/1583760-voces-fornecem-hospedagem-windows-na-hostinger/),
[Hostinger — imagens próprias não suportadas](https://www.hostinger.com/support/8852324-can-you-add-your-own-os-image-to-vps-at-hostinger/) e
[especificações atuais do KVM 8](https://www.hostinger.com/br/servidor-vps).

### 24.11 Renderizador Linux open source — seleção e homologação técnica em 04/08/2026

Esta seção **acrescenta e atualiza a decisão** das seções 24.8 a 24.10 sem apagar o histórico da
análise. Foi encontrado um renderizador de DANFE NFC-e que roda de forma nativa no Linux e não
depende de `System.Drawing.Common`, Windows, Wine ou virtualização aninhada:

> **Selecionado com aprovação técnica condicionada:** `nfephp-org/sped-da`, classe
> `NFePHP\DA\NFe\Danfce`, executada em um contêiner Linux separado e consumida pela API .NET 8
> por HTTP interno.

O `sped-da` **não substitui a Zeus**. A divisão de responsabilidade aprovada é:

```text
.NET 8 + Zeus -> monta, assina, transmite e persiste o XML
                         |
                         v
sidecar Linux NFePHP -> lê o XML imutável e devolve o PDF do DANFE NFC-e
```

Assim, não haverá dois motores de emissão nem recálculo fiscal no PHP. O sidecar não receberá
certificado A1, senha, CSC, regras tributárias ou autoridade para transmitir documentos.

#### Por que este candidato venceu

| Critério | Resultado observado |
|---|---|
| NFC-e modelo 65 | a classe `Danfce` rejeita XML cujo campo `mod` não seja `65` |
| Entrada por XML | o construtor recebe diretamente a string do XML; não exige o modelo de objetos de outra biblioteca |
| Linux | imagem construída e executada com sucesso sobre `php:8.4-cli-bookworm` |
| Papel térmico | PDF válido gerado em 58 mm e 80 mm |
| QR Code | QR normal, homologação, contingência v2 e QR v3 foram renderizados; os payloads foram decodificados com sucesso |
| Contingência | o documento exibiu a mensagem de contingência e a ausência do protocolo conforme o cenário de teste |
| Cancelamento | há tratamento no código para protocolo/evento de cancelamento, mas ainda falta ensaio com XML real do ERP |
| Open source | classe `Danfce` declara LGPL v3; o pacote Composer lista LGPL/GPL/MIT e exige inventário jurídico antes da distribuição |
| Manutenção | repositório ativo, com último commit ensaiado em 31/07/2026; mais de 1 milhão de instalações no Packagist |
| Dependências nativas | PHP com `dom`, `gd`, `mbstring` e `soap`; código de barras por `tecnickcom/tc-lib-barcode` |

Os concorrentes não foram selecionados neste momento:

- `@nfewizard/danfe`: suporta NFC-e e Linux/Node, mas a API documentada espera o objeto completo
  produzido pelo ecossistema NFeWizard, é um pacote muito mais novo e usa GPL-3.0;
- `BrazilFiscalReport`: bom projeto Python/Linux, porém a documentação atual comprova DANFE da
  NF-e modelo 55, não DANFE NFC-e modelo 65;
- renderizadores Zeus/FastReport baseados em Windows: permanecem incompatíveis com o host Linux
  da KVM 8 nas condições já registradas;
- QuestPDF: permanece fora do recorte estritamente open source definido para este projeto.

#### Ensaio reproduzido

O ensaio usou o commit `0bda76fdbcc37a61d3b94777cc36238f13a2c8af` do `sped-da`, imagem Debian
Bookworm com PHP 8.4 e as extensões declaradas. Foram gerados seis documentos:

| Cenário | Resultado |
|---|---|
| produção normal, 80 mm | PDF 1.3, uma página, 80 mm, QR legível |
| ambiente de homologação, 80 mm | PDF gerado e QR legível; marca d'água invade os totalizadores e exige correção visual |
| contingência offline, 80 mm | PDF gerado, mensagem de contingência presente e QR legível |
| QR Code versão 3, 80 mm | PDF gerado; decodificação devolveu exatamente o payload v3 fornecido no XML |
| XML com grupo sintético `IBSCBSTot` | PDF gerado sem falha; os grupos foram tolerados, mas não são impressos especificamente pela classe |
| produção normal, 58 mm | PDF 1.3, uma página, 58 mm, sem corte visual no ensaio digital |

Observações medidas no ambiente de desenvolvimento:

- pico reportado pelo processo PHP ao gerar os seis PDFs em sequência: aproximadamente 6 MiB;
- duas execuções produziram PDFs com hashes binários diferentes, mas imagens renderizadas com o
  mesmo SHA-256; portanto, a saída é visualmente determinística, não byte a byte;
- PHP 8.4 exibiu avisos de depreciação na dependência `tc-lib-barcode` durante a criação do QR Code.
  Em produção, `display_errors` deve permanecer desligado para nunca contaminar a resposta PDF,
  mas os avisos precisam ser corrigidos ou eliminados por versão/fork antes do aceite final;
- o Poppler informou fontes lógicas `Symbol`/`ArialUnicode` ausentes durante a rasterização local,
  embora os textos e acentos tenham aparecido corretamente. As fontes da imagem final precisam ser
  fixadas e testadas na mesma imagem que será publicada.

O ensaio com grupo IBS/CBS comprova compatibilidade estrutural — o parser não quebra com as novas
tags —, mas **não comprova exibição tributária específica**. A NT 2025.002 altera o XML da NF-e/NFC-e;
o manual oficial do DANFE NFC-e versão 6.0 continua sendo a base visual vigente localizada nesta
avaliação. Toda nova versão do manual ou Nota Técnica que determine impressão adicional deve abrir
um teste e, se necessário, um patch no fork do renderizador.

#### Status correto da homologação

O candidato está **aprovado para implementação do sidecar e homologação integrada**, mas ainda não
está liberado para produção. Não confundir “gerou PDF em Linux” com aceite fiscal e operacional.

Para converter a aprovação condicionada em `DAN-001/DAN-002 concluído`, executar:

1. criar fork interno do `sped-da` e fixar commit, `composer.lock`, imagem base por digest e textos
   das licenças; não consumir `dev-master` flutuante;
2. encapsular apenas `Danfce($xml)->render()` em `POST /render/danfce`, sem acesso externo e sem
   persistência própria;
3. limitar XML e log: tamanho máximo, tempo, memória, concorrência, proibição de DTD/entidades
   externas, autenticação de serviço e nenhuma gravação de CPF/XML completo;
4. enviar ao sidecar o XML autorizado (`nfeProc`) ou o XML offline imutável; nunca reconstruir o
   DANFE a partir de DTO de venda ou banco relacional;
5. testar XMLs reais gerados pela Zeus deste ERP: produção/homologação, Simples Nacional, cashback,
   crediário, pagamentos mistos, desconto, frete, troco, consumidor identificado e sem identificação;
6. acrescentar matriz com QR v3 online e offline real, cancelamento, rejeição, contingência, 1/40/200
   itens, acentos, nomes longos e ausência de logo;
7. corrigir a sobreposição da marca d'água de homologação e os avisos da biblioteca de QR Code;
8. imprimir fisicamente em pelo menos uma impressora 58 mm e uma 80 mm e ler o QR com dois celulares;
9. comparar campo a campo com o Manual de Padrões Técnicos do DANFE NFC-e v6.0 e obter aceite fiscal
   documentado; a autorização da SEFAZ não valida o desenho do PDF;
10. executar carga, timeout e reinício do sidecar; a falha de PDF não pode desfazer, duplicar ou
    retransmitir uma NFC-e já autorizada;
11. guardar no registro da emissão o hash do XML de entrada, versão/commit do renderizador e hash do
    PDF entregue; não usar igualdade binária entre duas reimpressões como regra de consistência;
12. manter a opção de agente local somente para envio físico à impressora. A geração central do PDF
    deixa de exigir Windows, VM ou outro provedor.

#### Versões e política de atualização

O último pacote estável visível no Packagist é `v1.1.6`, publicado em 2024, enquanto o repositório
`master` recebeu alterações em 2026. Isso impede usar simplesmente `composer require` sem decisão:
o projeto deve auditar o diff, criar um fork interno, aplicar somente os patches necessários e
publicar uma imagem imutável. Atualização de PHP, `sped-da`, `sped-common`, `tc-lib-barcode`, fontes
ou imagem Debian exige repetir snapshots visuais, leitura do QR e matriz de XMLs.

**Decisão final desta rodada:** abandonar a necessidade de Windows para gerar PDF. Prosseguir com
**NFePHP/sped-da em sidecar Linux**, mantendo Zeus no .NET 8 como única biblioteca de emissão. O
go-live continua bloqueado somente até concluir a homologação integrada com XMLs reais do ERP,
corrigir as duas falhas técnicas registradas e obter o aceite visual/fiscal.

**Fontes:** [código da classe `Danfce`](https://github.com/nfephp-org/sped-da/blob/master/src/NFe/Danfce.php),
[repositório oficial `sped-da`](https://github.com/nfephp-org/sped-da),
[metadados e estatísticas no Packagist](https://packagist.org/packages/nfephp-org/sped-da),
[Manual oficial do DANFE NFC-e v6.0](https://www.nfe.fazenda.gov.br/portal/exibirArquivo.aspx?AspxAutoDetectCookieSupport=1&conteudo=k%2FIuuaW4YiY%3D),
[NT 2025.001 — QR Code v3](https://www.nfe.fazenda.gov.br/Portal/exibirArquivo.aspx?conteudo=NvuzQGYd6E8%3D),
[Portal Nacional — NT 2025.002 e atualizações RTC](https://www.nfe.fazenda.gov.br/portal/consulta.aspx/listaConteudo.aspx?AspxAutoDetectCookieSupport=1&tipoConteudo=04BIflQt1aY%3D),
[`@nfewizard/danfe`](https://www.npmjs.com/package/@nfewizard/danfe) e
[`BrazilFiscalReport`](https://engenere.github.io/BrazilFiscalReport/pt/).

### 24.12 Verificação das alternativas sugeridas por pesquisa genérica

Uma resposta de mecanismo de busca indicou ACBrLib, Zeus Automação e PyNFe/PySIGNFe como
renderizadores open source para Linux. A verificação nas fontes oficiais mostra que a afirmação é
parcial e não altera automaticamente a decisão da seção 24.11:

| Alternativa | O que foi confirmado | Limitação para este projeto | Decisão |
|---|---|---|---|
| ACBrLibNFe | carrega XML de NF-e/NFC-e, gera PDF e possui `.so` para Linux e interfaces C# | a distribuição Linux compilada e atualizada faz parte do ACBr Pro pago; impressão em Linux exige Xvfb, GTK2 e fontes; compilar o código-fonte internamente cria uma nova cadeia Pascal/Lazarus a manter | plano B para spike, não substituição imediata |
| Zeus + FastReport OpenSource | gera DANFE e possui projeto NFC-e | a própria matriz oficial declara FastReport OpenSource em `.NET 8+` como **Windows apenas**; o suporte Linux informado é para .NET 6 | reprovado no ambiente atual `.NET 8/Linux` |
| Zeus `NFe.Danfe.Nativo` | existe no fonte e compila para `net8.0` | inspeção do projeto encontrou `System.Drawing.Common`, `System.Drawing.Printing`, `Graphics`, `Bitmap`, `PrintDocument` e binding Windows do ZXing; compilar para `net8.0` não significa suporte de execução em Linux | reprovado no Linux/.NET 8 sem reescrita gráfica |
| PyNFe | emite NF-e/NFC-e 4.00 e funciona em Python/Linux | não possui renderizador DANFCE próprio; a impressão é delegada ao `BrazilFiscalReport`, cuja documentação atual apresenta DANFE modelo 55 e DAMDFE, não DANFE NFC-e modelo 65 | reprovado como renderizador NFC-e |
| PySIGNFe | nenhum projeto fiscal autoritativo com esse nome foi localizado no GitHub/PyPI | nome possivelmente incorreto ou conteúdo gerado pela pesquisa; não há pacote, licença, API e testes auditáveis | não considerar até existir URL verificável |

O ACBr é uma alternativa real, mas não é “grátis e pronto” nas mesmas condições do NFePHP. Ele deve
ser comparado somente se o projeto aceitar pagar pelo ACBr Pro ou assumir a compilação e manutenção
do código Pascal/Lazarus, além de executar uma pilha gráfica virtual dentro do contêiner. Em ambos
os casos será obrigatório repetir a mesma matriz de QR v3, contingência, 58/80 mm, IBS/CBS, fontes,
carga e licença realizada para o `sped-da`.

Na Zeus, o ponto decisivo é a diferença entre **target framework** e **runtime suportado**. O projeto
`NFe.Danfe.Nativo` declara `net8.0`, mas usa APIs que a Microsoft restringe a Windows. O README da
própria família Hercules/Zeus também limita FastReport OpenSource em .NET 8 a Windows. Portanto,
essa opção não deve ser colocada no Docker Linux apenas porque o projeto compila.

**Conclusão:** a pesquisa trouxe o ACBrLib como plano B válido, mas as descrições de Zeus e PyNFe
generalizaram capacidades que não atendem à combinação concreta **DANFE NFC-e + Linux + .NET 8 +
stack pronta estritamente open source**. Permanece a decisão de implementar primeiro o sidecar
NFePHP já ensaiado; ACBr entra na matriz de contingência tecnológica, não no caminho crítico.

**Fontes:** [ACBrLibNFe — produto e acesso ACBr Pro](https://projetoacbr.com.br/pro/downloads/acbrlibnfe/),
[ACBrLib — dependências Linux/Xvfb/GTK2](https://acbr.sourceforge.io/ACBrLib/ComoInstalarDistribuir.html),
[ACBrLib — geração de PDF](https://acbr.sourceforge.io/ACBrLib/NFE_ImprimirPDF.html),
[matriz oficial Hercules/Zeus](https://github.com/Hercules-NET/ZeusFiscal),
[PyNFe — dependência de impressão](https://github.com/TadaSoftware/PyNFe) e
[restrição oficial de `System.Drawing.Common` fora do Windows](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/6.0/system-drawing-common-windows-only).

---

## 25. Revisão de escopo do DANFE — separação entre requisito fiscal e requisito operacional — 04/08/2026

Esta seção **não invalida** 24.8, 24.9 e 24.10. A pesquisa registrada ali está correta e permanece
válida. O que se revisa aqui é a **premissa** que a originou: a de que o DANFE NFC-e precisa ser
renderizado como imagem ou PDF no servidor. A inspeção do código mostra que a renderização já
ocorre no navegador do PDV, e que a pendência real de DAN-001 é de origem do dado, não de
tecnologia de desenho.

### 25.1 Estado verificado do cupom atual

| Evidência | Arquivo | Consequência |
|---|---|---|
| Cupom renderizado no cliente, em HTML | `frontend/app/admin/fiscal/cupom/[id]/page.tsx` | nenhum pixel é desenhado no servidor |
| QR Code gerado no navegador a partir da URL fiscal montada pela Zeus | mesma página, via lib `qrcode` | o conteúdo e a assinatura continuam vindo do motor fiscal |
| Impressão por `window.print()` com `@media print` | mesma página e `app/globals.css` | a fila de impressão usada é a do sistema operacional da loja |
| `CupomDto` montado a partir do cadastro atual | `CardGameStore/Services/Interfaces/INfceEmissionService.cs:55` | **é aqui que mora o defeito de DAN-001** |

O PDV é uma aplicação web executada no computador da loja, normalmente Windows, com a impressora
já instalada no sistema operacional. A renderização, portanto, já acontece em ambiente compatível,
sem depender do contêiner Linux.

### 25.2 Dois problemas distintos, hoje tratados como um

| | Natureza | Onde se resolve | Bloqueia go-live? |
|---|---|---|---|
| **A — DANFE não deriva do XML** | fiscal: a via impressa pode divergir do documento autorizado | backend Linux, desserialização do XML para um DTO imutável | **sim — é o P0 real de DAN-001** |
| **B — impressão sem diálogo, corte de papel e gaveta** | operacional: conforto e velocidade de caixa | agente local ESC/POS na estação | não |

O encadeamento de 24.9 e 24.10 decorre de tratar **B como pré-requisito de A**. Separados, o
requisito fiscal deixa de depender de renderizador, de host Windows e de licença de sistema
operacional; e o agente local passa a ser melhoria de operação, priorizável depois do piloto.

O Manual de Padrões Técnicos do DANFE NFC-e define **divisões e conteúdo mínimo**, não tecnologia
de renderização. HTML com `@page` em milímetros atende ao formato de bobina, e a própria disciplina
da NFC-e admite entrega por meio eletrônico ou simples exibição do QR Code ao consumidor.

### 25.3 DAN-001 — escopo revisado

O cartão deixa de exigir escolha de biblioteca de renderização e passa a exigir **origem única e
imutável do dado**:

1. desserializar `nfeProc` quando autorizado, ou o XML assinado offline quando em contingência;
2. montar um DTO fiscal imutável exclusivamente a partir desse XML — emitente, itens, tributos,
   totais, pagamentos, consumidor, chave, protocolo, `tpEmis` e QR Code;
3. nunca completar campo ausente com o cadastro atual: o que não estiver no XML não vai ao DANFE;
4. usar o mesmo DTO para admin, cliente e reimpressão;
5. diferenciar visualmente normal, contingência e cancelada;
6. impedir que documento rejeitado ou pendente seja apresentado como DANFE válido;
7. cobrir as divisões obrigatórias do Manual vigente.

**Aceite de DAN-001 (inalterado em rigor, agora verificável sem infraestrutura nova):** alterar
razão social, endereço ou cadastro de produto após a emissão não altera a reimpressão. O teste é um
snapshot do DTO derivado de um XML fixo.

**Custo de dependências:** nenhuma biblioteca nova. `System.Drawing.Common`, host Windows,
virtualização aninhada e licença de Windows Server deixam de ser bloqueios de DAN-001.

### 25.4 Reposicionamento de 24.9 e 24.10

As duas seções passam a ser **anexo de decisão de infraestrutura**, aplicável apenas se surgir a
necessidade de renderização server-side — por exemplo, envio de PDF por e-mail em lote, geração
assíncrona para o contador ou reimpressão fora da estação de venda. Não são pré-requisito do
recorte certificado.

Permanecem integralmente válidos como pesquisa registrada:

- `System.Drawing.Common` é suportado apenas em Windows nas versões atuais do .NET;
- a hospedagem contratada não oferece virtualização aninhada, Windows VPS nem imagem própria;
- QuestPDF possui condições de licenciamento que exigem revisão formal antes de qualquer adoção;
- nenhum renderizador deve ser aprovado sem spike reproduzível na imagem de produção.

### 25.5 Cartão novo — PDV-001, operação com conectividade instável

A análise do DANFE expôs uma limitação que nenhum agente de impressão resolve e que hoje não tem
cartão: **a contingência offline da NFC-e pressupõe que a SEFAZ está inacessível, não que a loja
está.** Sendo o PDV uma aplicação web, a queda do enlace da loja interrompe a venda antes de
qualquer discussão sobre documento fiscal.

| ID | Frente | Prioridade | Dependências | Status |
|---|---|---:|---|---|
| PDV-001 | Operação de caixa com conectividade instável | P1 / condicional ao mercado-alvo | FIS-001 | Pendente |

Escopo a decidir com o negócio, não apenas com o código:

1. definir se a proposta de valor inclui vender com o enlace da loja fora do ar;
2. se incluir, avaliar PWA com fila local — o projeto já possui `manifest.ts` e instalação PWA —
   ou agente local com capacidade de operação autônoma;
3. definir onde ficam numeração, contingência e reconciliação nesse modo;
4. definir o limite honesto de suporte e declará-lo no material comercial.

Este é o argumento efetivo a favor de um agente local: **operar**, não imprimir. Enquanto PDV-001
não for decidido, o material comercial não deve sugerir funcionamento offline.

### 25.6 Ajustes a consolidar no quadro geral

Ao consolidar esta revisão na seção 3, aplicar:

| ID | Ajuste |
|---|---|
| DAN-001 | mantém P0; escopo passa a ser origem do dado (XML), sem dependência de renderizador |
| DAN-002 | mantém P0; a verificação visual/física continua exigida sobre a saída efetivamente impressa |
| PDV-001 | incluir como cartão novo, P1 condicional |
| — | 24.9 e 24.10 passam a anexo de infraestrutura, superadas por 24.11 quanto à necessidade de Windows |

**Fontes:** [Manual de Padrões Técnicos do DANFE NFC-e e QR Code](https://www.nfe.fazenda.gov.br/PORTAl/exibirArquivo.aspx?conteudo=k%2FIuuaW4YiY%3D)
e evidências de código citadas em 25.1.

### 25.7 Reconciliação com 24.11 — quantas representações do DANFE vamos manter

As seções 24.11 e 25 foram escritas em paralelo e **convergem no ponto essencial**: a via entregue
ao consumidor deve nascer do XML imutável. O item 4 do roteiro de 24.11 — “nunca reconstruir o
DANFE a partir de DTO de venda ou banco relacional” — é a mesma exigência de 25.3.

A divergência que sobra não é de fonte, e sim de **canal de saída**:

| Canal | Origem | Onde executa | Estado |
|---|---|---|---|
| HTML impresso pelo navegador do PDV | hoje, `CupomDto` do cadastro | estação da loja | existe e funciona, mas **viola a origem única** |
| PDF do sidecar NFePHP | XML autorizado ou offline | contêiner Linux | aprovado condicionalmente em 24.11 |

Manter os dois canais recria exatamente o risco que 24.8 alerta: **duas fontes fiscais que podem
divergir**. A decisão precisa ser tomada antes de implementar, porque muda o escopo de DAN-001.

#### Opções

| | Descrição | A favor | Contra |
|---|---|---|---|
| **A** | Sidecar como fonte única: o PDV exibe/imprime o PDF gerado do XML | uma única representação; aceite fiscal feito uma vez | põe o sidecar no caminho crítico da venda; impressão de PDF ainda passa pelo diálogo do navegador |
| **B** | HTML no PDV + PDF no sidecar para reimpressão, e-mail e contador | caixa rápido e sem dependência externa | duas representações a homologar e manter em paridade |
| **C** | HTML no PDV derivado do XML; sidecar só para arquivo/envio | remove o sidecar do caminho da venda | continua sendo duas representações, com o mesmo custo de paridade |

**Recomendação:** adotar **A**, com a falha do sidecar tratada como não bloqueante da venda — a NFC-e
já está autorizada e o documento pode ser reimpresso depois, conforme o item 10 de 24.11. Uma única
representação é substancialmente mais defensável em fiscalização, e o aceite visual/fiscal é
executado uma vez só. Se a latência medida em caixa inviabilizar A, migrar para C — nunca para B
sem um teste de paridade automatizado entre as duas saídas.

#### Consequências para 25.3

Confirmada a opção A, o escopo de DAN-001 fica:

1. persistir e recuperar o XML imutável (`nfeProc` ou offline) — **inalterado, continua sendo o P0**;
2. encaminhar esse XML ao sidecar e devolver o PDF ao PDV;
3. registrar hash do XML de entrada, versão/commit do renderizador e hash do PDF, conforme item 11
   de 24.11;
4. aposentar o `CupomDto` derivado do cadastro, evitando que a representação antiga sobreviva como
   caminho alternativo silencioso.

O ponto 1 não depende da escolha entre A, B ou C, **não depende do sidecar e pode começar agora**.
É a única parte de DAN-001 que é pré-requisito de todas as opções.

#### Ajuste de rigor herdado de 24.8

O ensaio de 24.11 mediu que duas execuções geram PDFs com hashes binários distintos, embora
visualmente idênticos. Isso torna a prova obrigatória nº 5 de 24.8 — “reproduzir byte a byte” —
inatingível e incorreta como critério. Prevalece a formulação do item 11 de 24.11: comparar
**renderização**, não bytes. Registrar a correção para que o critério antigo não seja cobrado em
auditoria futura.

#### Pendências que 24.11 deixou explícitas e seguem abertas

Nenhuma delas impede começar o ponto 1 acima:

- cancelamento ainda sem ensaio com XML real deste ERP;
- marca d'água de homologação sobrepondo os totalizadores;
- avisos de depreciação do `tc-lib-barcode` em PHP 8.4;
- fontes lógicas ausentes na rasterização, a fixar na imagem publicada;
- inventário jurídico de LGPL v3 do `sped-da` e das dependências antes da distribuição.

**Aceite desta reconciliação:** decisão A/B/C registrada com responsável e data; `CupomDto` derivado
do cadastro marcado para remoção; critério de comparação visual substituindo o de igualdade binária
em 24.8.

## 26. Handoff para a PR de planejamento — adequação do cupom atual ao DANFE NFC-e

Esta seção registra de forma executável a avaliação visual feita sobre a tela atual em
`frontend/app/admin/fiscal/cupom/[id]/page.tsx`. Ela não remove nem substitui as decisões das
seções 24 e 25. Seu objetivo é impedir que a aparência funcional do cupom seja confundida com
homologação integral do DANFE NFC-e.

### 26.1 Conclusão objetiva

O documento exibido atualmente é uma **representação HTML simplificada da NFC-e**. Ele já contém
elementos essenciais e pode apresentar chave e QR Code verdadeiros, mas ainda não deve ser
declarado como DANFE NFC-e integralmente conforme o manual.

A validade fiscal pertence ao XML autorizado pela SEFAZ. O DANFE é sua representação auxiliar.
Uma autorização em homologação comprova a aceitação técnica do XML naquele ambiente; não aprova
automaticamente a fidelidade e a completude da representação impressa.

### 26.2 O que já existe na tela atual

| Elemento | Estado observado | Observação |
|---|---|---|
| identificação básica do emitente | existe | razão social, CNPJ e endereço vêm da configuração atual |
| itens | parcial | apresenta descrição, quantidade, preço unitário, subtotal e tributo aproximado |
| totais | parcial | total e desconto são apresentados |
| pagamento | parcial | existe apenas uma descrição agregada, sem a composição completa por meio e valor |
| identificação da NFC-e | existe | série, número, data, protocolo e chave, quando disponíveis |
| QR Code | existe | imagem gerada no navegador a partir da URL fiscal persistida |
| impressão | existe | HTML, `window.print()` e impressora instalada no computador da loja |
| transparência tributária | existe com ressalvas | valores e fonte precisam continuar vinculados ao snapshot/XML aplicável |

### 26.3 Não conformidades e verificações obrigatórias

| ID | Verificação/defeito | Motivo | Ação de implementação | Evidência de aceite |
|---|---|---|---|---|
| DFE-001 | representação reconstruída do cadastro e da venda | uma reimpressão pode divergir do XML autorizado depois de alterações cadastrais | desserializar `nfeProc` autorizado ou XML assinado de contingência e criar DTO fiscal imutável | alterar empresa/produto depois da emissão não muda a reimpressão |
| DFE-002 | divisão de itens simplificada | o manual define conteúdo e organização mínimos para código, descrição, quantidade, unidade, valor unitário e valor total | mapear exclusivamente as tags correspondentes do XML e adequar a apresentação | comparação campo a campo com XML fixo e manual vigente |
| DFE-003 | pagamentos agregados | vendas com pagamentos mistos, crediário, cashback, fidelidade e troco podem ser representadas incorretamente | ler todos os grupos `detPag`, respectivos `tPag`, valores e `vTroco` do XML | cenários unitário, misto, crediário, cashback e troco conferidos |
| DFE-004 | divisão do consumidor ausente | o DANFE deve representar o destinatário informado ou a indicação de consumidor não identificado, conforme o XML e o manual | renderizar CPF/CNPJ/identificação/nome/endereço somente quando presentes e a mensagem aplicável quando ausentes | testes com consumidor identificado e não identificado |
| DFE-005 | identificação de homologação incompleta | o DANFE de `tpAmb=2` exige aviso centralizado, em caixa alta, de ambiente de homologação e ausência de valor fiscal | derivar o ambiente do XML e renderizar a mensagem obrigatória, sem confundi-la com o `xProd` especial do primeiro item | impressão em homologação contém ambos os tratamentos aplicáveis e permanece legível |
| DFE-006 | contingência precisa de leiaute próprio | a via deve refletir o XML offline, momento, justificativa, tipo de emissão e marcações previstas | criar estado visual específico a partir de `tpEmis`, `dhCont` e `xJust`; tratar a via do estabelecimento conforme disciplina aplicável | ensaio normal, offline antes da autorização e reimpressão após autorização |
| DFE-007 | cancelada/rejeitada/pendente | um documento sem autorização não pode parecer DANFE autorizado; cancelamento precisa ser inequívoco | definir política por estado e bloquear o caminho fiscal válido para rejeitada/pendente | matriz de estados automatizada e conferência visual |
| DFE-008 | mensagens adicionais | texto institucional não pode substituir conteúdo fiscal e não deve parecer originado do XML quando não for | colocar “Documento emitido pelo sistema” somente após o encerramento do DANFE e separar visualmente | inspeção visual e comparação com `infCpl` |
| DFE-009 | QR e consulta | imagem legível não prova que URL, versão e parâmetros estão corretos | usar a URL do XML/artefato fiscal, validar tamanho físico e decodificar o QR impresso | leitura por dois aparelhos e consulta no ambiente correto |
| DFE-010 | reforma tributária | novos grupos do XML não devem ser inventados pelo frontend nem omitidos silenciosamente quando passarem a integrar a representação exigida | manter parser versionado e testes com XML contendo IBS/CBS; acompanhar notas técnicas e versão do manual | fixtures antes/depois da reforma e matriz por vigência |

### 26.4 Decisão de canal sem ambiguidade

A PR de planejamento deve registrar explicitamente uma escolha entre A, B ou C da seção 25.7.
Enquanto a escolha não estiver registrada, é permitido implementar o requisito comum e
independente: **persistir, recuperar, validar e desserializar o XML imutável**.

Se a opção A for confirmada, o PDF do sidecar `nfephp-org/sped-da` será a representação única e o
cupom HTML atual será aposentado como documento fiscal. Se a opção C for confirmada, o HTML poderá
continuar, mas deverá ser reconstruído integralmente a partir do XML e homologado visualmente; o
sidecar ficará restrito a arquivo/envio. A opção B exige paridade automatizada entre duas saídas e
não é recomendada como padrão.

### 26.5 Ordem segura de implementação para o agente de código

1. criar fixtures anonimizadas de `nfeProc` normal, homologação, pagamentos mistos, consumidor
   identificado, consumidor não identificado, cashback/crediário e IBS/CBS;
2. criar fixture de XML assinado em contingência e do mesmo documento posteriormente autorizado;
3. implementar parser versionado de XML para DTO fiscal imutável, sem consultar cadastro, venda ou
   configuração para completar conteúdo;
4. escrever testes de snapshot do DTO e de invariância após alteração cadastral;
5. registrar a decisão A/B/C da seção 25.7;
6. implementar a representação escolhida cobrindo DFE-002 a DFE-010;
7. validar QR Code, ambiente, pagamentos, consumidor, contingência e estados do documento;
8. imprimir em bobinas de 58 mm e 80 mm nas impressoras reais do piloto;
9. comparar campo a campo XML × representação × venda × pagamento, sem usar a venda como fonte do
   DANFE;
10. obter aceite técnico e fiscal documentado antes de substituir ou renomear o cupom como DANFE
    NFC-e em produção.

### 26.6 Limites para execução autônoma na nuvem

O agente pode implementar parser, DTO, fixtures sintéticas, testes e protótipo de renderização. Não
deve, sem revisão humana:

- usar XML, certificado, CSC, CNPJ, CPF ou dados reais em fixture ou commit;
- alterar a decisão A/B/C silenciosamente;
- declarar conformidade fiscal apenas porque testes automatizados passaram;
- remover o cupom atual antes de existir caminho de impressão substituto validado;
- publicar imagens de contêiner ou dependências sem o inventário de licença indicado em 24.11;
- promover para produção sem ensaio em homologação, impressão física e aceite registrados.

### 26.7 Base normativa e rastreabilidade

- [Portal oficial — histórico e Manual de Padrões Técnicos do DANFE NFC-e e QR Code v6.0, março de 2025](https://www.nfe.fazenda.gov.br/portal/listaHistorico.aspx?tipoConteudo=Ef+Y1blZDbU%3D);
- código da representação atual: `frontend/app/admin/fiscal/cupom/[id]/page.tsx`;
- montagem atual do DTO: `CardGameStore/Services/Implementations/NfceEmissionService.cs`, método
  `ObterCupomAsync`;
- contrato atual: `CardGameStore/Services/Interfaces/INfceEmissionService.cs`, `CupomDto`;
- decisões e ensaios de bibliotecas: seções 24.8 a 24.12;
- revisão de arquitetura e escolha de canal: seção 25.

**Definition of Done do DANFE:** representação gerada somente de artefato XML imutável; todas as
divisões aplicáveis conferidas; QR decodificado e consultável; estados normal, homologação,
contingência e cancelamento ensaiados; impressão física aprovada; divergência zero na comparação
campo a campo; aceite técnico e fiscal anexado à PR.

---

## 27. Decisão de canal registrada — opção C, e execução dos passos 1 a 4 — 04/08/2026

### 27.1 Decisão

A escolha exigida pela seção 26.4 está tomada e registrada:

> **Opção C — HTML impresso pelo navegador, integralmente alimentado pelo XML fiscal.**
>
> Decidida por: Eduardo Taino (responsável pelo produto), em 04/08/2026.
> Alvo de lançamento: 10/08/2026.

Arquitetura confirmada:

```text
XML autorizado (nfeProc) ou XML assinado offline
    → parser versionado no backend
    → DTO fiscal imutável
    → HTML no padrão do Manual do DANFE NFC-e
    → window.print()
```

**Correção conceitual que motivou a decisão:** o XML autorizado **já está no padrão da SEFAZ**. Não
há nada a corrigir nele. O trabalho é fazer a representação ler esse XML e apresentá-lo no padrão
visual do manual. O problema nunca foi de emissão — foi de origem do dado na tela.

O `nfephp-org/sped-da` **permanece documentado e aprovado condicionalmente** em 24.11, fora do
caminho crítico do caixa, como alternativa futura para PDF em lote, envio por e-mail e entrega ao
contador. A decisão C não descarta o spike; apenas não o coloca entre a venda e o cupom.

Justificativa comparativa:

| Caminho | Trabalho | Infraestrutura nova | Indicação |
|---|---|---|---|
| **XML → DTO → HTML atual** | menor | nenhuma | **adotado agora** |
| XML → NFePHP → PDF | médio/alto | sidecar PHP em Docker | útil depois, para PDF/e-mail/arquivo |
| Zeus / FastReport | alto | Windows ou limitação em Linux | não indicado |
| Renderizador próprio | muito alto | variável | desnecessário |

**Regra de fronteira mantida:** o React **não** monta o DANFE a partir de XML bruto. O backend
transforma XML em DTO imutável e o frontend consome apenas esse DTO. Isso preserva fonte única para
admin, cliente e reimpressão, mantém o frontend simples, torna o teste possível com XML fixo e
organiza a futura adequação a IBS/CBS num único ponto.

### 27.2 Passos 1 a 4 da seção 26.5 — concluídos

Executados antes da decisão por serem comuns a A, B e C, conforme autorizado em 26.4.

| Passo | Entrega | Arquivo |
|---|---|---|
| 1 e 2 | seis fixtures sintéticas de `nfeProc` e de XML offline | `tests/unit/CardGameStore.Tests/Fixtures/Nfce/` |
| 3 | DTO fiscal imutável | `CardGameStore/DTOs/DanfeFiscalDtos.cs` |
| 3 | parser versionado, sem acesso a banco/cadastro/configuração | `CardGameStore/Services/Implementations/DanfeXmlParser.cs` |
| 4 | 25 testes, incluindo invariância e entradas hostis | `tests/unit/CardGameStore.Tests/Services/DanfeXmlParserTests.cs` |

Cenários cobertos pelas fixtures: produção autorizada, homologação com o `xProd` obrigatório,
pagamentos mistos (Pix + crediário `05` + cashback `19`) com consumidor identificado, desconto e
troco, contingência offline sem protocolo, o mesmo documento autorizado depois, e documento com
grupos IBS/CBS.

Conformidade com 26.6 — limites de execução autônoma:

- nenhum dado real: CNPJ `00000000000191`, CPF `00000000191`, chaves iniciadas em UF `99`
  (inexistente), protocolos fora de faixa real; convenções registradas em `Fixtures/Nfce/LEIA-ME.md`;
- nenhum certificado, CSC ou segredo em fixture ou commit;
- nenhuma alteração no cupom atual, que segue funcionando como está;
- nada promovido para produção.

### 27.3 O que os testes já garantem

| Garantia | Como é verificada |
|---|---|
| **DFE-001** — representação é função pura do XML | o mesmo XML produz DTOs equivalentes; reformatação de espaços não altera conteúdo fiscal |
| **DFE-002** — item completo | código, descrição, NCM, CFOP, unidade, quantidade, unitário, total e tributo aproximado lidos do XML |
| **DFE-003** — pagamentos | três `detPag` preservados individualmente, com `xPag` e `vTroco`; o parser não traduz o `tPag` |
| **DFE-004** — consumidor | ausência do grupo `dest` vira “não identificado” explícito, não divisão omitida |
| **DFE-005** — homologação | ambiente lido do XML e sinalizado à parte do `xProd` especial do primeiro item |
| **DFE-006** — contingência | `tpEmis`, `dhCont` e `xJust` preservados; documento autorizado depois mantém emissão e justificativa originais e só ganha protocolo |
| **DFE-009** — QR | URL vem do `infNFeSupl`, nunca remontada — remontar exigiria CSC e é origem da rejeição 397 |
| **DFE-010** — IBS/CBS | grupos novos tolerados sem quebrar e sem serem inventados na representação |
| Segurança do parser | DTD e entidades externas proibidos (teste de XXE), limite de tamanho, modelo 55 recusado |

`SEM GTIN` é tratado como ausência de código de barras, não como GTIN — imprimir esse literal seria
informação falsa.

### 27.4 O que falta para fechar DAN-001/DAN-002

Passos 6 a 10 da seção 26.5, agora sem ambiguidade de canal:

1. `ObterCupomAsync` passa a desserializar o XML persistido e a devolver o DTO fiscal; o
   `CupomDto` montado do cadastro é aposentado;
2. HTML completado com as divisões faltantes — consumidor, quantidade total de itens, unidade e
   código por item, valor por meio de pagamento, troco, aviso de homologação e bloco de contingência;
3. CSS de impressão com `@page` em 58 mm e 80 mm, substituindo a largura fixa de 320 px;
4. política visual por estado do documento (autorizada, contingência, cancelada, rejeitada/pendente);
5. impressão física nas bobinas reais e leitura do QR por dois aparelhos;
6. comparação campo a campo XML × representação e aceite técnico e fiscal registrados.

**Fontes:** [Manual de Padrões Técnicos do DANFE NFC-e e QR Code v6.0](https://www.nfe.fazenda.gov.br/PORTAl/exibirArquivo.aspx?conteudo=k%2FIuuaW4YiY%3D)
e seções 24.11, 25.7 e 26 deste documento.

---

## 28. FIS-002 e RES-002 concluídos — 05/08/2026

Dois cartões P0 fechados no código, cada um com o defeito que motivava e a prova
que o trava. Nenhum substitui a homologação física.

### 28.1 FIS-002 — códigos de meio de pagamento

Crediário, pontos e cashback caíam todos em `tPag=99` ("Outros"). Passaram a usar
os códigos próprios e vigentes:

| Meio do ERP | Antes | Agora | Código |
|---|---|---|---|
| Crediário | 99 + xPag | `fpCartaoDaLoja` | **05** |
| Pontos | 99 + xPag | `fpProgramadefidelidade` | **19** |
| Cashback | 99 + xPag | `fpProgramadefidelidade` | **19** |

- a descrição do `05` foi ampliada pelo Informe Técnico 2024.002 ("Cartão da Loja,
  Crediário Digital, Outros Crediários"), vigente em produção desde 01/07/2024;
- `xPag` deixou de ser emitido nesses meios — fica reservado ao `99`, único código
  que a SEFAZ rejeita sem descrição;
- não havia teste sobre a montagem do pagamento, e foi por isso que o `99` passou
  despercebido. `NfcePagamentoTests` agora trava cada código e as combinações de
  split (12 casos).

Arquivos: `NfceEmissionService.MapFormaPagamento`, `MontarDetPagUnico`;
`tests/.../Services/NfcePagamentoTests.cs`.

### 28.2 RES-002 — XML assinado da contingência persistido

Antes, uma NFC-e emitida offline guardava só chave e QR; o XML assinado entregue
ao consumidor não era persistido. Duas consequências, ambas corrigidas:

1. **DANFE de contingência não tinha fonte imutável.** Nova coluna
   `xml_contingencia` guarda o XML assinado no momento da emissão offline;
   `ObterCupomAsync` passa a usá-lo quando não há `nfeProc` (nfeProc autorizado
   tem precedência). Reiniciar a aplicação ou perder cache não altera mais o
   documento entregue.
2. **A retransmissão remontava da comanda atual.** Uma edição na comanda entre a
   venda offline e a retransmissão produziria um documento diferente com a MESMA
   chave — divergente do que o consumidor levou. `RetransmitirContingenciaAsync`
   desserializa o XML salvo e reenvia exatamente ele, sem remontar nem reassinar.
   Ao autorizar, o `nfeProc` vira a fonte e o XML de contingência é descartado.

Cobertura: `NfceContingenciaCupomTests` fixa a fonte do DANFE por estado
(contingência sem protocolo, autorizada preferindo o nfeProc, cancelada, e sem
documento → null). Migration `AddXmlContingencia` (coluna nullable, sem backfill).

**Limite de homologação:** o reenvio do XML literal à SEFAZ (`NFeAutorizacao` com o
`NfeDocumento` desserializado) não pôde ser validado contra o ambiente real nesta
etapa. A lógica de estado, persistência e escolha de fonte está testada; a
transmissão em si entra na matriz HOM-001, no cenário de contingência offline →
reinício do serviço → retransmissão.

### 28.3 Validação

- `dotnet test` — **543 aprovados, 0 falhas** (eram 527; +12 FIS-002, +4 RES-002);
- `npm run build` no frontend — compilação de produção concluída;
- migration `AddXmlContingencia` gerada e conferida.

### 28.4 Estado dos cartões após esta rodada

| Cartão | Estado |
|---|---|
| FIS-002 | código e testes concluídos; aceite fiscal do XML pendente (HOM-001) |
| RES-002 | persistência, DANFE de contingência e escolha de fonte concluídos; transmissão real do reenvio pendente (HOM-001) |
| REG-001 | não iniciado — próximo a analisar; emissão fora do Simples segue bloqueada no pré-voo |

---

## 29. XML-001 concluído — identificação do item no XML — 05/08/2026

Três defeitos de identificação do item, todos verificados em código na seção 18
e agora corrigidos.

### 29.1 O que mudou

| Campo | Antes | Agora |
|---|---|---|
| `cProd` | posição do item na nota (`000001`, `000002`…) | Id do produto (`Guid` "N"), identidade estável que cruza com estoque e escrituração |
| `cEAN` / `cEANTrib` | sempre `SEM GTIN`, ignorando o código de barras cadastrado | GTIN do cadastro **quando válido**; `SEM GTIN` caso contrário |
| `xProd` | nome cru, sem limite | truncado a 120 caracteres (limite do leiaute), espaços colapsados |

### 29.2 Validação de GTIN — por que na unha

A biblioteca fiscal só oferece consulta ao CCG por **webservice** (`ConsultaGtin`),
inviável a cada venda. O dígito verificador GS1 (módulo 10) foi implementado
localmente — algoritmo padrão e bem definido. `SanitizarGtin` aceita apenas
GTIN-8/12/13/14 com dígito correto; qualquer outra coisa vira `SEM GTIN`.

Isso importa porque **cEAN inválido é rejeição 611** (NT 2021.003): mandar um
código de barras interno malformado como se fosse GTIN derruba a nota. É melhor
declarar `SEM GTIN` do que declarar um GTIN que não fecha.

O algoritmo foi validado contra quatro GTINs reais conhecidos (EAN-13, GTIN-8 e
GTIN-14) — se o cálculo estivesse errado, um produto legítimo perderia o GTIN ou
um inválido passaria.

### 29.3 Validação

- `NfceIdentificacaoItemTests` — 18 casos (cProd estável, GTIN válido/inválido/
  malformado, xProd truncado);
- `dotnet test` — **561 aprovados, 0 falhas** (eram 543; +18);
- migration não foi necessária: os campos já existiam no `Product` (`Id`, `Barcode`),
  só não eram usados na montagem do XML.

### 29.4 Estado dos cartões

| Cartão | Estado |
|---|---|
| FIS-002 | concluído em código; aceite fiscal na HOM-001 |
| RES-002 | persistência e DANFE de contingência concluídos; transmissão real na HOM-001 |
| XML-001 | concluído em código; conferência do XML real na HOM-001 |
| XML-002 | não iniciado — ligar validação XSD antes de transmitir |
| CON-001 | não iniciado — conciliação de vendas × documentos |
| REG-001 | não iniciado — emissão fora do Simples segue bloqueada no pré-voo |

---

## 30. REG-001 concluído — totalizadores do regime normal — 05/08/2026

### 30.1 O defeito

`SomarTotaisIcms` era um `switch` que conhecia apenas `ICMSSN201` e `ICMSSN202`.
Isso bastava no Simples, onde o CSOSN não destaca ICMS próprio e o `ICMSTot`
legitimamente fica zerado. Quando o motor passou a montar itens por CST (Lucro
Presumido/Real), **nenhuma das dez classes novas tinha `case`**: o item destacava
`vICMS` e o total mandava zero.

Isso é divergência entre a soma dos itens e o totalizador do documento —
rejeição certa na SEFAZ, com numeração queimada. E o `default` silencioso do
`switch` não quebrava teste nenhum: o erro só apareceria na primeira venda real.

### 30.2 A correção

Os totais passam a ser calculados com os **getters polimórficos da própria
biblioteca fiscal** (`NFe.Classes.Informacoes.Detalhe.Tributacao.Extensions`),
que operam sobre `ICMSBasico` e funcionam para qualquer subtipo:

`GetIcmsBcValue`, `GetIcmsValue`, `GetIcmsDesonValue`, `GetIcmsBcStValue`,
`GetIcmsStValue`, `GetPisValue`, `GetCofinsValue`.

Não há mais `case` a esquecer — um CST novo entra sozinho. **Isso não economiza
linhas: elimina a classe inteira de bug.** Era o mesmo padrão que causou o
defeito original, e mantê-lo (só acrescentando dez `case`) teria deixado a
armadilha armada para o próximo grupo.

Única exceção: o **FCP** não tem getter na biblioteca e continua lido por tipo,
isolado em `SomarFcp`, com as classes listadas explicitamente para que a ausência
de um tipo seja visível ali e não vire zero silencioso no total.

O `ICMSTot` deixou de hardcodear `vBC`, `vICMS`, `vICMSDeson`, `vFCP`, `vPIS` e
`vCOFINS` em zero — todos vêm da soma dos itens.

### 30.3 Emissão fora do Simples reaberta

Com os totalizadores consolidando os grupos, a guarda de pré-voo por regime foi
removida: os três regimes montam documento completo.

> **Isto não é aprovação fiscal.** O XML completo fora do Simples ainda precisa
> passar por XSD (XML-002), homologação na SEFAZ por CST e aceite do contador
> antes de um tenant real emitir. O que mudou é que o motor deixou de produzir
> documento internamente inconsistente.

### 30.4 Validação

- `NfceTotalizadoresTests` — 11 casos, incluindo o teste que reproduz o defeito
  (`Cst00_TotalNaoPodeFicarZeradoQuandoOItemDestacaIcms`), a soma de múltiplos
  itens, CST 10/20/60 com e sem ST, PIS/COFINS cumulativo e não-cumulativo, e
  **não-regressão do Simples** (totais continuam zerados, agora por cálculo);
- `dotnet test` — **572 aprovados, 0 falhas** (eram 561; +11).

### 30.5 Estado dos cartões

| Cartão | Estado |
|---|---|
| FIS-002, RES-002, XML-001, DAN-001, REG-001 | concluídos em código |
| XML-002 | **bloqueado por artefato externo** — a biblioteca tem `ValidarSchemas`/`DiretorioSchemas`, mas os XSDs oficiais não vêm no pacote nem existem no repositório; é preciso baixar e versionar o pacote de schemas |
| RES-001, CON-001/002 | pendentes |
| DAN-002, CAD-001, FIS-001/003, OPS, HOM/PRD | dependem de homologação ou do contador |
