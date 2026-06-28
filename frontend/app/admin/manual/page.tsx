'use client'
import { useEffect } from 'react'

const LOJA = 'Santuário Nerd'
const VERSION = 'v1.10.0'
const DATA = '28/06/2026'

const SECOES = [
  {
    num: '01',
    titulo: 'Dashboard',
    cor: '#00F0A8',
    itens: [
      { t: 'Comandas em destaque', d: 'Ao abrir o dashboard, as comandas ativas aparecem imediatamente, sem scroll. Os 4 KPIs no topo (comandas ativas, receita do dia, valor em aberto, estoque baixo) ficam sempre visíveis.' },
      { t: 'Tab Ativas', d: 'Lista em tempo real de todas as comandas abertas, com busca por cliente e botão para abrir nova comanda.' },
      { t: 'Tab Histórico', d: 'Comandas fechadas e canceladas do dia selecionado. Inclui breakdown por forma de pagamento no topo.' },
      { t: 'Tab Análises', d: 'Painéis financeiros colapsáveis: detalhe do dia, gráfico de receita 7 dias, previsão do mês, patrimônio, top clientes, LGPD e pré-inscrições. Cada painel mantém estado aberto/fechado entre visitas.' },
      { t: 'Atualização automática', d: 'Intervalo configurável: 15s, 30s, 1 minuto ou manual. Também recebe eventos em tempo real via SignalR.' },
    ],
    dicas: [
      'Quais painéis aparecem na tab Análises é controlado em Configurações → Dashboard.',
      'O esquema de cores do gráfico (Padrão, Azul, Neon) é configurável por usuário.',
    ],
  },
  {
    num: '02',
    titulo: 'Comandas (Mesa / QR Code)',
    cor: '#3EC2F2',
    itens: [
      { t: 'Como funciona', d: 'O cliente escaneia o QR Code da mesa e abre a própria comanda. O sistema identifica o cliente automaticamente pelo CPF ou WhatsApp.' },
      { t: 'Adicionar itens', d: 'No painel admin, clique na comanda aberta e pesquise o produto. Você também pode adicionar itens manualmente com nome e preço.' },
      { t: 'Fechar a comanda', d: 'Clique em "Fechar Comanda", escolha a forma de pagamento (Dinheiro, Pix, Cartão, Crediário, Pontos ou Cashback) e confirme. O sistema desconta o estoque automaticamente.' },
      { t: 'Split de pagamento', d: 'É possível usar duas formas de pagamento ao mesmo tempo. Selecione a segunda forma e informe o valor dela.' },
      { t: 'Cancelar comanda', d: 'Se o cliente desistir, use "Cancelar". O estoque não é alterado e a comanda some do painel.' },
      { t: 'Agrupar itens', d: 'Itens iguais são somados automaticamente na visualização para facilitar a conferência.' },
    ],
    dicas: [
      'Comandas com status "Aberta" ainda não têm itens. "Em Andamento" já tem pelo menos um item adicionado.',
      'O admin pode abrir uma comanda manualmente em nome de um cliente pelo botão "Nova Comanda".',
    ],
  },
  {
    num: '03',
    titulo: 'Venda Avulsa — Frente de Caixa (PDV)',
    cor: '#4ADE80',
    itens: [
      { t: 'Quando usar', d: 'Para vendas rápidas no balcão, sem QR Code. Ideal para clientes que chegam e pagam na hora.' },
      { t: 'Wizard 3 etapas', d: 'O PDV guia você por: 1) selecionar cliente, 2) adicionar produtos, 3) escolher pagamento. Navegue livremente entre etapas sem perder o carrinho.' },
      { t: 'Layout 2 colunas no step de produtos', d: 'Ao chegar na etapa de produtos, o modal expande mostrando o catálogo à esquerda e o carrinho à direita. Cada item adicionado aparece imediatamente no painel do carrinho com controles de quantidade — sem precisar avançar de etapa para ver o que foi adicionado.' },
      { t: 'Adicionar produtos', d: 'Pesquise pelo nome, categoria ou código de barras. Clique em + para adicionar; o produto vai direto para o carrinho ao lado.' },
      { t: 'Desconto rápido', d: 'Aplique desconto percentual diretamente na etapa de pagamento. O desconto padrão é pré-configurável nas Preferências.' },
      { t: 'Split de pagamento', d: 'Selecione uma segunda forma de pagamento e informe o valor. O saldo restante é calculado automaticamente.' },
      { t: 'Analytics do PDV', d: 'Histórico com gráfico de pico de horário, top produtos vendidos no período e breakdown por forma de pagamento.' },
    ],
    dicas: [
      'O desconto padrão (0 a 20%) pode ser pré-configurado em Configurações → Frente de Caixa.',
      'Sem cliente selecionado, a venda é anônima — aparece apenas nos relatórios gerais.',
    ],
  },
  {
    num: '04',
    titulo: 'Estoque (Produtos)',
    cor: '#FB923C',
    itens: [
      { t: 'Cards de resumo', d: 'Topo da tela exibe 4 cards em tempo real: total de peças em estoque, valor total imobilizado em estoque, quantidade de itens com estoque baixo e quantidade de itens zerados.' },
      { t: 'Filtros por situação', d: 'Use os chips Todos / Normal / Estoque Baixo / Zerado para filtrar a lista. Os filtros Baixo e Zerado reordenam automaticamente do pior para o melhor.' },
      { t: 'Drawer de detalhe', d: 'Clique em qualquer linha da tabela para abrir o painel lateral com imagem do produto, categoria, código de barras, preço, custo, barra de margem, barra de estoque vs mínimo, valor imobilizado e botões rápidos + / − de ajuste.' },
      { t: 'Cadastrar produto', d: 'Vá em Estoque → Novo Produto. Preencha nome, categoria, preço de custo, preço de venda e estoque inicial.' },
      { t: 'Estoque mínimo', d: 'Defina um estoque mínimo para receber alertas quando o produto estiver baixo. Aparece nos cards de resumo e no filtro de situação.' },
      { t: 'Promoção', d: 'Ative o campo "Em Promoção" e informe o preço promocional. O sistema usa o preço com desconto automaticamente nas vendas.' },
      { t: 'Código de barras', d: 'Cadastre o código de barras para agilizar vendas no caixa — basta escanear para adicionar o produto.' },
      { t: 'Ajuste de estoque', d: 'Para corrigir o estoque (entrada de mercadoria, inventário), use o botão de ajuste manual no drawer ou no modal de edição.' },
      { t: 'Desativar produto', d: 'Produtos inativos não aparecem nas vendas, mas o histórico é mantido. Use isso em vez de excluir.' },
    ],
    dicas: [
      'O estoque é descontado automaticamente a cada venda — comanda ou PDV.',
      'Produtos com estoque zerado ainda podem ser vendidos manualmente, mas aparecem com aviso.',
    ],
  },
  {
    num: '05',
    titulo: 'Clientes, Pontos & Cashback',
    cor: '#C084FC',
    itens: [
      { t: 'Cadastrar cliente', d: 'Clique em "Novo Cliente". Nome é obrigatório; CPF, WhatsApp e e-mail são opcionais, mas ajudam na identificação.' },
      { t: 'Pontos Maikon', d: 'A cada R$1 gasto, o cliente ganha 1 ponto. Os pontos expiram em 30 dias após a última compra e podem ser usados como desconto.' },
      { t: 'Pontos não acumulam com cashback', d: 'Se qualquer parte do pagamento usar cashback, o cliente não acumula pontos naquela venda.' },
      { t: 'Adicionar pontos manualmente', d: 'Selecione o cliente no painel e informe a quantidade de pontos e o motivo (ex: "Campeonato de Pokémon").' },
      { t: 'Cashback (Saldo)', d: 'Diferente de pontos — é saldo em reais que o cliente pode usar como pagamento. Crédite ou débite manualmente pelo painel.' },
      { t: 'Histórico completo', d: 'Clique em "Ver Histórico" no painel do cliente para ver todas as comandas, vendas no caixa, crediários e campeonatos em um único lugar.' },
    ],
    dicas: [
      'Pontos e cashback são coisas diferentes: pontos têm validade de 30 dias; cashback é saldo em reais sem validade.',
      'Para redefinir a senha de um cliente, use o botão "Redefinir Senha" no painel lateral.',
    ],
  },
  {
    num: '06',
    titulo: 'Crediário (Fiado)',
    cor: '#F97316',
    itens: [
      { t: 'O que é', d: 'O cliente leva os produtos e paga depois. O sistema cria uma dívida vinculada ao cliente com vencimento em 30 dias.' },
      { t: 'Como abrir', d: 'Ao fechar uma comanda ou venda avulsa, selecione "Crediário" como forma de pagamento. O cliente precisa estar cadastrado.' },
      { t: 'Um crediário por vez', d: 'Cada cliente só pode ter um crediário aberto. Novas compras no crediário acumulam no mesmo saldo.' },
      { t: 'Ver os itens', d: 'No painel de crediário, expanda o card do cliente para ver exatamente o que ele levou em todas as visitas.' },
      { t: 'Pagamento parcial', d: 'Clique em "Registrar Pagamento" e informe o valor pago e a forma de pagamento. O saldo é atualizado automaticamente.' },
      { t: 'Quitar o crediário', d: 'Quando o valor total for pago, clique em "Marcar como Pago". O cliente fica liberado para abrir um novo.' },
      { t: 'Crediário vencido', d: 'Aparece em vermelho no painel quando passou dos 30 dias sem pagamento. Use para cobrar os clientes em atraso.' },
    ],
    dicas: [
      'Pagar uma dívida de crediário NÃO gera pontos para o cliente.',
      'O vencimento é renovado automaticamente sempre que o cliente faz um pagamento parcial.',
    ],
  },
  {
    num: '07',
    titulo: 'Campeonatos',
    cor: '#FBBF24',
    itens: [
      { t: 'Criar campeonato', d: 'Vá em Campeonatos → Novo. Informe nome, jogo (Pokémon, Magic, etc.), data, taxa de inscrição e número máximo de participantes.' },
      { t: 'Status do campeonato', d: '"Planejado" → "Inscrições Abertas" → "Em Andamento" → "Finalizado". Mude o status conforme o evento avança.' },
      { t: 'Inscrever participantes', d: 'No painel do campeonato, adicione participantes manualmente ou deixe que eles se inscrevam pela landing page pública.' },
      { t: 'Pré-inscrições da landing page', d: 'Clientes sem conta podem se pré-inscrever pelo link público. Você aprova ou rejeita as pré-inscrições no painel.' },
      { t: 'Definir colocações', d: 'Após o campeonato, clique no participante e informe o lugar (1º, 2º, 3º...). O sistema monta o pódio automaticamente.' },
    ],
    dicas: [
      'A taxa de inscrição é registrada manualmente — o sistema não cobra automaticamente.',
      'Use "Cancelado" para campeonatos que não aconteceram para manter o histórico limpo.',
    ],
  },
  {
    num: '08',
    titulo: 'Relatórios',
    cor: '#22D3EE',
    itens: [
      { t: 'Relatório PDV', d: 'Mostra todas as vendas avulsas do período: receita dia a dia, top produtos vendidos e formas de pagamento usadas.' },
      { t: 'Relatório de Clientes', d: 'Lista todos os clientes com pontos, cashback e status de atividade. Ajuda a identificar quem está ativo e quem parou de visitar.' },
      { t: 'Comandas Abertas', d: 'Mostra as comandas que estão há mais dias abertas. Útil para identificar clientes que ainda não fecharam a conta.' },
      { t: 'Relatório Financeiro', d: 'Visão consolidada de receitas por período, formas de pagamento e ticket médio.' },
      { t: 'Exportar PDF', d: 'Cada relatório tem botão de exportação em PDF para imprimir ou compartilhar.' },
    ],
  },
  {
    num: '09',
    titulo: 'Perfis de Acesso (Operadores)',
    cor: '#A78BFA',
    itens: [
      { t: 'O que são perfis', d: 'Perfis permitem criar usuários operadores com acesso restrito. Você define quais módulos cada perfil pode acessar.' },
      { t: 'Criar um perfil', d: 'Vá em Administração → Perfis de Acesso → Novo Perfil. Dê um nome (ex: "Caixa") e marque as permissões desejadas.' },
      { t: 'Permissões disponíveis', d: 'Dashboard, PDV, Comandas, Estoque, Categorias, Clientes, Crediário, Campeonatos, Financeiro, Relatórios, Anúncios, Cartas TCG, QR Codes e LGPD.' },
      { t: 'Atribuir perfil a operador', d: 'Em Clientes → aba Operadores, crie ou edite um operador e selecione o perfil. O operador verá apenas os módulos do perfil ao fazer login.' },
      { t: 'Alterar permissões', d: 'Edite o perfil a qualquer momento. As mudanças valem na próxima vez que o operador fizer login.' },
      { t: 'Excluir perfil', d: 'Só é possível excluir um perfil se nenhum operador estiver usando. Reatribua os operadores antes.' },
    ],
    dicas: [
      'Admin sempre tem acesso a tudo — perfis se aplicam apenas a operadores.',
      'Um operador sem perfil atribuído não consegue acessar nenhum módulo.',
    ],
  },
  {
    num: '10',
    titulo: 'Anúncios, Banners & Catálogo TCG',
    cor: '#FB7185',
    itens: [
      { t: 'Criar anúncio', d: 'Vá em Anúncios → Novo. Escolha o tipo, escreva o texto, adicione imagem e defina data de expiração se necessário.' },
      { t: 'Banners do hero', d: 'Aparecem como fundo rotativo do hero da landing page. Cadastre múltiplos banners para rotação automática.' },
      { t: 'Avisos e destaques', d: 'Carrossel rotativo abaixo do hero com setas de navegação. Pausa automaticamente ao tocar.' },
      { t: 'Expiração automática', d: 'Defina uma data de expiração e o anúncio some automaticamente. Útil para promoções com prazo.' },
      { t: 'Catálogo TCG', d: 'Catálogo integrado de cartas (Pokémon, Magic, etc.) com preços de mercado. Pesquise por nome e adicione diretamente na comanda.' },
    ],
  },
  {
    num: '11',
    titulo: 'Atalhos de Teclado',
    cor: '#F472B6',
    itens: [
      { t: 'Navegar pelo teclado', d: 'Quando nenhum campo de texto está focado, pressione uma tecla para ir direto à página: D → Dashboard, P → PDV (Frente de Caixa), E → Estoque, U → Clientes, C → Crediário, F → Financeiro, R → Relatórios, A → Campeonatos.' },
      { t: 'Ver todos os atalhos', d: 'Pressione ? (shift + /) em qualquer tela para abrir o painel de atalhos com a lista completa. Pressione ? novamente ou Esc para fechar.' },
      { t: 'Fechar com Esc', d: 'A tecla Esc fecha modais, painéis flutuantes e o painel de atalhos. Funciona em qualquer contexto.' },
      { t: 'Badges no menu lateral', d: 'Ao passar o mouse sobre um item do menu no desktop, aparece a tecla de atalho correspondente em destaque ao lado do nome.' },
      { t: 'Não interfere com digitação', d: 'Os atalhos de navegação ficam desativados enquanto você digita em campos de texto, busca ou formulários — só a tecla ? continua ativa.' },
    ],
    dicas: [
      'Os atalhos de letras são case-insensitive — maiúscula ou minúscula, funciona igual.',
      'Para navegar com teclado no celular (teclado físico Bluetooth), os atalhos funcionam normalmente.',
    ],
  },
  {
    num: '12',
    titulo: 'Configurações e Preferências',
    cor: '#94A3B8',
    itens: [
      { t: 'Preferências por perfil', d: 'Cada usuário tem configurações salvas no servidor. Mudanças refletem em todos os dispositivos e são aplicadas em tempo real, sem recarregar a página.' },
      { t: 'Assistente IA — botão', d: 'Ative/desative o chat IA. Escolha entre botão arrastável (posição salva entre sessões) ou fixo em um dos 4 cantos da tela.' },
      { t: 'VLibras — Acessibilidade', d: 'Ative/desative o widget de tradução em Libras e defina em qual canto da tela ele aparece.' },
      { t: 'Dashboard — Painéis', d: 'Controle quais painéis aparecem na tab Análises: detalhe financeiro, gráfico 7 dias, previsão do mês, patrimônio, top clientes, LGPD e pré-inscrições.' },
      { t: 'Dashboard — Intervalo e cores', d: 'Configure o intervalo de atualização automática (15s, 30s, 1min ou manual) e o esquema de cores do gráfico (Padrão, Azul, Neon).' },
      { t: 'Frente de Caixa — Desconto padrão', d: 'Pré-selecione o desconto padrão (0 a 20%) que aparece ao abrir uma nova venda no PDV.' },
    ],
    dicas: [
      'Todas as mudanças são aplicadas em tempo real — sem precisar recarregar a página.',
      'Use "Resetar layout" nas configurações do Dashboard para reabrir todos os painéis colapsados.',
    ],
  },
  {
    num: '13',
    titulo: 'Financeiro & Curva ABC',
    cor: '#38BDF8',
    itens: [
      { t: 'Filtro de período', d: 'Selecione o intervalo de datas no topo ou use o mini filtro abaixo do gráfico para ajustar o período sem precisar rolar a página.' },
      { t: 'Gráfico de receita por dia', d: 'Barras animadas com entrada suave. Clique em qualquer barra para abrir o detalhe do dia: donut por forma de pagamento, receita, custo e margem.' },
      { t: 'Gráfico de formas de pagamento', d: 'Pizza interativa mostrando a proporção de cada método. Quando há apenas um método no período, a pizza aparece completa sem erro visual.' },
      { t: 'Visão Simples', d: 'Tabela de produtos com receita, quantidade, preço médio, custo e margem. Colunas ordenáveis por clique.' },
      { t: 'Curva ABC', d: 'Classifica automaticamente os produtos pela contribuição na receita: A = top 80% (produtos mais importantes), B = 80–95%, C = restante. Use para decidir o que priorizar no estoque.' },
      { t: 'Gráfico de Pareto', d: 'Visualização da Curva ABC com barras coloridas por classe e linha de acumulado. Linhas de referência nos 80% e 95% facilitam a leitura.' },
      { t: 'Filtros na Curva ABC', d: 'Clique em qualquer badge de classe (A, B ou C) ou em uma categoria para filtrar a tabela. Clique novamente para limpar o filtro.' },
    ],
    dicas: [
      'Classe A = poucos produtos que respondem por 80% da receita — são os que nunca podem faltar no estoque.',
      'O mini filtro de período abaixo do gráfico atualiza os dados sem disparar uma nova requisição separada — usa o mesmo estado do filtro do topo.',
    ],
  },
  {
    num: '15',
    titulo: 'Cartas TCG & Deck Builder',
    cor: '#F59E0B',
    itens: [
      { t: 'Jogos suportados', d: 'Pokémon TCG (pokemontcg.io), Magic: The Gathering (Scryfall), Yu-Gi-Oh! (YGOProDeck) e LoL: Riftbound (Riftcodex + Scrydex). As APIs de Scryfall, YGOProDeck e Riftcodex são gratuitas e sem chave. Pokémon e Scrydex têm chaves opcionais configuráveis no backend.' },
      { t: 'Busca por nome', d: 'Digite o nome (ou parte) da carta na barra de busca e selecione o jogo. Os resultados aparecem em grade com imagem, nome e preço em USD e R$.' },
      { t: 'Busca por código de set', d: 'Busca pela combinação set + número retorna a carta exata. Pokémon: "PAL 058" (código PTCGO) ou "sv3pt5 094" (ID do set). MTG: "MH3 232". YGO: "DUNE-EN001" ou passcode (89631139). LoL: "OGN-296".' },
      { t: 'Busca somente por filtros', d: 'Para Pokémon, é possível buscar sem digitar nome — apenas com filtros. Exemplo: clique em Filtros, selecione "Regulation Mark G" + "Standard" e pressione Buscar para ver todas as cartas legais com aquela marca.' },
      { t: 'Filtros básicos por jogo', d: 'Pokémon: raridade, supertipo (Pokémon/Trainer/Energy), set. MTG: raridade, tipo de carta, set. Yu-Gi-Oh!: tipo de monstro/magia/armadilha, atributo, set. LoL Riftbound: raridade, tipo, set.' },
      { t: 'Filtros avançados — Pokémon', d: 'Clique no ícone de filtros para expandir: Subtipo (Basic/Stage 1/Stage 2/EX/GX/V/VMAX/VSTAR/Supporter/Item…), Tipo de Energia (Fire/Water/Grass…), Regulation Mark (A a H), Legalidade (Standard/Expanded/Unlimited), Série do set (Scarlet & Violet/Sword & Shield…), Código PTCGO, Artista, Evolui de, Nº Pokédex, HP mínimo/máximo, Data de lançamento (de/até).' },
      { t: 'Detalhe da carta', d: 'Clique em qualquer carta para abrir o painel completo: imagem ampliada, HP/ATK-DEF/Força-Resistência (adaptado ao jogo), set, série, raridade, tipos, artista, texto de regras ou efeito, fraquezas e resistências (Pokémon), variantes de preço TCGPlayer (USD) com todas as versões (Normal, Holo, Reverse, 1ª Ed., Unlimited), preços CardMarket (EUR: Tendência, Média, Avg 30d, Reverse Holo) e conversão para R$.' },
      { t: 'Preços TCGPlayer e CardMarket', d: 'Cada carta Pokémon exibe preços separados por variante: Normal, Holofoil, Reverse Holofoil, 1ª Edição Normal, 1ª Edição Holo, Unlimited Normal, Unlimited Holo — cada uma com low/mid/high/market em USD. O CardMarket exibe em EUR: Média de venda, Tendência, Mais baixo, Reverse Holo Trend, Médias 1d/7d/30d.' },
      { t: 'Taxa BRL em tempo real', d: 'O widget de cotação no topo da tela mostra USD → R$ atualizado automaticamente. Todos os preços TCGPlayer são convertidos para R$ usando essa taxa.' },
      { t: 'Adicionar ao estoque', d: 'No detalhe da carta (admin), use o botão "Adicionar ao Estoque" para criar um produto baseado nos dados da carta TCG — já preenche nome, imagem e preço sugerido.' },
      { t: 'Deck Builder', d: 'Acesse em Minha Conta → Meus Decks. Crie um deck para um jogo específico e use a busca integrada para adicionar cartas. Os mesmos filtros avançados do admin estão disponíveis na busca do deck.' },
      { t: 'Busca por câmera', d: 'No Deck Builder, clique no ícone de câmera para fotografar uma carta. O sistema recorta a faixa inferior (onde fica o código) e tenta detectar o código automaticamente — se não detectar, você digita manualmente e confirma.' },
      { t: 'Importar lista', d: 'Cole uma lista no formato PTCG Live / Limitlesstcg (ex: "4 Pikachu PAL 058" / "2 Raichu PAR 021") e clique em Importar. O sistema busca cada carta e adiciona ao deck respeitando os limites de cópias.' },
    ],
    dicas: [
      'LoL: Riftbound usa duas APIs em paralelo — Riftcodex (gratuita) e Scrydex (opcional, com preços de mercado). Se a Scrydex não estiver configurada, o Riftcodex funciona sozinho.',
      'O cache de busca tem TTL de 5 minutos — refazer a mesma busca dentro desse período retorna resultado imediato sem chamar a API.',
      'Para cartas antigas (Base Set, Gym, Neo…), o preço de mercado mais completo é o CardMarket (EUR) — o TCGPlayer tem menos vendedores internacionais nessas coleções.',
    ],
  },
  {
    num: '16',
    titulo: 'Assistente IA — Voz & Navegação',
    cor: '#A78BFA',
    itens: [
      { t: 'Como acessar', d: 'Clique no botão roxo flutuante no canto da tela (arrastável). O widget abre acima do botão.' },
      { t: 'Perguntas sobre a loja', d: 'Pergunte sobre vendas do dia, estoque baixo, crediários em aberto, top produtos e muito mais. O assistente responde com dados reais em tempo real.' },
      { t: 'Navegação por comando', d: 'Digite ou fale "abre o estoque", "vai pro financeiro", "abre a frente de caixa" e o assistente navega automaticamente para a página e fecha o widget.' },
      { t: 'Entrada por voz', d: 'Clique no ícone de microfone no campo de texto. Fale normalmente em português — a transcrição é enviada automaticamente ao assistente. Disponível no Chrome e Edge.' },
      { t: 'Resposta em voz', d: 'Clique no ícone de alto-falante no cabeçalho do widget para ativar leitura em voz alta das respostas em PT-BR.' },
      { t: 'Sugestões rápidas', d: 'Quando o chat está vazio, aparecem sugestões de perguntas e comandos para facilitar o uso.' },
    ],
    dicas: [
      'O microfone funciona melhor no Chrome e Edge — Firefox não tem suporte nativo ao reconhecimento de voz.',
      'Os dados enviados ao assistente são anonimizados (LGPD): nomes de clientes são substituídos por "Cliente #N" antes de ir ao Google Gemini.',
    ],
  },
]

