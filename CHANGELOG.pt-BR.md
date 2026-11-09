# Changelog

> Tradução de [CHANGELOG.md](CHANGELOG.md), que é o original em inglês.

## 4.6.3

- **O nome da música abre e fecha com fluidez:** a capa e o equalizador agora ficam nas bordas da ilha o tempo todo, então tudo cresce e encolhe junto (antes o conteúdo se comprimia primeiro e a ilha vinha atrás). O conteúdo não some nem fica borrado enquanto a ilha alarga, e o nome não volta com força total antes de sumir

## 4.6.2

- **O nome da música não corta mais a capa e o equalizador:** a ilha agora toma a largura nova assim que o nome abre (antes ela ficava estreita e a capa e o equalizador eram cortados), e o conteúdo acompanha as bordas da ilha enquanto ela cresce e encolhe
- **O aviso depois de atualizar:** espera um momento depois que a ilha subiu (ele podia se perder enquanto o app ainda abria) e fica seis segundos. O mesmo vale para o aviso depois de uma falha

## 4.6.1

- **O nome da música, no espaço da própria ilha:** na ilha normal o nome não troca mais a ilha por outra forma com um símbolo. A ilha abre um pouco mais, a capa desliza para fora e a nova desliza para dentro, o nome entra ao lado, e depois de um momento o nome some e a ilha volta ao tamanho dela. Funciona também com o nome sempre visível
- **Menos bugs na troca de música:** uma capa ou um título que chega durante o deslize não é mais sobrescrito pelo antigo, e duas trocas seguidas não tocam uma em cima da outra

## 4.6.0

- **O nome da música:** quando a música muda, a capa e o nome deslizam para um lado (a direção segue próxima ou anterior), a ilha dá um pequeno empurrão, e na ilha normal o nome aparece por um momento ao lado da capa e depois some. Novo em Ajustes, Atividades: **Nome da música sempre visível** (desligado por padrão)
- **As páginas entram uma a uma:** quando a ilha abre, cada linha da página (música, Claude, timer, atalhos, sistema) entra um pouco depois da anterior

## 4.5.3

- **Depois de atualizar:** quando o app reinicia na versão nova, a ilha mostra o visto verde que aparece com um pulinho ("Atualizado para a versão X") no lugar do sino cinza. Depois de uma falha ela reabre com o alerta âmbar

## 4.5.2

- **Atualizações mais rápidas pela ilha:** quando o endereço de download não responde, ele é ignorado pelos próximos 30 minutos (esperava por ele de novo a cada arquivo), a lista de arquivos da release é pedida uma vez só, e depois de um download que você pediu o app reinicia na hora em vez de esperar o aviso sumir
- **Perguntas do Claude na ilha:** uma pergunta (AskUserQuestion) aparece em palavras normais, a pergunta e as opções, e diz para responder no Claude, em vez de mostrar texto cru e "longo demais para aprovar"

## 4.5.1

- **Linha fina por padrão:** com uma janela maximizada na frente a ilha vira a linha fina, e as atividades (música e o resto) também viram a linha fina, para nunca cobrir as abas do navegador. Instalações novas já começam assim, e as cópias já instaladas ganham as duas opções ligadas uma vez (dá para desligar de novo nos Ajustes)

## 4.5.0

- **Contorno do timer:** no timer compacto (e no Pomodoro), um anel corre em volta da pílula: cheio no começo, e some no sentido horário conforme o tempo acaba (laranja no foco, verde na pausa), sobre um trilho fraquinho
- **Ajustes, Essenciais:** os dois botões de baixo ("Mais ajustes" e atualizações) agora têm a mesma largura, e o texto da atualização é curto para nunca ser cortado ("Atualizações", "Procurando…", "Baixar 4.5.0", "Atualizado")

## 4.4.3

- **Pergunta de atualização na ilha:** agora tem um botão verde "Baixar" e fica na frente mesmo com o mouse sobre a ilha (antes, passar o mouse abria a música e a resposta se perdia). Espera 30 segundos

## 4.4.2

- **Avisos de atualização na ilha:** acabou o sino cinza. Procurar e baixar mostram a seta girando, uma versão nova uma seta verde, "atualizado" um visto verde que aparece com um pulinho, e uma falha um alerta âmbar. Cada aviso substitui o anterior em vez de esperar na fila

## 4.4.1

- **Ajustes:** a linha de atualização em Essenciais quebra o texto em vez de cortá-lo, e tem mais espaço que o lado "Mais ajustes"
- O cartão "Novidades" agora descreve a 4.4

## 4.4.0

- **Menu da bandeja:** ícones em cada item, "Sair" virou "Fechar" em vermelho, o timer saiu dele, e o fundo segue o tema (preto no escuro, branco no claro). Ele liga e desliga a linha fina e ativa o Não perturbe da ilha (os avisos não aparecem; só o que o Claude pergunta continua aparecendo)
- **Procurar atualizações pela bandeja:** não abre mais os Ajustes. Procura em segundo plano e, se houver versão nova, a ilha pergunta: toque para baixar, conferir e reiniciar
- **Ajustes, Páginas:** arraste uma página pela alça para reordenar, no lugar das setas

## 4.3.2

- **Ajustes:** quando uma versão nova é encontrada, o botão de atualização ("Baixar a atualização X") e a seta ficam verdes (assim como "Atualizado"), e em Essenciais ele tem mais espaço das bordas

## 4.3.1

- **Windows Update:** "Instalando atualizações" aparecia quando nada estava sendo instalado (o Windows usa o processo de manutenção, o TiWorker, para limpeza e quando a página do Update procura atualizações). Agora é preciso que o próprio registro do cliente de atualização diga que uma instalação começou e ainda não terminou, além do TiWorker trabalhando

## 4.3.0

- **Página de atalhos:** uma página nova com a ilha aberta, com botões redondos para silenciar o microfone ou o alto-falante (vermelho ou branco enquanto mudo), tirar uma captura (Win+Shift+S), abrir os ajustes de som do Windows, e um controle de volume. Em Ajustes, Comportamento, Páginas há o interruptor e o lugar na ordem. Só os dispositivos padrão são mexidos
- **Pomodoro:** pontos na página do timer mostram o ciclo em que você está, e a linha fina pisca (verde na pausa, laranja no foco) quando uma fase muda. A linha fina também pisca em verde quando o Claude termina

