# SoftNerd CardGameStore — Roteiro de Apresentação PIM
**UNIP · Análise e Desenvolvimento de Sistemas · 2025**
**Duração total: ~7 minutos**

---

## SLIDE 1 — Capa  |  Eduardo Taino  (~40 seg)

> "Bom dia, banca. Meu nome é Eduardo Taino, sou o Tech Lead desta equipe, e hoje vamos apresentar o **SoftNerd CardGameStore** — um sistema de gestão cloud-native para uma loja de card games competitivos.
>
> O projeto integra todas as disciplinas do semestre — Engenharia de Software, POO, Banco de Dados, UX, Segurança e Comunicação — numa solução real, com código em produção.
>
> Somos seis pessoas em três duplas: Back-end, Front-end e Banco de Dados. A seguir, o Rafael explica o problema que nos motivou e como arquitetamos a solução."

---

## SLIDE 2 — Problema & Solução  |  Rafael Henrique  (~55 seg)

> "Eu sou o Rafael, responsável pelos serviços de negócio e testes do back-end.
>
> O SoftNerd nasceu de uma dor real: em lojas de card games, o dono precisa atender o balcão **e** gerenciar as mesas ao mesmo tempo — não tem como. O controle de estoque é manual, não existe fidelização digital e os campeonatos são gerenciados em cadernos.
>
> Nossa resposta foi um sistema cloud-native completo: o cliente escaneia o QR Code da mesa e faz o pedido pelo celular — que aparece instantaneamente no painel do balcão via WebSocket. A gamificação acumula pontos, o estoque baixa automaticamente e os campeonatos têm inscrição online.
>
> A stack: **ASP.NET Core 8** no back, **Next.js 14** no front, **PostgreSQL** e **MongoDB** no banco, tudo orquestrado com Docker e Nginx."

---

## SLIDE 3 — Banco de Dados  |  Rickson Ferreira  (~65 seg)

> "Eu sou o Rickson, responsável pela infraestrutura e banco de dados.
>
> A decisão mais importante de arquitetura foi adotar a **Persistência Poliglota** — dois bancos com propósitos opostos, separados pelo lado do "VS" que vocês veem no slide.
>
> O **PostgreSQL** cuida do núcleo financeiro: comandas, estoque e histórico de vendas. Usamos chaves estrangeiras e triggers para garantir que nenhuma conta feche com erro matemático. É o cofre do sistema — rígido por design.
>
> O **MongoDB** cuida do que muda o tempo todo: perfil do cliente, sistema de pontos e ingressos de campeonato. Por ser schema-less, podemos adicionar novos campos de fidelização sem nenhuma migration de banco — **zero downtime**, zero risco de quebrar o sistema em produção.
>
> Essa divisão não é modismo — é escolha consciente de ciclo de vida de dados."

---

## SLIDE 4 — Infraestrutura & LGPD  |  Hugo Matos  (~55 seg)

> "Sou o Hugo, responsável pela qualidade e documentação.
>
> Em infraestrutura: toda a aplicação roda em containers **Docker Compose** num VPS Ubuntu. O **Nginx** faz proxy reverso, terminação SSL e rate limiting — até 200 requisições por minuto na API geral, e apenas 5 tentativas de login por minuto por IP.
>
> Na conformidade **LGPD**: toda ação sobre dados pessoais é registrada na tabela `audit_logs` com hash de IP, atendendo ao Artigo 46. O administrador tem um painel para processar requisições de titulares — exportar ou deletar dados — conforme o Artigo 18. O JWT fica em **cookies HttpOnly**, jamais em localStorage, seguindo recomendação OWASP.
>
> Para qualidade, adotamos **TDD** desde o início: xUnit com Moq, **9 de 9 testes passando**, cobertura de 80% nos módulos críticos."

---

## SLIDE 5 — Back-end API  |  Eduardo Taino  (~65 seg)

