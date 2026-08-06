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
