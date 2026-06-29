# SoftNerd CardGameStore — Roteiro de Apresentação PIM (v2)
**UNIP · Análise e Desenvolvimento de Sistemas · 3º Semestre · PIM III · 2025**
**Duração total: ~7 minutos  |  Slides: 7  |  Equipe: 6 integrantes**

> **Dica da turma:** Apresente os slides em ~2 minutos e reserve o restante para a demo ao vivo do sistema.

---

## SLIDE 1 — Capa  |  Eduardo Taino  (~35 seg)

> "Bom dia, banca. Meu nome é Eduardo Taino. Somos o grupo SoftNerd e hoje apresentamos o **CardGameStore** — um sistema cloud-native de gestão para lojas de card games competitivos.
>
> O projeto integra as disciplinas do semestre em uma solução real, com código em produção.
>
> Somos seis integrantes em três duplas: **Eduardo e Reinaldo** no back-end, **Aldenor e Rickson** no front-end, **Rafael e Hugo** no banco de dados. O Rafael começa com o contexto do problema."

---

## SLIDE 2 — O Problema  |  Rafael  (~45 seg)

> "Sou o Rafael, banco de dados. Antes de mostrar o que construímos, precisamos entender o que motivou o sistema.
>
> Lojas de card games crescem, mas seguem gerenciadas à mão. Três problemas centrais: primeiro, o dono precisa atender no balcão e controlar as mesas ao mesmo tempo — é impossível fazer os dois bem. Segundo, estoque em planilhas e campeonatos em cadernos — zero rastreabilidade. Terceiro, nenhuma plataforma digital para fidelizar o cliente.
>
> O mercado tem mais de 8.000 lojas especializadas no Brasil, crescendo com o boom do TCG. Ninguém criou ainda uma solução acessível para isso. O Hugo apresenta nossa resposta."

---

## SLIDE 3 — Nossa Solução  |  Hugo  (~45 seg)

> "Sou o Hugo. Nossa solução tem três pilares.
>
> **Autoatendimento:** o cliente escaneia o QR Code da mesa, faz o pedido no próprio celular, e ele aparece no painel do balcão em menos de 50 milissegundos — sem fila, sem erro de digitação.
>
> **Gestão completa:** o estoque baixa automaticamente a cada pedido, os campeonatos têm inscrição online e o gestor acompanha tudo em tempo real.
>
> **Fidelização:** o cliente acumula pontos, tem perfil digital e pode tirar dúvidas com nosso assistente de IA. O Rafael agora fala sobre como estruturamos o banco de dados."

---

## SLIDE 4 — Banco de Dados  |  Rafael + Hugo  (~60 seg)

> **Rafael:** "A decisão arquitetural mais importante foi a **Persistência Poliglota** — dois bancos com propósitos opostos, definidos pelo ciclo de vida do dado.
>
> O **PostgreSQL** guarda os dados financeiros e operacionais: comandas, estoque, histórico de vendas. É o cofre do sistema — rígido por design, com chaves estrangeiras e triggers que garantem que nenhuma conta fecha com erro.
>
> O **MongoDB** guarda o que muda o tempo todo: perfil do cliente, pontuação, inscrições em campeonato. Por ser schema-less, adicionamos novos campos de fidelização sem nenhuma migration de banco — zero downtime, zero risco em produção.
>
> Essa divisão não é modismo — é escolha consciente de ciclo de vida de dado."

---

## SLIDE 5 — Back-end & Segurança  |  Eduardo Taino + Reinaldo  (~60 seg)

> **Eduardo:** "O back-end é uma API RESTful em ASP.NET Core 8 com 15 requisitos funcionais implementados.
>
> O coração do sistema é o **SignalR**: pedido feito no celular chega ao balcão em menos de 50ms, sem nenhum refresh de página. É em tempo real mesmo.
>
> Para segurança e LGPD: o JWT fica em cookies HttpOnly — nunca em localStorage. Temos rate limiting de 5 tentativas de login por minuto por IP, e cada ação sobre dados pessoais é registrada no `audit_logs` conforme o Artigo 46."
>
> **Reinaldo:** "Na qualidade: adotamos TDD desde o início com xUnit e Moq — **9 de 9 testes passando**, 80% de cobertura nos módulos críticos. Arquitetura limpa em camadas: Controller, Service, Repository."

---

## SLIDE 6 — Front-end & UX  |  Aldenor + Rickson  (~60 seg)

> **Aldenor:** "Sou o Aldenor. A interface foi construída com Next.js 14 e TypeScript, **Mobile-First**, porque o cliente usa o celular na mesa. O fluxo é simples: escaneia o QR Code, faz Quick-Login com CPF e WhatsApp, navega pelo cardápio e faz o pedido — menos de 2 minutos.
>
> A aplicação é uma **PWA**: pode ser instalada no celular como app nativo, sem App Store."
>
> **Rickson:** "Nos destaques: integramos o **VLibras** em toda a interface — 9,7 milhões de surdos no Brasil agora podem navegar no sistema com independência. O **Gemini 2.0 Flash** responde dúvidas sobre produtos TCG e regras de campeonato automaticamente."

---

## SLIDE 7 — Resultados & Demonstração  |  Eduardo Taino  (~45 seg)

> "Para fechar: entregamos 15 requisitos funcionais, 12 não-funcionais, 9 de 9 testes passando e conformidade LGPD completa. O sistema é uma PWA instalável e acessível para todos.
>
> Em resumo: um sistema real, com código em produção, que resolve um problema real.
>
> Agradecemos a atenção da banca. **Passemos à demonstração ao vivo do sistema.**"

---

## Distribuição de Tempo

| Slide | Apresentador(es) | Tempo |
|-------|-----------------|-------|
| 1 – Capa | Eduardo Taino | ~35 seg |
| 2 – O Problema | Rafael | ~45 seg |
| 3 – Nossa Solução | Hugo | ~45 seg |
| 4 – Banco de Dados | Rafael + Hugo | ~60 seg |
| 5 – Back-end & Segurança | Eduardo + Reinaldo | ~60 seg |
| 6 – Front-end & UX | Aldenor + Rickson | ~60 seg |
| 7 – Resultados & Demo | Eduardo Taino | ~45 seg |
| **Total slides** | | **~350 seg / ~6 min** |
| **Demo ao vivo** | Todos | **~1–2 min** |
| **Total** | | **~7–8 min** |

---

## Lembretes para a Apresentação

- Lembrem de preencher os **R.A.s** na capa antes de imprimir/apresentar
- Lembrem de preencher o nome do **orientador(a)** na capa
- A demo ao vivo é o ponto forte — ensaiem o fluxo do QR Code ao balcão
- Se a banca perguntar sobre escolha de tecnologia: "escolhemos com base nos requisitos, não em tendência"