> "Voltando para mim: o back-end é uma API RESTful em **ASP.NET Core 8** seguindo arquitetura em camadas — Controller recebe a requisição, Service executa a regra de negócio, Repository persiste via Entity Framework Core.
>
> O módulo de **autenticação** usa JWT com dois tokens: Access Token de 60 minutos e Refresh Token de 30 dias, ambos em cookies HttpOnly — jamais em localStorage. O Quick-Login via QR Code permite que o cliente entre com apenas CPF e WhatsApp.
>
> O coração do sistema é o módulo de **comandas**: quando o cliente adiciona um item pela mesa, o **SignalR** dispara um evento para o balcão em menos de 50 milissegundos — sem refresh de página. O preço é sempre lido no servidor, impossibilitando manipulação pelo cliente.
>
> Além disso, temos gestão completa de campeonatos de TCG e integração com o **Google Gemini 2.0 Flash** para assistência ao cliente."

---

## SLIDE 6 — Front-end  |  Aldenor Lopes  (~65 seg)

> "Eu sou o Aldenor, responsável pelo front-end.
>
> Toda a interface foi desenvolvida em **Next.js 14** com TypeScript e Tailwind CSS, seguindo a filosofia **Mobile-First** — o sistema funciona perfeitamente no celular, porque é exatamente onde o cliente vai usá-lo na mesa.
>
> O fluxo principal é simples: o cliente escaneia o QR Code único da mesa, faz o Quick-Login com CPF e WhatsApp, a comanda abre automaticamente, ele navega pelo cardápio e adiciona itens — que chegam ao balcão via SignalR sem qualquer intervenção humana.
>
> A aplicação é uma **PWA** — pode ser instalada no celular como app nativo, sem passar pela App Store.
>
> Temos três escopos separados: `/admin` para o gestor, `/mesa` para o autoatendimento e `/cliente` para o perfil do usuário, além das rotas de compliance LGPD sempre acessíveis."

---

## SLIDE 7 — UX / IA / Conclusão  |  Reinaldo Barros  (~75 seg)

> "Eu sou o Reinaldo, responsável por UX/UI e responsividade.
>
> Nosso design foi **Mobile-First no Figma**, depois implementado em Tailwind CSS. No dashboard administrativo, a hierarquia visual usa cores de status para que o balconista veja de relance quais mesas precisam de atenção — sem precisar ler uma tabela.
>
> A jornada do cliente foi simplificada ao máximo: dois campos, QR Code, e o pedido vai. Nenhum cadastro prévio, nenhuma senha.
>
> Para acessibilidade, integramos o **VLibras** em toda a interface — são **9,7 milhões de brasileiros** com deficiência auditiva que agora podem ler o cardápio, entender as regras dos torneios e navegar no sistema com independência. E todas as mensagens de erro falam em linguagem humana — o sistema não mostra 'Erro 401', mostra 'E-mail ou senha incorretos'.
>
> Como assistente de IA, o **Gemini 2.0 Flash** responde dúvidas sobre produtos e regras de TCG automaticamente, reduzindo a carga da equipe.
>
> Em conclusão: entregamos um sistema cloud-native completo, com **15 requisitos funcionais**, **12 não-funcionais**, **conformidade LGPD** e **100% dos testes passando**. Agradecemos a atenção da banca e estamos à disposição para perguntas."

---

## Distribuição de Tempo

| Slide | Apresentador | Tempo |
|-------|-------------|-------|
| 1 | Eduardo Taino | ~40 seg |
| 2 | Rafael Henrique | ~55 seg |
| 3 | Rickson Ferreira | ~65 seg |
| 4 | Hugo Matos | ~55 seg |
| 5 | Eduardo Taino | ~65 seg |
| 6 | Aldenor Lopes | ~65 seg |
| 7 | Reinaldo Barros | ~75 seg |
| **Total** | | **~420 seg / 7 min** |