## 4.2.4

- **Um `.exe` baixado que não é o mais novo:** ao abri-lo para instalar, ele primeiro procura uma versão mais nova e oferece baixar e instalar essa no lugar (com as mesmas conferências de assinatura e hash de uma atualização). E se já houver instalada uma versão mais nova que o arquivo, ele avisa que isso é um retrocesso e pergunta, com "Não" como padrão

## 4.2.3

- **Atualizações:** "Procurar atualizações" falhava em algumas conexões porque o endereço de download das versões no GitHub demorava demais; quando ele não responde, o app agora pede o mesmo arquivo à API do GitHub (a assinatura e o hash são conferidos do mesmo jeito)
- **Ajustes, cartão de novidades:** o título mostra a versão completa, cada mudança tem um título e uma frase, o botão de dispensar é um xis no canto, e o link tem o símbolo do GitHub e uma seta. Todo link que abre uma página da internet agora termina com essa seta. A linha "Histórico de mudanças" tinha sumido e voltou
- **Linha fina:** o brilho do Claude é sempre o laranja do Claude; ele não fica mais amarelo nem vermelho conforme o limite de 5 horas enche (o limite já tem seus avisos)

## 4.2.2

- **Atualizações:** quando a verificação automática (a cada hora enquanto o app roda, a primeira 30 segundos depois de abrir) encontra uma versão nova, a ilha agora avisa "Atualização X disponível · baixando" antes de baixar e reiniciar nela

## 4.2.1

- **Ajustes:** a versão e o link do projeto ficam no rodapé de todas as páginas; Essenciais termina com uma linha dividida em duas, "Mais ajustes" e o botão de atualização (então "Procurar atualizações" no menu da bandeja mostra o resultado ali); "Relatar problema" é um botão só que copia o relatório e abre a página de issues; o idioma é uma fileira de bandeiras (globo para automático) na mesma linha do nome

## 4.2.0

- **Pomodoro:** um terceiro botão na página do timer inicia ciclos de foco e pausa (25, 5 e uma pausa longa de 15 a cada quatro, ajustáveis em Ajustes, Comportamento, Pomodoro), com um aviso a cada mudança; o botão "+" pula para a próxima fase. A pausa é verde e o foco é laranja (também na linha fina)
- **Transições mais fluidas:** o conteúdo de uma visão nova espera a forma andar um terço do caminho, então a linha fina cresce até virar pílula antes de o conteúdo aparecer; o conteúdo antigo sobe ao sair e o novo vem de baixo; os avisos chegam com um pouco mais de mola
- **Volume e brilho:** o brilho é um sol que acende (os raios crescem com o nível) e o alto-falante do volume ganha ondas, com um xis quando está mudo
- **Novidades no app:** depois de uma atualização, um cartão no topo dos Ajustes lista as principais mudanças (com link para o histórico no seu idioma) até você dispensar, e o aviso de atualização na ilha avisa; em Ajustes, Sistema, há um link para o histórico de mudanças

## 4.1.0

- **Problemas:** o app guarda um registro das próprias falhas em `crash.log` (na pasta de configurações, só neste PC, sem enviar para lugar nenhum) e, depois de uma falha, reabre sozinho (no máximo 3 vezes em 5 minutos) com um aviso. Em Ajustes, Sistema, há **Copiar relatório de um problema** (versão, sistema, os ajustes diferentes do padrão e as últimas falhas, sem nomes nem chaves) e **Relatar um problema no GitHub**, que abre a página de issues para colar o relatório
- **Testes automáticos no GitHub:** um fluxo compila o app e roda os testes a cada envio e a cada pull request

## 4.0.6

- **Pedidos de permissão na ilha:** um comando longo não é mais "longo demais para aprovar": ele aparece inteiro (até 1.800 caracteres) e rola dentro da ilha, e o Permitir continua disponível (só some quando os argumentos não dá para ler como comando, arquivo ou endereço)
- **Abrir o Claude** agora vai direto ao chat que precisa de você (usa o link `claude://` do próprio app), em vez de só trazer o app para a frente
- **Um pedido respondido no próprio Claude** agora sai da ilha na hora (antes ficava um ou dois minutos); a ilha percebe que apareceu um resultado de ferramenta no chat

## 4.0.5

- **O Claude te pergunta algo que a ilha não percebeu:** além de mostrar o aviso do app do Claude, a linha fina agora pulsa em laranja (como em "Claude espera por você") até você olhar para a ilha, por no máximo três minutos, ou até o Claude terminar

## 4.0.4

- **O Claude te pergunta algo que a ilha não percebeu:** o aviso do Windows do próprio app do Claude era sempre escondido porque a ilha esperava anunciá-lo ela mesma, então uma pergunta que ela não conseguia ler passava em silêncio (só a bolinha branca aparecia). Agora, se a ilha não anunciou o Claude pouco depois desse aviso, o aviso é mostrado

## 4.0.3

- **Linha fina:** as bolinhas (gravação, microfone, câmera, compartilhamento de tela e a notificação não lida) agora ficam sempre na ponta esquerda da linha, em vez de no meio
- **README:** os recursos começam com seis destaques e a lista completa fica dobrada logo abaixo; a imagem dos Ajustes mostra as abas como colunas lado a lado

## 4.0.2

- **Ajustes, Essenciais:** sem o tema (ele está em Avançado, Aparência), sem a descrição longa sob as notificações do Windows e com uma descrição mais curta sob "Linha fina sempre"

## 4.0.1

- **Ajustes:** a linha com os botões do Chrome e do Edge agora diz o que é: "Extensão do navegador", uma extensão para instalar no Chrome ou no Edge (ícones dos sites para o áudio da web e o progresso dos downloads)

## 4.0.0