export default function ManualPdfPage() {
  useEffect(() => {
    document.title = `Manual do Sistema — ${LOJA} ${VERSION}`
  }, [])

  return (
    <>
      <style>{`
        @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap');

        * { box-sizing: border-box; margin: 0; padding: 0; }

        body {
          font-family: 'Inter', sans-serif;
          background: #fff;
          color: #111;
          font-size: 13px;
          line-height: 1.55;
        }

        .page { max-width: 820px; margin: 0 auto; padding: 32px 40px; }

        /* Botão — some no print */
        .print-btn {
          display: flex;
          align-items: center;
          gap: 8px;
          background: #00F0A8;
          color: #000;
          font-weight: 700;
          font-size: 13px;
          border: none;
          border-radius: 10px;
          padding: 10px 20px;
          cursor: pointer;
          margin-bottom: 32px;
        }
        .print-btn:hover { background: #00d494; }

        /* Capa */
        .capa {
          border-bottom: 3px solid #00F0A8;
          padding-bottom: 24px;
          margin-bottom: 32px;
        }
        .capa-badge {
          display: inline-block;
          background: #00F0A820;
          color: #00b87a;
          border: 1px solid #00F0A840;
          border-radius: 20px;
          padding: 3px 12px;
          font-size: 11px;
          font-weight: 700;
          letter-spacing: .05em;
          text-transform: uppercase;
          margin-bottom: 12px;
        }
        .capa h1 {
          font-size: 26px;
          font-weight: 800;
          color: #0C3D5A;
          margin-bottom: 4px;
        }
        .capa-meta {
          font-size: 12px;
          color: #6b7280;
          margin-top: 6px;
        }

        /* Índice */
        .indice { margin-bottom: 32px; }
        .indice h2 { font-size: 11px; font-weight: 700; color: #9ca3af; text-transform: uppercase; letter-spacing: .08em; margin-bottom: 10px; }
        .indice-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 4px 32px; }
        .indice-item { display: flex; align-items: center; gap: 6px; font-size: 12px; color: #374151; padding: 3px 0; }
        .indice-num { font-weight: 700; color: #9ca3af; font-size: 11px; width: 20px; }

        /* Seção */
        .secao {
          margin-bottom: 28px;
          break-inside: avoid;
          page-break-inside: avoid;
        }
        .secao-header {
          display: flex;
          align-items: center;
          gap: 10px;
          margin-bottom: 10px;
          padding-bottom: 8px;
          border-bottom: 2px solid #f3f4f6;
        }
        .secao-num {
          font-size: 11px;
          font-weight: 800;
          color: #9ca3af;
          min-width: 22px;
        }
        .secao-title {
          font-size: 15px;
          font-weight: 700;
          color: #111;
          flex: 1;
        }
        .secao-dot {
          width: 8px;
          height: 8px;
          border-radius: 50%;
          flex-shrink: 0;
        }

        /* Itens */
        .item { display: flex; gap: 10px; margin-bottom: 6px; }
        .item-bullet {
          width: 18px;
          height: 18px;
          border-radius: 50%;
          background: #f3f4f6;
          border: 1px solid #e5e7eb;
          display: flex;
          align-items: center;
          justify-content: center;
          font-size: 9px;
          font-weight: 700;
          color: #9ca3af;
          flex-shrink: 0;
          margin-top: 2px;
        }
        .item-t { font-weight: 600; color: #111; font-size: 12px; }
        .item-d { color: #4b5563; font-size: 12px; margin-top: 1px; }

        /* Dicas */
        .dicas { margin-top: 8px; background: #f9fafb; border-radius: 8px; padding: 8px 12px; border-left: 3px solid #00F0A8; }
        .dicas-label { font-size: 10px; font-weight: 700; text-transform: uppercase; letter-spacing: .06em; color: #00b87a; margin-bottom: 4px; }
        .dica { font-size: 11px; color: #4b5563; margin-bottom: 2px; }
        .dica::before { content: '→ '; color: #00b87a; font-weight: 700; }

        /* Rodapé */
        .rodape { margin-top: 40px; padding-top: 16px; border-top: 1px solid #e5e7eb; display: flex; justify-content: space-between; font-size: 11px; color: #9ca3af; }

        @media print {
          .print-btn { display: none !important; }
          body { font-size: 12px; }
          .page { padding: 16px 24px; max-width: 100%; }
          .capa h1 { font-size: 22px; }
          .secao { margin-bottom: 20px; }
          @page { margin: 1.5cm; size: A4; }
        }
      `}</style>

      <div className="page">
        {/* Botão imprimir */}
        <button className="print-btn" onClick={() => window.print()}>
          🖨️ Imprimir / Salvar como PDF
        </button>

        {/* Capa */}
        <div className="capa">
          <div className="capa-badge">Manual do Usuário</div>
          <h1>{LOJA} — Sistema de Gestão</h1>
          <div className="capa-meta">
            Versão {VERSION} &nbsp;·&nbsp; Atualizado em {DATA} &nbsp;·&nbsp; Uso interno
          </div>
        </div>

        {/* Índice */}
        <div className="indice">
          <h2>Índice</h2>
          <div className="indice-grid">
            {SECOES.map(s => (
              <div key={s.num} className="indice-item">
                <span className="indice-num">{s.num}</span>
                <span>{s.titulo}</span>
              </div>
            ))}
          </div>
        </div>

        {/* Seções */}
        {SECOES.map(s => (
          <div key={s.num} className="secao">
            <div className="secao-header">
              <span className="secao-num">{s.num}</span>
              <span className="secao-title">{s.titulo}</span>
              <span className="secao-dot" style={{ background: s.cor }} />
            </div>

            {s.itens.map((item, i) => (
              <div key={i} className="item">
                <span className="item-bullet">{i + 1}</span>
                <div>
                  <div className="item-t">{item.t}</div>
                  <div className="item-d">{item.d}</div>
                </div>
              </div>
            ))}

            {s.dicas && s.dicas.length > 0 && (
              <div className="dicas">
                <div className="dicas-label">Dicas</div>
                {s.dicas.map((d, i) => (
                  <div key={i} className="dica">{d}</div>
                ))}
              </div>
            )}
          </div>
        ))}

        {/* Rodapé */}
        <div className="rodape">
          <span>{LOJA} · Sistema de Gestão Interno</span>
          <span>{VERSION} · {DATA}</span>
        </div>
      </div>
    </>
  )
}
