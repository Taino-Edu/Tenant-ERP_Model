# Fixtures de NFC-e — dados sintéticos

Todos os XMLs desta pasta são **inventados**. Nenhum foi emitido, transmitido ou
autorizado pela SEFAZ, e nenhum contém dado de empresa, pessoa ou nota real.

Convenções usadas para deixar isso evidente em qualquer inspeção:

| Campo | Valor sintético | Por quê |
|---|---|---|
| CNPJ do emitente | `00000000000191` | CNPJ de teste amplamente usado, não pertence a contribuinte ativo |
| Razão social | `LOJA FIXTURE DE TESTE LTDA` | nome autoexplicativo |
| CPF do consumidor | `00000000191` | sequência de teste, nunca emitida a pessoa física |
| Chave de acesso | começa com `9999` | UF 99 não existe; garante que a chave nunca colida com documento real |
| Protocolo | `999...` | fora da faixa de protocolos reais |
| CSC / certificado | ausentes | fixture nenhuma carrega segredo |

Regra do plano de go-live (seção 26.6): **não usar XML, certificado, CSC, CNPJ,
CPF ou dado real em fixture ou commit**. Ao acrescentar um cenário novo, manter
as convenções acima.

## Cenários

| Arquivo | Cobre |
|---|---|
| `nfce-normal-autorizada.xml` | produção, consumidor não identificado, pagamento único, autorizada |
| `nfce-homologacao.xml` | `tpAmb=2`, primeiro item com o `xProd` obrigatório de homologação |
| `nfce-pagamentos-mistos.xml` | consumidor identificado por CPF, Pix + cashback (`19`) + crediário (`05`), troco e desconto |
| `nfce-contingencia.xml` | `tpEmis=9` com `dhCont`/`xJust`, sem protocolo |
| `nfce-contingencia-autorizada.xml` | o mesmo documento anterior, agora com `protNFe` |
| `nfce-ibscbs.xml` | grupos da reforma tributária presentes no XML |