- **Boas-vindas:** na primeira execução, e uma vez depois de atualizar para a 4.0, uma tela curta de boas-vindas pergunta como a ilha deve ser (uma linha fina quando parada, sempre uma linha fina, ou uma pílula) e o que deve mostrar (música, Claude, notificações do Windows, bolinhas de microfone e câmera, responder permissões do Claude, iniciar com o Windows)
- **Ajustes recomeçados:** os ajustes das versões anteriores voltam aos novos padrões, uma vez. Mantidos: idioma, Discord, a API local, o hook de permissões e os apps escondidos das bolinhas de privacidade
- **Essenciais e Avançado:** os Ajustes abrem em Essenciais (alguns botões e "Mais ajustes"); Avançado é cada ajuste, em quatro abas, com a busca
- **Novos padrões:** iniciar com o Windows ligado, notificações do Windows ligadas, a ilha parada é uma linha fina (também com uma janela maximizada), ela nunca se esconde sozinha e "Linha fina sempre" fica desligada
- **Busca:** digitar nunca trava (a página muda quando você para), a página é montada uma vez por busca, e a janela para numa altura confortável e fica na borda da barra de tarefas
- **Botões azuis** em vez de verdes

## 3.0.12

- **Corrigido um travamento:** digitar na caixa de busca dos Ajustes fechava o app. Agora todas as linhas são montadas como de costume e as que não combinam são apenas escondidas
- **Ajustes reorganizados:** a linha fina, o monitor do sistema e os tamanhos agora estão em Aparência (o grupo da linha fina abre primeiro); Comportamento fica só com como a ilha se comporta (passar o mouse, tela cheia, atalho, tempos, páginas). Com "Linha fina sempre" ligada, as opções que só importam sem ela ficam escondidas

## 3.0.11

- **A gravação de tela fica à vista:** uma bolinha vermelha para uma gravação (OBS ou Game Bar) fica junto das bolinhas de microfone e câmera (dentro da linha, na linha fina), então ela não some mais quando música e Claude ocupam os dois lugares da ilha

## 3.0.10

- **Gravação de tela (OBS e Game Bar):** o ícone na ilha normal é vermelho, não azul; na linha fina é uma linha vermelha fraca com uma bolinha piscando, e não mais o pulso vermelho que significa erro ou bateria baixa

## 3.0.9

- **Downloads da Steam, em tempo real:** a Steam só grava o progresso de um download nos seus arquivos de vez em quando (uma instalação nova ficava em 0% até ser pausada e retomada). Agora a ilha conta os bytes que a Steam grava no disco enquanto trabalha, partindo do que os arquivos dela diziam quando a execução começou, então a porcentagem anda enquanto o download roda. Quando a ilha abre no meio de um download, ela mostra reticências até a execução ser pausada e retomada, em vez de um 0% errado
- **A linha fina ficou maior:** mais larga e mais alta (108 x 9), com bolinhas maiores e um equalizador maior
- **README, imagens e animação:** os quatro READMEs descrevem cada estado da linha fina, e todas as imagens e a animação foram redesenhadas com eles

## 3.0.8

- **Gravação de tela:** a gravação da Game Bar do Windows (Win+Alt+R) agora aparece na ilha, com o tempo, como a do OBS (uma linha vermelha pulsando na linha fina). Ela é percebida pelo vídeo que a Game Bar grava em Videos\Captures enquanto grava. O botão agora se chama "Gravação de tela" (OBS e Game Bar)

## 3.0.7

- **Ajustes:** a caixa de busca agora parece uma: uma lupa, as palavras "Buscar ajustes", um contorno fino e um x para limpar

## 3.0.6

- **Linha fina, monitor do sistema:** os dois anéis agora leem números novos a cada segundo (antes só atualizavam quando a ilha era aberta); a parte que não está preenchida é preta; com "manter o monitor à vista" os anéis ficam ao lado da linha enquanto outra atividade roda, e a bolha dessa atividade vai para além deles
- **Linha fina, privacidade:** as bolinhas de microfone, câmera e tela ficam dentro da linha
- **Linha fina, mais estados quando nada mais acontece:** sem internet (uma linha vermelha tracejada que pisca), bateria baixa (a linha pulsa em vermelho) e uma bolinha branca para uma notificação que ninguém viu ainda (some quando você passa o mouse na ilha, ou depois de 10 minutos)
- **Linha fina, volume e brilho:** uma barrinha dentro da linha em vez da barra grande

## 3.0.5

- **Linha fina, monitor do sistema:** com o monitor do sistema ligado, a linha fina mostra processador e memória como dois anéis pequenos ao lado dela (azul e verde, âmbar a partir de 70%, vermelho a partir de 90%) enquanto nada mais acontece
- **Desfeito:** o contorno laranja fixo para o Claude trabalhando ao lado de outra atividade (3.0.4). O Claude trabalhando ao lado da música volta a ser uma bolhinha

## 3.0.3

- **Linha fina:** a segunda atividade (um download da Steam enquanto o Claude trabalha, música enquanto um timer roda, e assim por diante) aparece como uma bolhinha ao lado da linha, com seu próprio ícone, capa ou anel de progresso, para que nada importante fique escondido

## 3.0.2

- **Linha fina:** o brilho enquanto o Claude trabalha é mais largo, se repete ao longo da linha e se move mais rápido, então a maior parte da linha fica acesa; o equalizador da música assume a cor da capa; as bolinhas de privacidade ficam menores ao lado da linha fina e voltam ao lugar na hora em que o nome ou a bolinha some
- **Aviso de música:** o símbolo (pausar, tocar, próxima, anterior) fica na ponta direita, separado da capa e do nome; "anterior" agora é identificado sempre certo: os botões da ilha e as teclas de mídia do teclado dizem para que lado a música foi (só se olha se essas duas teclas estão pressionadas, 8 vezes por segundo; nada é guardado)

## 3.0.1

- **A linha fina é o visual padrão** ("Linha fina sempre" nos Ajustes, Comportamento): a ilha recolhida é uma linha fina, e passar o mouse a abre
- **A linha mostra o que acontece na própria linha:** um brilho percorre a linha enquanto o Claude trabalha (amarelo ou vermelho conforme o limite enche), ela pulsa quando o Claude espera por você, um equalizador pequeno toca para música, um timer, um download ou a carga a preenchem, um alerta (ou gravação do OBS) a faz pulsar em vermelho e "concluído" a deixa verde. A lua do foco continua sendo uma luazinha
- **Bolinhas de privacidade:** elas seguem a ilha bem mais rápido, sem balançar, quando ela cresce ou encolhe

## 3.0.0

