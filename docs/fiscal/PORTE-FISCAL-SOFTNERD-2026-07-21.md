# Porte de correções fiscais do softNerd — checklist pro Sol (21/07/2026)

Cópia de referência de `softNerd/FISCAL-CHANGELOG.md` — repo irmão, mesma base de
código de origem. Serve pra colocar o motor fiscal do Tenant-ERP_Model "nos trilhos"
igual já foi feito no softNerd hoje. Não editar `GO-LIVE-FISCAL-2026-07-25.md` a
partir deste arquivo — são documentos complementares (esse aqui é o "o que copiar
do softNerd"; aquele é o checklist de go-live específico deste repo).

**Como usar:** pra cada item abaixo, checar se o Tenant-ERP_Model já tem o fix
(alguns podem já ter sido portados). Se não tiver, é praticamente certeza que vai
dar o MESMO erro assim que testar emissão real contra Homologação — já foi
reproduzido e diagnosticado no softNerd hoje.

---

## Bugs de código já corrigidos no softNerd (conferir se existem aqui também)

### 1. Certificado A1 nunca emitia de verdade (o mais grave)
`ConfiguracaoCertificado.TipoCertificado` nunca era setado → cai no padrão
`A1Repositorio` (certificado do Windows, sem senha) → `Senha` lança
`"Para Certificado A1 o Senha não deve ser informada!"` mesmo com tudo certo.
**Fix:** setar `TipoCertificado = TipoCertificado.A1ByteArray` ANTES de
`ArrayBytesArquivo`/`Senha`, em `AbrirConfiguracaoSefazAsync()`.

### 2. NCM sem sanitizar (frontend trava no ponto + backend manda com pontuação)
Frontend: `maxLength={8}` com placeholder de 10 caracteres (`0000.00.00`) trava
antes do fim. Backend: NCM com ponto quebra a XSD (exige 8 dígitos puros).
**Fix:** `SanitizeNcm(string? ncm)` (remove tudo que não é dígito) + validação de
exatamente 8 dígitos antes de montar o XML, com erro claro se não bater.

### 3. CFOP sem sanitização — mesma classe do NCM
`CFOP = int.Parse(item.Cfop)` cru lançava `FormatException` sem explicação.
**Fix:** `ParseCfop(string? cfop)` sanitiza e valida 4 dígitos, com mensagem clara.

### 4. Desconto e pontos nunca entravam na nota — bug de VALOR (o mais grave em termos de $)
Toda NFC-e saía pelo valor BRUTO dos itens, ignorando desconto/pontos aplicados.
`vDesc` sempre zero, `vNF` sempre o valor cheio — divergência real com o que o
cliente pagou. **Fix:** `DadosEmissao` ganha `TotalCentavos` (valor líquido de
`Comanda.TotalInCents`/`VendaAvulsa.TotalInCents`); XML declara `vProd`/`vDesc`/`vNF`
corretamente separados; split de pagamento usa o total líquido.

### 5. Exportar XMLs falhava com início = fim (mesmo dia)
Selecionar a mesma data em início/fim dava erro 400; mesmo corrigindo isso, "fim"
era tratado como limite exclusivo (dia inteiro nunca entrava no ZIP).
**Fix:** só rejeita `fim < inicio`; soma 1 dia ao fim antes de gerar o ZIP.

### 6. `tpEmis` nunca configurado — emissão real nunca alcançava a rede
Com o certificado (#1) já corrigido, a emissão ainda falhava:
`"Serviço NFeAutorizacao, versão , não disponível para a UF SP..."` (versão/tipo em
branco). `ConfiguracaoServico.tpEmis` nunca era setado (ficava 0, valor de enum
inválido) — a lib usa isso pra buscar a URL do webservice numa tabela interna
(UF + ambiente + serviço + versão + tipoEmissao); com 0 não encontra NADA, pra
nenhuma UF. **Fix:** `cfgServico.tpEmis = TipoEmissao.teNormal` em
`AbrirConfiguracaoSefazAsync()`, sobrescrito pra `teOffLine` só quando retransmitindo
de uma contingência anterior. **Bônus mesmo commit:** CEP do emitente também sem
sanitizar (`"01310-100"` quebra `enderEmit\CEP deve receber somente números`) —
mesmo helper de sanitização (`SomenteDigitos`).

**Consequência:** até esse fix, NENHUMA emissão real (não simulada) jamais
funcionou — toda nota "Autorizada" vista antes veio do Modo Simulação, que nunca
toca a SEFAZ de verdade.

### 7. Exportação de XML usava fuso do servidor, não do Brasil
`ExportarXmls` usava `DateTime.ToUniversalTime()` (fuso LOCAL DO SERVIDOR — vira
UTC puro num VPS Linux). **Fix:** `TimeZoneInfo.ConvertTimeToUtc(..., BrazilZone)`,
mesmo padrão já usado em `ComandaService`/`VendaAvulsaService`/etc.

### 8. Certificado vencido era tratado como "SEFAZ fora do ar"
Certificado vencido derruba a autenticação mTLS via `HttpRequestException` — o
MESMO tipo que `EhFalhaDeConectividade` usa pra reconhecer indisponibilidade da
SEFAZ, mandando a nota pra contingência (cliente recebe cupom que a SEFAZ NUNCA vai
aceitar). **Fix:** checa `certificado.NotAfter` em `AbrirConfiguracaoSefazAsync()`
e lança erro de configuração claro se vencido.

### 9. CSC ausente derrubava o LOTE inteiro (cStat 225) e queimava numeração
`infNFeSupl`/`qrCode` é grupo OBRIGATÓRIO na XSD só pra NFC-e (mod=65, não existe
em NF-e mod=55). Sem `CscId`/`CscToken`, o motor montava `infNFeSupl` vazio →
SEFAZ rejeita o LOTE inteiro com `cStat 225 — Falha no Schema XML do lote de NFe`.
Como não é rejeição por contingência, cada "Reprocessar" reservava e inutilizava
um número NOVO à toa. **Fix:** valida `CscId`/`CscToken` em
`AbrirConfiguracaoSefazAsync()` ANTES de reservar número.

### 10. Texto da tela de CSC dava a entender que era só cosmético
Corrigido pra deixar claro que falta de CSC rejeita a nota INTEIRA (não só o QR),
e que Homologação/Produção usam CSCs diferentes.

---

## Achados de hoje que NÃO são bug de código (afetam qualquer tenant)

- **Credenciamento de NFC-e é separado por ambiente.** O CNPJ de teste (Maikon) já
  estava credenciado em Produção mas NÃO em Homologação — precisou credenciar
  voluntariamente no site da SEFAZ do estado antes de gerar CSC de teste. **Todo
  tenant novo vai precisar desse passo manual** antes do primeiro teste em
  Homologação — não tem como o software resolver isso.
- **NCM e código IBGE do município errados** derrubam a nota com mensagem clara da
  SEFAZ ("Informado NCM inexistente", "Código Municipal do Fato Gerador do ICMS
  inexistente") — conferir o cadastro de cada tenant contra a tabela oficial do
  IBGE antes de liberar (achamos um dígito trocado no cadastro do Maikon).
- **Reforma Tributária (IBS/CBS, NT 2025.002) já é exigida em Homologação**, antes
  do prazo de Produção (04/01/2027 pra Simples Nacional, 03/08/2026 pra Regime
  Normal). Sem o Grupo IBS/CBS no XML, a SEFAZ rejeita com "IBS/CBS não informado
  [nItem]". **Se isso ainda não estiver implementado aqui, qualquer teste em
  Homologação vai travar nesse ponto** — ver o que já foi feito no softNerd:
  alíquotas-teste oficiais de 2026 (IBS-UF 0,1%, IBS-Mun 0%, CBS 0,9%), CST 000,
  cClassTrib 000001.

## Achados de auditoria (Codex) ainda não corrigidos em nenhum dos dois repos

- Cancelamento fiscal não estorna estoque/financeiro/pontos/crediário — decisão de
  escopo pendente (nem sempre cancelar a nota deveria estornar a venda).
- Regime tributário (Lucro Presumido/Real) é selecionável na tela mas o motor
  sempre monta ICMS como Simples Nacional — precisa de regra de CST real,
  validada com contador antes de implementar (mesmo princípio do NCM: não
  inventar tabela fiscal sem confirmação profissional).
- CSC armazenado sem criptografia no banco (diferente do certificado, que já usa
  `EncryptionService`).

## Ordem sugerida

1. Certificado (#1) + tpEmis (#6) + CSC (#9) — sem os três, emissão real não sai
   do lugar pra nenhum tenant.
2. Desconto/pontos (#4) — único que gera nota com VALOR errado (os outros travam
   antes de sair).
3. NCM (#2), CFOP (#3), CEP (#6) — mesma classe de sanitização.
4. IBS/CBS (Reforma Tributária) — bloqueia Homologação HOJE, independente do
   prazo de produção.
5. ZIP export (#5) — menor impacto.
