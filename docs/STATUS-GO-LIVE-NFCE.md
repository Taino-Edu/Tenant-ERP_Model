# Status de execução — NFC-e para produção

Painel rápido do que já foi feito, do que está em andamento e do que falta.
**Complementa, não substitui** o plano em
[`PLANO-GO-LIVE-NFCE-PRODUCAO-2026.md`](PLANO-GO-LIVE-NFCE-PRODUCAO-2026.md) — o
plano continua sendo a fonte da verdade sobre escopo, critérios e fundamentos; aqui
é só o retrato do progresso.

Última atualização: **05/08/2026** (rev. 3) · Alvo de lançamento: **10/08/2026**

## Legenda

- ✅ **Feito** — código e testes concluídos nesta base
- 🟡 **Parcial** — parte entregue; falta etapa registrada
- ⏳ **Pendente** — não iniciado
- 🚧 **Bloqueado** — depende de artefato ou decisão externa
- 👤 **Humano** — depende de contador, loja ou homologação física (não é código)

> Nenhum ✅ significa "certificado para produção". O go-live fiscal depende de
> homologação na SEFAZ, aprovação do contador e da matriz HOM-001 — ver seção 12
> do plano. "Feito" aqui é sempre "feito em código, com testes".

## Quadro de cartões

| Cartão | Estado | O que foi feito / o que falta | Seção do plano |
|---|---|---|---|
| **FIS-002** — códigos de pagamento | ✅ | crediário → tPag 05, pontos/cashback → 19; 12 testes de montagem de pagamento. Aceite fiscal do XML na HOM-001. | 28.1 |
| **RES-002** — XML de contingência | ✅ | XML assinado offline persistido (`xml_contingencia`); DANFE sai dele; retransmissão reenvia o documento original em vez de remontar. Transmissão real à SEFAZ fica na HOM-001. | 28.2 |
| **XML-001** — identificação do item | ✅ | `cProd` = Id do produto; `cEAN` = GTIN validado (dígito GS1 local); `xProd` truncado a 120. 18 testes. | 29 |
| **DAN-001** — DANFE do XML | ✅ | parser + DTO imutável + HTML no padrão do manual, alimentado só pelo XML; `ObterCupomAsync` lê o XML persistido. 25 testes de parser. | 25.3, 26, 27 |
| **DAN-002** — verificação física | 👤 | impressão em 58/80 mm, leitura do QR em dois aparelhos, aceite fiscal. | 7 |
| **RES-001** — resultado incerto | ⏳ | timeout que consulta a chave antes de decidir; tratar duplicidade por reconciliação. | 6 |
| **XML-002** — validação XSD | 🚧 | **bloqueado por artefato externo**: a lib tem `ValidarSchemas`/`DiretorioSchemas`, mas os XSDs oficiais não vêm no pacote nem existem no repo — é preciso baixar e versionar o pacote de schemas. | 9, 30.5 |
| **CON-001** — conciliação | ✅ | serviço parte das VENDAS e acha o documento de cada uma; expõe venda sem nota e divergência de valor; endpoints para lojista e contador + aba no portal. 16 testes. | 31 |
| **CON-002** — alertas | ⏳ | alertas por severidade/idade, responsável e confirmação de resolução. Inclui registrar quem optou por não emitir (ver 31.4). | 8 |
| **REG-001** — regime normal | ✅ | totalizadores consolidam ICMS/ST/FCP/PIS/COFINS dos itens via getters polimórficos da lib; `ICMSTot` sem zeros fixos; emissão fora do Simples reaberta. 11 testes. **Não é aprovação fiscal** — falta XSD, homologação por CST e aceite do contador. | 30 |
| **CAD-001** — saneamento do catálogo | 👤 | conferência de NCM/CEST/CFOP/CSOSN/CST pelo contador. | 9 |
| **FIS-001** — escopo assinado | 👤 | UF, IE, credenciamento, série/número, escopo presencial — com o contador. | 4 |
| **FIS-003** — pontos pré-aplicados | 👤 | decidir se pontos são desconto ou crédito virtual — decisão do contador. | 5 |
| **RTC-001** — IBS/CBS versionado | 🟡 | grupos tolerados no parser; falta remover a trava fixa de 2027 e versionar as regras. | 10 |
| **UF-001 / ECOM-001** | ⏳ | condicionais: outra UF / e-commerce, fora do escopo inicial. | 10 |
| **OPS-001/002** — produção e guarda | 👤 | credenciamento real, backup, restauração — infra e homologação. | 11 |
| **HOM-001 / PRD-001** — certificação | 👤 | bateria de homologação e piloto controlado. | 12 |

## Também entregue (fora dos cartões de go-live)

Trabalho anterior à auditoria, já na mesma PR:

- motor de **apuração tributária** (Simples × Lucro Presumido, comparativo);
- **fechamento fiscal mensal** e DRE do contador;
- **portal do contador** componentizado (visão geral, impostos, estoque, fechamento, avisos, config);
- **configuração fiscal** ampliada (regime, CST, PIS/COFINS, folha, presunções).

## Onde estamos

**Fechado em código nesta maratona:** FIS-002, RES-002, XML-001, DAN-001, REG-001, CON-001.

**Próximos deliveráveis em código:** RES-001 (resultado incerto após timeout) e
CON-002 (alertas). XML-002 está bloqueado até o pacote de XSDs oficiais ser
baixado e versionado no repositório.

**O que só o dia da homologação resolve:** transmissão real (RES-001/002),
impressão física (DAN-002), aceite do catálogo (CAD-001) e as decisões do contador
(FIS-001, FIS-003).

## Suíte

`dotnet test` — **588 aprovados, 0 falhas** (PostgreSQL descartável de
`tests/docker-compose.yml`). Frontend: `npm run build` concluído.