- **A linha fina amadurece:** com uma janela maximizada (e "Atividades viram a linha também"), a linha mostra um ícone pequeno do que mais importa, na sua própria cor: música (nota rosa), Claude (faísca cor de barro, amarela a partir de 75% do limite e vermelha a partir de 90%, respirando enquanto trabalha, pulsando enquanto espera por você), timer (relógio, a linha esvazia conforme o tempo acaba), a atividade de um app (seu ícone, a linha enche com o progresso), carregando (verde, a linha enche com a carga) e foco (uma lua crescente). Só o estado mais importante aparece, nunca uma mistura
- **Música na linha:** pausar, tocar, próxima e anterior fazem a linha crescer por um momento com a capa, um símbolo e o nome da música. Opcional: a linha enche ao longo da música (Ajustes, Comportamento)
- **Novas atividades ao vivo:** gravação ou transmissão do OBS com o tempo (lido do próprio log do OBS); Windows instalando atualizações e "Reinicie para concluir"; downloads do navegador com a porcentagem (a extensão do navegador agora tem a permissão `downloads`: recarregue-a); e o placar ao vivo do seu time (desligado por padrão: digite seu time e os códigos de liga da ESPN, como `bra.1`)
- **Barras de progresso:** mais finas, sob o nome da atividade principal
- **Ajustes:** uma caixa de busca; o que está dentro de um grupo fica separado (mais escuro, recuado, borda azul); "Apps escondidos" substitui uma linha por app por chips (+ para esconder, x para trazer de volta); jogos, OBS e similares ficam em um grupo só

## 2.10.0

- **`island run`:** `island run --title Tests -- npm test` executa o comando e o mostra na ilha até terminar (concluído, ou erro com o código de saída, que o `island run` devolve como seu)
- **Ajustes:** com uma janela maximizada e a linha fina, "Atividades viram a linha também" faz música, Claude e as outras virarem uma linha fina também (passe o mouse para vê-las). Desligado por padrão

## 2.9.1

- **Ajustes:** descrições mais curtas

## 2.9.0

- **Downloads da Steam, corrigidos:** um download pausado ou na fila não aparece mais como rodando (o próprio log da Steam diz o que roda e o que está pausado), um segundo jogo sendo baixado agora toma o lugar, e a porcentagem continua andando entre os momentos em que a Steam grava seus arquivos em vez de esperar os próximos 5%
- **Barra de progresso:** a atividade principal mostra uma barra de progresso fina sob o nome
- **Segunda atividade:** um ícone pequeno dentro de um anel que enche com o progresso; quando termina, um disco verde com um visto aparece e depois some

## 2.8.2

- **Downloads da Steam** agora vem ligado por padrão (ele só lê os arquivos de manifesto locais da Steam; desligue em Ajustes, Atividades)

## 2.8.1

- **Alertas de limite:** "renovado" agora é anunciado também quando o limite não tinha chegado a 75% (qualquer queda de 10 pontos ou mais de volta para abaixo de 75%)

## 2.8.0

- **Downloads da Steam (desligado por padrão):** o progresso de um jogo sendo baixado ou atualizado na Steam aparece na ilha como uma atividade com barra de progresso, e "Concluído" quando termina. Ele é lido dos pequenos arquivos de manifesto que a Steam guarda junto dos jogos (`appmanifest_*.acf`), em cada pasta de biblioteca da Steam; nada é enviado para lugar nenhum. Ajustes, Atividades, "Downloads da Steam". Ainda não testado com um download em andamento: por favor, conte como ele se comporta
- As atividades que a própria ilha publica (como esta) não precisam mais que a API local esteja ligada

## 2.7.1

- Silenciar ou reativar o som no Discord traz a ilha de volta por um momento, mesmo quando ela estava guardada ou escondida automaticamente, para que a mudança seja vista; o ícone riscado fica enquanto você estiver silenciado


## 2.7.0

- **Discord silenciado ou ensurdecido (experimental, desligado por padrão):** um microfone riscado ou fones riscados ao lado da ilha enquanto você está silenciado ou ensurdecido no Discord, não importa como você fez isso. Usa o RPC local do Discord, que precisa de um aplicativo Discord seu e de uma aprovação única dentro do Discord; o passo a passo está em docs/DISCORD.md e há um link em Ajustes, Atividades, Discord. O client secret e os tokens são guardados criptografados para o seu usuário do Windows, e a ilha só fala com o pipe se o próprio Discord o oferecer. Testado contra o Discord de verdade quanto à conexão e à recusa de um id de aplicativo errado; a aprovação e o estado de silenciado não puderam ser testados sem um aplicativo Discord, então por favor conte o que você vê
- **Revisão de segurança do recurso do Discord:** o pipe só é confiável quando o servidor dele roda na sua sessão e é um executável dentro da sua própria pasta do Discord, e ele não pode agir como você; uma recusa é final até você mudar o ID ou o secret (o Discord não é chamado de novo e de novo para mostrar a janela de aprovação) e as falhas esperam cada vez mais, até cinco minutos; um token salvo fica amarrado ao seu id de aplicativo; um token de renovação rejeitado é descartado; um secret digitado nesse meio-tempo nunca é sobrescrito; há um botão **Esquecer os dados do Discord**
- **Jogos e vídeos que ocupam a tela toda** agora são tratados como tela cheia, não como janela maximizada, então a ilha não encolhe mais para uma linha fina sobre eles (ela se esconde, conforme "Esconder em tela cheia")
- **Novas opções em Ajustes, Comportamento:** como a ilha fica quando nada acontece (pílula pequena ou linha fina) e o que ela faz com uma janela maximizada na frente (linha fina, normal ou escondida)
- **O monitor do sistema fica à vista:** com o monitor ligado, música, Claude e outras atividades viram a bolha pequena ao lado dele em vez de cobri-lo (Ajustes, Aparência, "Manter o monitor à vista", ligado por padrão)
- Testes unitários para o formato das mensagens do Discord

## 2.6.1

- **Ignorar as bolinhas de um app:** os apps vistos usando o microfone, a câmera ou a tela aparecem listados em Ajustes, Atividades, Privacidade, e é possível desligar um (um app de chamada como o Discord mantém o microfone ocupado por horas)
- A opção de compartilhamento de tela agora diz que só os apps que avisam o Windows da sua captura são vistos (o Discord avisa; muitos navegadores e apps web não)


## 2.6.0

