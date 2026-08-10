# Status de execução — NFC-e para produção

Painel rápido do que já foi feito, do que está em andamento e do que falta.
**Complementa, não substitui** o plano em
[`PLANO-GO-LIVE-NFCE-PRODUCAO-2026.md`](PLANO-GO-LIVE-NFCE-PRODUCAO-2026.md) — o
plano continua sendo a fonte da verdade sobre escopo, critérios e fundamentos; aqui
é só o retrato do progresso.

Última atualização: **10/08/2026** (rev. 7) · Alvo de lançamento: **10/08/2026**

## Legenda

- ✅ **Feito** — código e testes concluídos nesta base
- 🟡 **Parcial** — parte entregue; falta etapa registrada
- ⏳ **Pendente** — não iniciado
- 🚧 **Bloqueado** — depende de artefato ou decisão externa
- 👤 **Humano** — depende de contador, loja ou homologação física (não é código)

> Nenhum ✅ significa "certificado para produção". O go-live fiscal depende de
> homologação na SEFAZ e da matriz HOM-001 — ver seção 12
> do plano. "Feito" aqui é sempre "feito em código, com testes".

## Quadro de cartões

| Cartão | Estado | O que foi feito / o que falta | Seção do plano |
|---|---|---|---|
| **FIS-002** — códigos de pagamento | ✅ | crediário → tPag 05; NFC-e com pontos/cashback bloqueada antes de reservar número, por orientação contábil. Aceite fiscal do XML na HOM-001. | 28.1 |
| **RES-002** — XML de contingência | ✅ | XML assinado offline persistido (`xml_contingencia`); DANFE sai dele; retransmissão reenvia o documento original em vez de remontar. Transmissão real à SEFAZ fica na HOM-001. | 28.2 |
| **XML-001** — identificação do item | ✅ | `cProd` = Id do produto; `cEAN` = GTIN validado (dígito GS1 local); `xProd` truncado a 120. 18 testes. | 29 |
| **DAN-001** — DANFE do XML | ✅ | parser + DTO imutável + HTML no padrão do manual, alimentado só pelo XML; `ObterCupomAsync` lê o XML persistido. 25 testes de parser. | 25.3, 26, 27 |
| **DAN-002** — verificação física | 👤 | XML e DANFE aprovados pelo contador; faltam impressão física em 58/80 mm e leitura do QR em dois aparelhos. | 7 |
| **RES-001** — resultado incerto | ✅ | falha de rede deixou de ser um caso só: "nunca chegou" vai para contingência, timeout vira `ResultadoIncerto` e consulta a chave antes de decidir; duplicidade adota o documento da SEFAZ em vez de rejeitar. Chave/XML/tentativa persistidos antes do envio; número protegido de inutilização. 15 testes com a SEFAZ atrás de interface. Timeout real fica na HOM-001. | 6, 32 |
| **XML-002** — validação XSD | ✅ | pacotes oficiais baixados e versionados em `CardGameStore/Schemas` com procedência; validação própria (`XmlSchemaSet`) porque os arquivos não podem ser achatados como o `DiretorioSchemas` da lib exige — `tiposBasico_v4.00.xsd` difere entre `Evento/` e `NFe/` no mesmo pacote. Roda depois de assinar e antes da contingência; reprovação vira rejeição local, nunca contingência nem retry infinito. O XML do motor passa nos 5 cenários. 13 testes. `enviNFe_v4.00.xsd` e `inutNFe_v4.00.xsd` já estão versionados, baixados byte a byte da SVRS. | 9, 30.5, 35 |
| **CON-001** — conciliação | ✅ | serviço parte das VENDAS e acha o documento de cada uma; expõe venda sem nota e divergência de valor; endpoints para lojista e contador + aba no portal. 16 testes. | 31 |
| **CON-002** — alertas | ✅ | pendências reconciliadas do estado real (não disparos): as seis situações da seção 8, com severidade por idade, dedup pela chave do fato, resolução automática quando a condição some, responsável e confirmação auditável que reabre se o problema continua. Painel em Admin > Fiscal. 30 testes. Falta backup (é OPS-002). | 8, 33 |
| **CON-003** — quem optou por não emitir | ✅ | comanda e venda avulsa guardam a escolha, o operador e o horário (`fiscal_emissao_escolhida`), registrados ANTES da tentativa de emitir; a conciliação passa a exibir a decisão. Nulo = venda anterior ao registro, e não "não escolheu". Sem isso o contador recebe venda sem documento e sem contexto (seção 36.5). | 31.4, 36.5 |
| **REG-001** — regime normal | ✅ | totalizadores consolidam ICMS/ST/FCP/PIS/COFINS dos itens via getters polimórficos da lib; `ICMSTot` sem zeros fixos; emissão fora do Simples reaberta. 11 testes. XML/DANFE aceitos pelo contador; falta homologação real por CST. | 30 |
| **CAD-001** — saneamento do catálogo | 👤 | conferência de NCM/CEST/CFOP/CSOSN/CST pelo contador. | 9 |
| **FIS-001** — escopo assinado | 👤 | UF, IE, credenciamento, série/número, escopo presencial — com o contador. | 4 |
| **FIS-003** — pontos pré-aplicados | ✅ | decisão do contador: pontos/cashback ficam fora da emissão fiscal; histórico preservado e novas tentativas bloqueadas. | 5 |
| **RTC-001** — IBS/CBS versionado | ✅ | trava fixa de 2027 removida: as regras viraram catálogo versionado com vigência, perfil do contribuinte, alíquotas, fonte oficial e data de consulta — a última faixa é aberta, então virar o ano nunca para a emissão. Perfil diferencia Simples, excesso de sublimite, opção pelo regime regular e regime normal. Alerta cobra a revisão da regra. 25 testes. **As alíquotas de 2027 dependem de publicação oficial** — o mecanismo está pronto, a faixa não. | 10, 34 |
| **UF-001 / ECOM-001** | ⏳ | condicionais: outra UF / e-commerce, fora do escopo inicial. | 10 |
| **OPS-001/002** — produção e guarda | 👤 | credenciamento real, backup, restauração — infra e homologação. | 11 |
| **HOM-001 / PRD-001** — certificação | 👤 | bateria de homologação e piloto controlado. | 12 |

