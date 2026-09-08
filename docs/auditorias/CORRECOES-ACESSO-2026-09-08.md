# Correções de acesso e recuperação — 08/09/2026

## Comportamento de acesso

- O login por QR cria clientes novos. Para reutilizar cadastro existente, exige sessão autenticada do mesmo cliente e do mesmo tenant. CPF e telefone não servem como credenciais. Contas inativas e funcionários são recusados, inclusive na busca somente por telefone.
- Nome e telefone de uma conta existente não são alterados pelo login.
- A ativação de conta exige a sessão do próprio cliente e senha ainda não definida. A gravação condicional impede duas ativações de sobrescreverem a senha. Uma conta com senha deve usar login ou recuperação de senha.
- Clientes antigos sem senha e sem sessão precisam de atendimento da loja para recuperar acesso. Não foi criado um serviço de SMS/OTP. A equipe deve confirmar a identidade antes de atualizar o contato e orientar a recuperação pelo contato cadastrado.
- O MCP aceita apenas Admin. Operator e Integration permanecem bloqueados até existir autorização por ferramenta.

## Confirmação de operações e entrega

Uma falha de envio SignalR é registrada sem transformar uma operação de comanda já persistida em erro HTTP. Os eventos ao vivo continuam sendo uma atualização auxiliar; consultar a API recupera o estado atual.

O e-mail de abertura de crediário originado de comanda é gravado em `crediario_email_outbox` no mesmo `SaveChanges` dos dados de negócio. O worker processa lotes de até 20 eventos por tenant, reserva a entrega por cinco minutos e só marca sucesso após o SMTP aceitar o envio. Configuração SMTP ausente e erros de transporte mantêm a pendência. O envio tem limite de um minuto.

A entrega é de pelo menos uma vez: se o SMTP aceitar a mensagem e o processo cair antes de registrar o sucesso, uma nova tentativa pode repetir o e-mail. Não há garantia de entrega exatamente uma vez nem de chegada à caixa de entrada.

## Banco e implantação

A migration `20260908123525_AddCrediarioEmailOutbox` cria uma tabela e um índice parcial; não modifica dados existentes. Deve ser aplicada aos schemas que atendem comandas, incluindo o tenant-zero quando utilizado. O mecanismo existente de migrations por tenant continua responsável pela aplicação. Nenhum deploy é executado por esta alteração.

## Consulta MCP

O faturamento do dia usa o intervalo UTC correspondente ao dia brasileiro, com início inclusivo e fim exclusivo. A agregação roda no banco, sem o limite de 200 vendas, e exclui vendas avulsas canceladas.

## Regressões automatizadas

- Conta privilegiada ou inativa não recebe sessão pelo QR.
- CPF/telefone de cadastro existente não substituem autenticação.
- Sessão de outro tenant não autoriza reutilização ou ativação.
- Ativação não sobrescreve senha nem ativa funcionário.
- MCP recusa Operator, Integration e Customer.
- Falha de SignalR não é propagada como falha da operação.
- Fechamento em crediário persiste a intenção de envio.
- Falha SMTP mantém a pendência; sucesso impede reenvio normal.
- Faturamento inclui mais de 200 vendas e respeita limites do dia e cancelamentos.

Validação local: `dotnet test softNerd.sln --no-restore --verbosity quiet` passou com 971 testes, zero falhas e zero ignorados, usando PostgreSQL real. `npm run lint`, `npx tsc --noEmit` e `git diff --check` também passaram. Não foram enviados e-mails reais nem executados testes contra produção.