- **Bolinha de compartilhamento de tela:** uma bolinha azul ao lado da ilha enquanto um app compartilha ou grava a tela (lida do mesmo registro do Windows das bolinhas de microfone e câmera). Ajustes, Atividades, Privacidade, "Compartilhamento de tela"
- **Pedido de permissão, após uma revisão de segurança:** os botões acordam depois de cerca de meio segundo e um clique precisa ser pressionado e solto no mesmo botão; o resumo é só o texto técnico (comando, arquivo, endereço, consulta) com quebras de linha mostradas como ⏎ e a pasta do projeto; quando ele é cortado ou não tem esse campo, Permitir é trocado por "Abrir o Claude"; um pedido que não pode ser mostrado (janela em tela cheia, ilha fechando) fica sem resposta em vez de segurar o Claude por um minuto; o arquivo de ajustes do Claude não é reescrito quando o hook já está certo, e toda mudança guarda uma cópia do arquivo antes
- **Ajustes:** um tema claro (Auto segue o Windows, ou escolha Claro ou Escuro; a ilha continua preta), a ordem das páginas, quais alertas de limite mostrar (75, 90, 100 %, renovado) e durações separadas para notificações do Windows e alertas de limite
- **Testes:** `dotnet test tests` roda testes unitários do leitor de transcrição do chat, das regras dos alertas de limite e do editor dos ajustes do Claude
- Menores: os ícones dos sites são suavizados uma vez em vez de a cada mudança de título, e copiar a pasta da extensão não trava mais os Ajustes

## 2.5.1

- Pedido de permissão: Negar é vermelho com um x e Permitir é verde com um visto, e as ferramentas do conector do Supabase mostram a marca do Supabase


## 2.5.0

- **Responder permissões pela ilha (experimental, desligado por padrão):** Ajustes, Atividades, "Responder permissões pela ilha" adiciona um hook `PermissionRequest` aos ajustes de usuário do Claude Code. Quando o Claude Code está prestes a pedir uma permissão (um comando, um arquivo, uma consulta ou migration de banco de dados de uma ferramenta conectada), a ilha mostra a ferramenta e o que ela quer fazer, com Permitir e Negar. Só um clique decide; se você não clicar em 60 segundos, a ilha for fechada ou qualquer coisa falhar, o Claude pergunta como de costume. Detalhes e limites em SECURITY.md
- A página do GitHub agora tem descrição e tópicos

## 2.4.4

- O app agora declara no manifesto que é feito para Windows 10 e 11, o que deve acabar com o aviso do "Assistente de Compatibilidade de Programas" que podia aparecer depois de uma atualização


## 2.4.3

- **Perguntas do Claude Desktop:** um chat que termina com uma pergunta (como "Publicar?") agora é reconhecido mesmo quando não está marcado como bloqueante, e por isso o último aparecia como parado. O aviso e a página do Claude mostram a pergunta (resumida) e um link **Abrir o Claude** traz o app desktop para a frente para responder. A ilha não consegue clicar a resposta por você


## 2.4.2

- O botão de desinstalar nos Ajustes agora é a última linha e todo em vermelho, para marcá-lo como perigoso


## 2.4.1

- **Desinstalar:** um novo botão "Desinstalar o Windows Island" no fim dos Ajustes, Sistema (só na cópia instalada), porque a entrada na lista de apps do Windows nem sempre aparece. A entrada agora é atualizada a cada início (a versão dela nunca era atualizada depois da primeira instalação) e traz tamanho, data e link; a versão do produto do exe não tem mais um hash de build


## 2.4.0

- **Alertas de limite:** um aviso quando o limite de 5 horas ou semanal do Claude chega a 75%, 90% e 100% e quando ele renova (depois de ter chegado a 75% ou mais). O aviso de 100% diz "Limite atingido" com a janela embaixo. Ligado por padrão; desligue em Ajustes, Atividades, Alertas de limite. A primeira leitura quando a ilha inicia nunca é anunciada
- **Novos padrões** (para instalações novas; um arquivo de ajustes existente mantém seus valores): duração dos alertas 3 s, esconder sozinha quando parada após 5 s, música pausada escondida na hora


## 2.3.5

- **Perguntas do Claude Desktop:** um chat que termina sua vez com uma pergunta para você (a bolinha amarela no Claude Desktop, como "Publicar?") agora aparece como esperando por você na ilha, e o aviso diz que o Claude precisa de você em vez de só "terminou"
- Imagens do README: as imagens de notificação e bateria voltaram a estar corretas, e as novas mostram o Claude pedindo por você, vários chats trabalhando e um de dois esperando


## 2.3.4

- O rodapé dos Ajustes mostra a marca do GitHub e `@leotrvs/windows-island` (um link para o projeto)
- **Revisão de segurança:** o download de ícones de sites recusa endereços neste computador ou na rede local e tem um limite de 4 segundos e 256 KB para a transferência toda; os ícones das notificações de sites só são lidos das próprias pastas do Chrome e do Edge, nunca por links, e só se forem PNG, JPEG ou ICO; o `config.json` da extensão só pode ser lido pelo seu usuário do Windows; o botão da extensão digita o endereço de uma vez só; o token da API fica fora do histórico da área de transferência do Win+V; o popup de volume do Windows é devolvido no próximo início mesmo depois de uma falha
- **Desempenho:** o volume é lido a cada 250 ms e só a cada 40 ms por dois segundos depois de uma mudança; a transcrição de um chat que não é encontrado não é procurada a cada segundo
- Imagens e GIFs do README regenerados, READMEs e notas de segurança atualizados para os novos recursos


## 2.3.3

- Rodapé dos Ajustes: a versão (Windows Island v2.3.3) à esquerda e um link para o projeto no GitHub à direita


## 2.3.2

- Apoiar o mouse sobre a pequena segunda atividade ao lado da ilha (por exemplo a faísca do Claude enquanto toca música) abre a ilha direto na página dessa atividade


## 2.3.1

- Ilha compacta com vários chats: quando alguns estão esperando, ela mostra quantos deles (1/2 · Esperando) em vez do total, que parecia dizer que todos estavam esperando


## 2.3.0