## Também entregue (fora dos cartões de go-live)

Trabalho anterior à auditoria, já na mesma PR:

- motor de **apuração tributária** (Simples × Lucro Presumido, comparativo);
- **fechamento fiscal mensal** e DRE do contador;
- **portal do contador** componentizado (visão geral, impostos, estoque, fechamento, avisos, config);
- **configuração fiscal** ampliada (regime, CST, PIS/COFINS, folha, presunções).

Depois da auditoria, na mesma frente:

- **tabela local do IBPT** (IBPT-002): a consulta HTTP saiu do caminho do usuário
  — job diário monta a tabela, cadastro de produto vira lookup local. Com a API do
  IBPT fora do ar, `Admin > Fiscal → Importar tabela (.csv)` carrega o arquivo
  oficial da UF inteira de uma vez e a emissão deixa de depender do serviço deles;
- **pontos e cashback viraram módulo opcional, desligado** — o código continua
  no lugar, mas não participa do fechamento da venda nem chega à nota. Retirou uma
  decisão do contador do caminho crítico (ver `BACKLOG.md`);
- **rastreabilidade do NCM pela NF-e de entrada** — o recebimento preserva o NCM
  do item como evidência, preenche somente cadastro sem NCM, mantém divergências
  visíveis sem sobrescrever e mostra ao contador chave, fornecedor e item de origem.

## Onde estamos

**Fechado em código nesta maratona:** FIS-002, RES-002, XML-001, DAN-001,
REG-001, CON-001, RES-001, CON-002, CON-003, RTC-001, XML-002.

**Não há mais cartão de go-live que dependa só de código, e nenhum bloqueado por
artefato externo.** Todos os cartões restantes dependem de pessoas: contador,
loja ou homologação na SEFAZ.

PDV-001 (operar com o enlace da loja fora do ar — seção 25.5) e o agente local de
impressão continuam sendo decisão de produto, não pendência fiscal: a impressão
do DANFE já funciona pelo navegador do PDV (opção C, seção 27), e o agente só se
justifica para operar offline, não para imprimir.

**O que só o dia da homologação resolve:** transmissão real (RES-001/002),
impressão física (DAN-002), aceite do catálogo (CAD-001) e os parâmetros cadastrais
de produção (FIS-001). FIS-003 saiu do caminho crítico: com pontos e cashback desligados, não
há benefício pré-aplicado chegando à nota para classificar.

**Pendência operacional aberta:** o número **22** da série foi queimado numa
tentativa de emitir comanda vazia (defeito já corrigido — venda sem item ou com
valor zero agora é recusada antes de reservar número). O número não foi usado e
provavelmente precisa de **inutilização** junto à SEFAZ.

## Suíte

`dotnet test` — **734 aprovados, 0 falhas** (PostgreSQL descartável de
`tests/docker-compose.yml`). Frontend: `npm run build` concluído.

Se a suíte falhar em massa em testes sem relação nenhuma com o que mudou
(`AuthService`, `TenantIsolation`), a causa provável não é o código: é schema de
teste acumulado no Postgres local até o handshake do Npgsql estourar. A partir
desta revisão a própria fábrica varre os órfãos no início da execução — ver o
cabeçalho de `TestDbFactory`.

## Validação local de 10/08/2026

- tenant novo provisionado em PostgreSQL real, com isolamento por schema, login
  do administrador e módulos `fiscal`/`estoque` verificados pela interface;
- pré-voo fiscal de tenant vazio bloqueou a emissão e indicou dados da empresa,
  certificado A1 e natureza padrão como próximas ações;
- fixture `nfce-homologacao.xml` renderizada pelo endpoint real do tenant em
  bobinas de 58 mm e 80 mm, com QR, chave, protocolo, aviso de homologação e
  todos os valores sem estouro horizontal;
- itens do DANFE passaram de uma grade comprimida de seis colunas para duas
  linhas térmicas (identificação + quantidade/valor), preservando os mesmos
  dados do XML; menus, widgets e banner de cookies ficam fora da mídia de impressão;
- boot local no Windows deixou de depender de permissão para o Event Log: logs
  portáteis (console/debug) não podem mais derrubar a aplicação durante o seed;
- texto da tela alinhado ao pré-voo: CSC é obrigatório e a ausência bloqueia a
  emissão antes da reserva de numeração;
- recebimento de NF-e testado com rastreabilidade do NCM: preenche cadastro vazio,
  preserva o valor do XML e não sobrescreve divergência sem revisão contábil.

O contador já aprovou o XML e o DANFE. Esta rodada **não fecha DAN-002**: ainda
faltam impressão em hardware real nas duas bobinas e leitura do QR impresso por
dois aparelhos. A fixture comprova layout e integração de software, não aderência
física da impressora nem autorização real da SEFAZ.
