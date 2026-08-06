# Esquemas XSD oficiais da SEFAZ (XML-002)

Pacotes de liberação baixados do [Portal Nacional da NF-e → Esquemas XML](https://www.nfe.fazenda.gov.br/portal/listaConteudo.aspx?tipoConteudo=BMPFMBoln3w%3D),
seção "VERSÕES OFICIAIS (em uso)", em **06/08/2026**.

| Pasta | Pacote | Publicado | Por que está aqui |
|---|---|---|---|
| `PL_010e_v1.02/` | Schemas XML NF-e 010e_v1.02 — NT 2025.002 v.1.40, NT 2025.002 v.1.0, NT 2026.003 v.1.0 | 10/07/2026 | **É o que a validação usa.** Leiaute NF-e/NFC-e 4.00 com os grupos de IBS/CBS |
| `PL_010d_v1.03/` | Schemas XML NF-e 010d_v1.03 — CNPJ Alfanumérico, NT 2026.004 v.1.01 | 10/07/2026 | Único com `consSitNFe` (consulta de chave do RES-001), `inutNFe` e `Evento/` (cancelamento) |
| `Eventos_RTC/` | Schema dos eventos da NT 2025.002 v.1.40 — RTC | 27/07/2026 | Eventos do RTC |
| `PL_NFeDistDFe_104/` | Pacote de Liberação Distribuição de DF-e v.1.04 | 03/07/2026 | Manifestação do destinatário (NF-e recebidas) |

## Não achate estas pastas

`tiposBasico_v4.00.xsd` tem **conteúdo diferente** entre `Evento/` e `NFe/` dentro
do mesmo pacote 010d (`11da7598…` vs `6021a5e7…`), e `tiposBasico_v1.03.xsd` tem
três versões distintas entre `010d/CadConsultaCadastro`, `010d/Evento` e
`Eventos_RTC`. A SEFAZ publica em subpastas porque os arquivos **não** são
intercambiáveis, e os `schemaLocation` internos são relativos.

Copiar tudo para um diretório único — que é o que o `DiretorioSchemas` do
DFe.NET espera — sobrescreveria um pelo outro em silêncio. Uma validação fiscal
errada é pior do que validação nenhuma, então a validação é feita por
`NfceSchemaValidator`, com `XmlSchemaSet`, lendo cada pacote na sua própria
pasta.

## Os pacotes são incrementais

Não existe `enviNFe_v4.00.xsd` (lote de envio) nem `procNFe_v4.00.xsd` (nfeProc)
em nenhum deles — o portal publica apenas os arquivos que mudaram. O conjunto de
`PL_010e_v1.02/NFe/` é autossuficiente para validar uma `<NFe>` assinada, que é o
que acontece antes de transmitir. Validar o lote exigiria o pacote base.

## Dois arquivos vieram avulsos do SVRS

O portal nacional publica apenas pacotes **incrementais**, e dois arquivos que a
lib exige não estão em nenhum deles. Foram baixados avulsos em **06/08/2026** do
espelho da Sefaz Virtual do RS (autoridade oficial, mas **fonte diferente** dos
outros quatro — registrar isso no dossiê de homologação):

| Arquivo | Origem | Onde ficou |
|---|---|---|
| `inutNFe_v4.00.xsd` | `https://dfe-portal.svrs.rs.gov.br/Schemas/PRNFE/inutNFe_v4.00.xsd` | `PL_010d_v1.03/NFe/` |
| `enviNFe_v4.00.xsd` | `https://dfe-portal.svrs.rs.gov.br/Schemas/PRNFE/enviNFe_v4.00.xsd` | `PL_010e_v1.02/NFe/` |

São **invólucros finos** (≈600 bytes cada): declaram o elemento raiz e incluem o
leiaute que já estava versionado aqui. Nenhuma regra de validação vem deles — as
regras continuam nos arquivos oficiais do portal. Por isso o risco de misturar
versões, que impede combinar pacotes de anos diferentes, praticamente não se
aplica a estes dois.

Cada um foi posto ao lado do leiaute que inclui, porque o `xs:include` é
relativo. Ambos compilam contra os arquivos existentes (verificado com
`XmlSchemaSet`), e a suíte cobre os três caminhos.

## O que a lib procura, verificado empiricamente

`NFe.Utils.Validacao.Validador` resolve o XSD pelo nome do serviço. Sondado em
06/08/2026 contra estas pastas:

| Serviço | Arquivo procurado | Situação |
|---|---|---|
| `RecepcaoEventoCancelmento` (sic, typo da lib) | `envEvento_v1.00.xsd` | ✅ presente em `PL_010d_v1.03/Evento/` — cancelamento é validado |
| `NfeInutilizacao` | `inutNFe_v4.00.xsd` | ✅ obtido avulso no SVRS (ver acima) — inutilização é validada |
| `NFeAutorizacao` (lote) | `enviNFe_v4.00.xsd` | ✅ obtido avulso no SVRS (ver acima) — lote é validado |
| Eventos do RTC (`e112110`, `e211110`, …) | `eNNNNNN_v1.00.xsd` | presentes em `Eventos_RTC/`, para quando esses eventos forem implementados |

Foram conferidos `010e_v1.02`, `010d_v1.03`, `010d_v1.01` (o "PL Eventos e Cad
Consulta Cadastro CCC"), `Eventos_RTC` e `DistDFe`: nenhum traz `inutNFe` ou
`enviNFe`. Eles existem avulsos no SVRS, como registrado acima.

**Nunca fabrique um XSD** que esteja faltando — nem transcrevendo o conteúdo de
uma página. Um schema reconstruído validaria contra uma regra que talvez não seja
a da SEFAZ, e validação fiscal errada é pior do que validação nenhuma. Baixe o
arquivo, byte a byte, de fonte oficial.

**Atenção ao testar:** a lib devolve **em silêncio** quando o XML não tem a raiz
esperada. Um teste com `<x/>` passa mesmo com a validação desligada. Para provar
que ela roda, use documento com a raiz certa e conteúdo inválido — é o que
`LibValidaOsTresCaminhosQueConsomemEstadoFiscal` faz.

## Ao atualizar

1. baixe o novo pacote do portal e extraia numa pasta com o nome dele;
2. atualize `NfceSchemaValidator.PacoteLeiaute` e a constante equivalente em
   `NfceSchemaValidacaoTests`;
3. rode `dotnet test --filter NfceSchemaValidacaoTests` — o XML que o motor
   produz precisa continuar válido no pacote novo;
4. registre a versão no dossiê de homologação (seção 17.1 do plano exige
   "relatório de validação XSD e versão do pacote").

Estes arquivos são documentos públicos da administração tributária, versionados
aqui para dar procedência à validação: sem eles no repositório, não há como
dizer *contra o quê* um documento foi validado.