- **Permissões do Claude Desktop:** a ilha agora mostra "esperando por você" quando um chat no Claude Desktop está pedindo uma permissão ou fazendo uma pergunta (o Desktop informa esses chats como ocupados). Ela lê só nomes de ferramentas e horários da transcrição do chat
- **Vários chats ao mesmo tempo:** com mais de um chat trabalhando, a ilha compacta mostra uma contagem antes do tempo (por exemplo 2 · 0:58) e o painel do Claude lista até três chats, cada um com seu estado e tempo


## 2.2.3

- A janela dos Ajustes mantém no lugar a borda mais próxima da barra de tarefas quando cresce ou encolhe: perto do fim da tela ela fica ancorada ali (e desce de volta quando um grupo é dobrado); na metade de cima ela mantém o topo


## 2.2.2

- A janela dos Ajustes agora também encolhe quando um grupo é dobrado ou uma aba mais curta é aberta (antes só crescia)


## 2.2.1

- **A janela dos Ajustes** se ajusta ao conteúdo: abre, troca de aba e desdobra grupos na altura de que precisa, então nada precisa ser rolado (até onde a tela permitir); ela cresce para cima quando chega na borda de baixo
- Os cartões empilhados em uma aba (Comportamento e Páginas) agora têm um espaço claro entre eles


## 2.2.0

- **Ícone da bandeja:** um segundo clique esquerdo agora fecha a janela dos Ajustes
- **Menu da bandeja:** redesenhado para combinar com a janela dos Ajustes (escuro, arredondado, com entrada e saída suaves); as opções do Timer se abrem sob a sua linha; Esc ou um clique em outro lugar o fecha


## 2.1.9

- **YouTube e outros sites de vídeo:** a ilha não fica mais presa no logo do navegador. A prévia do vídeo é lida de novo quando chega tarde, e uma prévia já mostrada é mantida em vez de voltar para o logo


## 2.1.8

- **Barra de volume:** lê o volume a cada 40 ms (era 250 ms) e desliza até cada nível, então segurar uma tecla se move suave em vez de em saltos
- **Esconder o popup de volume do Windows:** a janela dele é deixada totalmente transparente com antecedência, então o instante antes de ser escondida não pisca mais; ela é restaurada quando a opção é desligada ou a ilha fecha


## 2.1.7

- Áudio de sites: a ilha agora usa o ícone de toque do próprio site (apple-touch-icon.png) quando ele tem um, então o WhatsApp mostra o logo dele em vez de um ícone de aba borrado com o contador de não lidas. Baixado uma vez por site, só HTTPS (veja SECURITY.md)


## 2.1.6

- **Ajustes mais organizados:** opções relacionadas se dobram sob setas (Privacidade, Bateria, Alertas do sistema, Volume e brilho, Tempos, Tamanho e posição; e o token da API, o guia e a extensão do navegador). O estilo do monitor do sistema só aparece enquanto o monitor está ligado


## 2.1.5

- Os ícones das notificações (apps e sites) não ficam mais sobre um bloco cinza: aparecem direto na ilha com cantos arredondados, então ícones transparentes se misturam


## 2.1.4

- Os ícones pequenos de sites na tela de música são ampliados com interpolação suave, então ficam macios em vez de quadriculados


## 2.1.3

- **Áudio de sites:** o equalizador assume a cor do ícone do site (o WhatsApp é verde, não roxo); os ícones são desenhados mais suaves e a extensão envia um maior (recarregue a extensão para recebê-lo)
- **Notificações de sites** do Chrome/Edge agora mostram o ícone e o nome do site em vez do logo do navegador
- **As bolinhas de microfone/câmera** não se escondem nem deslizam mais quando a ilha é guardada ou escondida automaticamente; elas ficam no lugar


## 2.1.2

- **Botão da extensão do navegador:** abre a página de extensões sozinho (os navegadores ignoram endereços chrome:// vindos da linha de comando e mostram uma aba em branco, então o endereço é digitado nessa aba, só quando uma nova aba em branco está na frente)
- Copiar o caminho da pasta é tentado de novo e conferido; se ainda falhar, a pasta abre no Explorer em vez de dizer que foi copiada. Os passos também citam "Modo de programador" e "Carregar descompactada" para navegadores em português (Portugal)

## 2.1.1

- **Guardar:** a ilha agora volta depois de 2,5 segundos, e também quando o mouse fica parado na borda de cima da tela no meio (onde ela fica), para não precisar estar no ponto exato dela

## 2.1.0

- **Monitor do sistema:** uma opção em Ajustes, Aparência, mantém a carga do processador e da memória na ilha quando nada mais está acontecendo. Anéis (o padrão), barras ou números; desligado por padrão
- **Guardar:** arraste a ilha para cima (para baixo quando ela fica embaixo) para guardá-la e usar o que está atrás dela, como abas do navegador. Ela volta com uma nova atividade, um alerta, um microfone ou câmera em uso, ou quando o mouse fica parado no lugar dela por alguns segundos

## 2.0.1

- **Janela dos Ajustes:** lembra o tamanho e o lugar, e não pula mais quando abre
- **Botão da extensão do navegador:** abre a página de extensões no perfil que você usou por último (chega da página "quem está usando o Chrome?"), com passos curtos numerados
- **Ajustes da API local:** agora dizem para que serve o token (só para os seus próprios scripts; o comando island e a extensão não precisam de nenhum) e linkam para o guia
- **Nome do app nas bolinhas:** aparece quando você passa o mouse nas próprias bolinhas, não quando abre a ilha
- **Esconder o popup de volume do Windows (experimental):** agora reage no instante em que o popup aparece, então não pisca mais

## 2.0.0

- **Nomes de apps nas bolinhas:** passe o mouse na ilha para ver qual app usa o microfone ou a câmera, ao lado da bolinha. Os Ajustes também podem mostrar o nome sozinho por 1, 2 ou 3 segundos quando um app começa (desligado por padrão)
- **Bateria como atividade:** enquanto o carregador está ligado e a bateria não está cheia, a ilha mostra o nível de carga como qualquer outra atividade
- **Escolha suas páginas:** Ajustes, Comportamento, permite ligar ou desligar cada página da ilha aberta
- **Ícones de apps nas notificações:** as notificações do Windows mostram o ícone real do app que as enviou (carregado do shell, só para ids de app simples)
- **Volume e brilho:** uma barrinha enquanto você muda o volume ou o brilho da tela do notebook
- **Esconder o popup de volume do Windows (experimental, desligado por padrão):** esconde o popup de volume e brilho do próprio Windows para que só a barra da ilha apareça. Depende de uma janela não documentada do shell, então uma atualização do Windows pode fazê-lo parar de funcionar
- **Dispositivos e rede:** um aviso curto quando um dispositivo Bluetooth ou USB conecta ou desconecta, e quando a internet cai ou volta
- **Não perturbe:** uma luazinha na ilha enquanto o Não perturbe ou uma sessão de foco está ligada
- **Relógio e página do sistema:** a hora e a data, com a carga do processador e da memória
- **Atalho de teclado:** abre e fecha a ilha de qualquer lugar. Desligado por padrão e configurável (Ctrl+Alt+I no começo); usa a própria chamada de atalho do Windows, então nenhum hook de teclado é instalado
- **Extensão do navegador:** a pasta `extension/` tem uma pequena extensão para Chrome e Edge que avisa a ilha qual aba está tocando, então o áudio da web mostra o ícone do site em vez de uma letra. Os Ajustes têm um botão de instalação que a desempacota com seu token e abre a página de extensões do navegador
- **API local:** scripts e apps podem mostrar suas próprias atividades ao vivo e alertas, e uma extensão do navegador pode dizer qual aba está tocando. Desligada por padrão; escuta só em 127.0.0.1 e precisa de um token secreto. O comando `island.cmd` junto do app faz o mesmo a partir de um terminal. Veja [docs/API.md](docs/API.md)

## 1.13.0

- **Nítida em todos os monitores:** a ilha agora segue a escala de exibição própria de cada monitor (DPI por monitor), então não fica mais esticada e borrada em uma tela com escala diferente da principal
- `--dev` não precisa mais de `--portable`

## 1.12.0

- **Alerta de captura de tela:** um aviso bem pequeno, "Captura de tela feita", quando você tira uma captura com Print Screen ou Win+Shift+S. Ele não enfileira nem se repete para uma rajada de capturas, e pode ser desligado em Ajustes, Atividades. Ele só percebe que uma imagem chegou à área de transferência vinda das ferramentas de captura do próprio Windows: nunca lê a imagem e não vigia o teclado
- **Abas e opções deslizam:** toda barra de abas e escolha nos Ajustes agora tem uma pílula que desliza até a que você escolhe
- A escolha de idioma agora é só **Auto**, English, Português, Español e Русский
- Texto mais nítido nos Ajustes (ClearType em vez de suavização em tons de cinza)
- Corrigido o ícone de atualização girando em torno de um ponto fora dele
- As linhas do Claude e do Codex nos Ajustes usam suas próprias marcas
- `--dev` roda uma build ao lado da cópia instalada, para testes

## 1.11.0

- **Verificar atualizações** agora mostra o que está fazendo: um ícone girando e "Verificando atualizações…", depois um ícone de download e "Baixar atualização 1.x" quando uma é encontrada. Nada é baixado até você clicar. As atualizações automáticas continuam se instalando sozinhas
- **Idiomas:** inglês, português, espanhol e russo, escolhidos automaticamente a partir do Windows. Mude em Ajustes, Sistema, Idioma
- **Inter** agora é a fonte, incluída no app (SIL Open Font License)
- **Alertas menores:** notificações e alertas ficam cerca de 20% menores, com uma escolha Pequeno ou Normal em Ajustes, Aparência
- **Alertas de bateria** têm seu próprio ícone (carregando, baixa, cheia). As prévias estão no README
- **Ícones** em todas as linhas da janela dos Ajustes, e uma barra de rolagem bem fina
- **Padrões:** tamanho recolhido 90%, tamanho expandido 95%, distância da borda 5 px (instalações novas; suas próprias escolhas são mantidas)
- `--preview=Settings` abre a janela dos Ajustes com dados de exemplo

## 1.10.1

- A janela dos Ajustes agora se comporta como um flyout da bandeja: ela pega o foco (o que fecha o painel de ícones escondidos), desliza para cima até o canto junto da barra de tarefas, fica acima das outras janelas até você clicar em outro lugar, e Esc a fecha
- GIF animado no topo do README, gravado com dados de exemplo (`scripts/render-gif.ps1`)

## 1.10.0

- **Ícone do Codex:** o logo do Codex substitui o antigo símbolo de prompt na ilha
- **Cor do anel de uso:** azul por padrão, como no próprio Claude e Codex, com Verde e Roxo nos Ajustes, em Aparência. Os anéis continuam ficando âmbar e vermelhos conforme o limite se aproxima
- **Janela dos Ajustes:** um cabeçalho preto com botão de fechar em vez da barra de título cinza do Windows (sem minimizar nem maximizar), e ela abre mais alto, longe do flyout de ícones escondidos
- A tela de música recolhida é mais estreita
- Nova imagem no README mostrando os estados recolhido e expandido, e um botão azul de Download que se destaca no tema escuro do GitHub

## 1.9.0

- **Páginas na ilha:** quando ela está expandida, gire a roda do mouse sobre ela ou clique nas bolinhas para ir entre música, Claude (com Codex) e o timer. Você vê os limites e as sessões do Claude sem pausar a música
- **Página do timer:** escolha uma duração com - e +, comece uma contagem regressiva ou um cronômetro, pause, retome e pare, tudo dentro da ilha
- A tela de música expandida é mais curta, com menos espaço vazio
- As telas compactas do Claude e do Codex ficaram um pouco mais estreitas

## 1.8.0

- **Ícone do app** para o exe, os atalhos e a janela dos Ajustes, em vez do padrão do Windows
- **Janela dos Ajustes:** abre no canto junto da barra de tarefas (e pode ser movida), com abas no topo (Atividades, Comportamento, Aparência, Sistema) em vez de uma página longa. Barra de rolagem mais fina e botões menores
- **Bolinhas de microfone:** menores e mais suaves, e o microfone agora também é detectado pelas sessões de áudio do Windows, o que pega apps que o método anterior perdia, como o ditado em alguns apps de desktop

## 1.7.3

- Corrigida a ilha mostrando o Claude brevemente como trabalhando, com um timer, toda vez que os limites eram lidos. O Claude Code de curta duração iniciado para a leitura do `/usage` não conta mais como um chat

## 1.7.2

- A seção do Codex no painel do Claude agora só aparece enquanto o Codex está trabalhando, em vez de sempre que os arquivos dele existem no PC

## 1.7.1

- Corrigido o painel expandido do Claude cortando a seção do Codex embaixo

## 1.7.0

- **Timer e cronômetro** pelo menu da bandeja (**Timer**): uma atividade ao vivo na ilha, com um aviso quando a contagem regressiva termina
- **Bolinhas de microfone e câmera:** uma bolinha laranja ou verde ao lado da ilha enquanto um app os usa. Desligue com **Bolinhas de microfone e câmera** nos Ajustes
- **Alertas de bateria** em notebooks: carregador conectado ou removido, bateria baixa (20% e 10%) e totalmente carregada

## 1.6.0

- **App mais leve:** o ícone da bandeja e a lista de monitores agora usam o Windows diretamente em vez do Windows Forms. O download fica cerca de 10 MB menor (78 MB para 68 MB) e o comportamento é o mesmo
- A memória fica em torno de 40 MB, também com uma ilha em cada um de três monitores

## 1.5.0

- **O Codex** entra na ilha: uma atividade ao vivo com timer enquanto o Codex trabalha, seus limites de 5 horas e semanal sob os do Claude no painel expandido, e um aviso **Codex terminou**. Lido dos arquivos de sessão que o Codex guarda, sem tocar no login dele
- **Mostrar o Codex** e **Alertas do Codex** nos Ajustes

## 1.4.1

- Corrigido o modo de monitor **Com o mouse**: depois de mudar para outra tela a ilha piscava e depois ficava invisível em todas as telas
- Chegar a uma nova tela conta como atividade, então o esconder automático espera o tempo todo lá

## 1.4.0

- **Monitor** nos Ajustes: mostre a ilha no monitor principal, em um específico, em todos ao mesmo tempo, ou naquele com o mouse, onde ela pula conforme o cursor se move
- Janelas em tela cheia e maximizadas agora são detectadas no monitor de cada ilha

## 1.3.0

- **Janela dos Ajustes** no estilo dos Ajustes do iOS, com todas as opções. Clique no ícone da bandeja, escolha **Ajustes…**, ou abra o atalho enquanto a ilha roda
- **Tamanho:** tamanhos separados para a ilha recolhida e a expandida, de 80% a 130%
- **Posição:** distância da borda e posição horizontal
- O menu da bandeja fica só com as ações rápidas

## 1.2.0

- **Esconder sozinha quando parada:** a ilha se esconde depois de ficar de 5 segundos a 5 minutos sem mostrar nada, e volta com uma nova atividade ou quando o mouse chega ao lugar dela. Desligado por padrão
- **Duração dos alertas:** escolha por quanto tempo os alertas do Claude, as notificações e as mensagens de apps ficam na tela
- **Esconder a música pausada depois de:** escolha por quanto tempo uma música pausada fica, de na hora até um minuto
- Valores digitados em `settings.json` são mantidos dentro de limites razoáveis

## 1.1.0

- **Ilha vazia** no menu da bandeja: a ilha recolhida fica uma pílula simples e não cresce mais enquanto toca música ou o Claude trabalha. Passe o mouse ou clique nela para ver as atividades. Alertas e notificações continuam seguindo suas próprias opções

## 1.0.2

- O texto das notificações é lido de forma mais defensiva: definições XML (DTDs) são recusadas, e notificações e textos grandes demais são cortados, já que qualquer app ou site pode enviar uma notificação

## 1.0.1

- As atualizações são assinadas com a mesma chave do Claude Usage Taskbar. Cada arquivo de atualização agora nomeia seu app, e a ilha rejeita qualquer arquivo feito para outro app
- Cópias da 1.0.0 não conseguem atualizar para esta versão sozinhas: baixe-a uma vez a partir desta release

## 1.0.0

Primeira release.

- Atualizações automáticas assinadas a partir das releases do GitHub: a ilha verifica a cada hora, confere a assinatura e o hash do arquivo, e reinicia na nova versão quando você não está usando. **Atualizações automáticas** e **Verificar atualizações** estão no menu da bandeja
- Uma música pausada sai da ilha depois de 5 segundos em vez de 30
- Tudo o que havia nas prévias: música, atividade e limites do Claude Code, alertas, notificações do Windows, o instalador e animações com mola

## 0.2.1

- A música segue o player que está realmente tocando. Antes, um player pausado podia ficar na ilha por vários segundos depois que outro começava
- Páginas web que tocam áudio sem capa, como uma mensagem de voz do WhatsApp, mostram um bloco com a inicial da página em vez do logo do navegador

## 0.2.0

- Lê os limites do Claude sozinha pelo comando `/usage` do Claude Code, então o Claude Usage Taskbar não é mais necessário. Os números dele ainda são usados quando estão mais recentes
- Notificações do Windows na ilha, desligadas por padrão. Os toasts do próprio app do Claude são ignorados enquanto os alertas do Claude da ilha estão ligados
- A faísca do Claude toca uma animação suave de pensando enquanto o Claude trabalha
- Instalador: atalhos no menu Iniciar e na Área de Trabalho, iniciar com o Windows, uma entrada na lista de apps do Windows e um desinstalador. `--portable` roda sem instalar
- O exe roda sem o .NET instalado
- Cerca de 40 MB de memória em vez de 166 MB, desenhando a ilha na CPU
- Roadmap em `docs/ROADMAP.md`

## 0.1.0

Primeira prévia.

- Uma ilha no topo da tela que se transforma entre os estados parada, compacta e expandida com animações de mola
- Música de qualquer player nos controles de mídia do Windows: capa, um equalizador colorido pela capa, uma barra de progresso em que você pode clicar para avançar, e tocar, pausar, próxima e anterior
- Atividade ao vivo do Claude Code: há quanto tempo o Claude está trabalhando, quando ele espera por você, e sessões paradas
- Anéis de uso do Claude para os limites de 5 horas e semanal, lidos do Claude Usage Taskbar
- Alertas quando o Claude termina uma tarefa ou precisa de você
- Uma segunda atividade se separa em uma bolha ao lado da ilha
- Encolhe para uma linha fina quando uma janela maximizada está na frente e se esconde em tela cheia
- Menu da bandeja com posição no topo ou embaixo, iniciar com o Windows e o que mostrar
- Inglês e português do Brasil
