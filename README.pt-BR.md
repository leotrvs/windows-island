# Windows Island

[English](README.md) | **Português (Brasil)** | [Español](README.es.md) | [Русский](README.ru.md)

Uma Dynamic Island para o Windows. Música, Claude Code, os limites de uso e suas notificações ficam numa pequena pílula no topo da tela, com o visual do iPhone e animações de mola.

<p align="center">
<img src="docs/images/demo-pt.gif" alt="Windows Island animada: recolhida, expandindo, música, Claude, timer e um aviso" width="575">
</p>

<p align="center">
<a href="https://github.com/leotrvs/windows-island/releases/latest/download/WindowsIsland.exe"><img alt="Baixar para Windows" src="https://img.shields.io/badge/Baixar_para_Windows-0A84FF?style=for-the-badge&logo=windows&logoColor=white"></a>
</p>

> Projeto não oficial da comunidade. Não é afiliado, endossado ou mantido pela Apple, pela Anthropic ou pela OpenAI. "Claude" e o logo do Claude pertencem à Anthropic, e "Codex" à OpenAI; a licença MIT não os cobre.

## Recursos

<table>
<tr>
<td width="33%" valign="top"><b>Música</b><br>Capa, um equalizador nas cores da capa, barra de progresso e os controles, de qualquer player</td>
<td width="33%" valign="top"><b>Claude e Codex</b><br>Atividade ao vivo, limites de uso, avisos e permissões respondidas direto na ilha</td>
<td width="33%" valign="top"><b>Atividades ao vivo</b><br>Downloads da Steam e do navegador, gravação do OBS e do Game Bar, atualizações do Windows, o placar do seu time, timers</td>
</tr>
<tr>
<td width="33%" valign="top"><b>A linha fina</b><br>Recolhida, a ilha é uma linha fina que mostra o que mais importa; passe o mouse para abrir</td>
<td width="33%" valign="top"><b>Privacidade</b><br>Pontos de microfone, câmera, compartilhamento de tela e gravação, com o nome do app</td>
<td width="33%" valign="top"><b>Do seu jeito</b><br>Ajustes Essenciais e Avançado, boas-vindas no primeiro uso, quatro idiomas, Ajustes claro ou escuro</td>
</tr>
</table>

<details>
<summary><b>Todos os recursos</b></summary>

- **Música** de qualquer player que aparece nos controles de mídia do Windows, como Spotify ou um navegador: capa, equalizador na cor da capa, barra de progresso que avança ao clicar, e tocar, pausar, próxima e anterior
- **Atividade ao vivo do Claude Code:** o símbolo do Claude "pensa" enquanto ele trabalha, com o tempo decorrido, e pulsa quando ele está esperando você
- **Uso do Claude:** anéis dos limites de **5 horas** e **semanal**, com o horário em que renovam
- **Codex:** atividade ao vivo enquanto o Codex trabalha, os limites de 5 horas e semanal, e um aviso quando ele termina
- **Páginas:** com a ilha expandida, role o mouse sobre ela ou clique nos pontinhos para alternar entre música, Claude e timer, que também tem seus botões ali
- **Timer e cronômetro:** inicie um pelo menu da bandeja; ele aparece como atividade ao vivo e avisa quando o tempo acaba
- **Pontos de microfone e câmera:** laranja com o microfone em uso, verde com a câmera, como no iPhone
- **Bateria** em notebooks: aparece como atividade ao carregar, mais avisos de carregador conectado ou removido, bateria baixa e carregada
- **Nomes dos apps nos pontos:** veja qual app usa o microfone ou a câmera ao passar o mouse sobre a ilha
- **Ponto de compartilhamento de tela:** um ponto azul enquanto um app compartilha ou grava a tela, como numa chamada ou numa gravação
- **Downloads da Steam (ligado por padrão):** o progresso de um jogo sendo baixado ou atualizado na Steam, lido dos arquivos de manifesto que a Steam guarda ao lado dos jogos
- **Mais atividades ao vivo:** OBS gravando, atualizações do Windows, downloads do navegador (com a extensão) e o placar ao vivo do seu time (desligado por padrão), além de uma linha fina mais rica, com um ícone pequeno do que mais importa
- **Discord mudo ou ensurdecido (experimental, desligado por padrão):** um microfone cortado ou um fone cortado ao lado da ilha enquanto você está mudo no Discord, não importa como se mutou. Precisa de um app seu no Discord, veja [docs/DISCORD.md](docs/DISCORD.md)
- **Ajustes claro ou escuro:** a janela de Ajustes pode seguir o tema do Windows ou ficar no claro ou no escuro; a ilha em si é sempre preta
- **Sua ordem e seus tempos:** escolha a ordem das páginas, quais avisos de limite mostrar (75%, 90%, 100%, renovado) e quanto tempo ficam os avisos, as notificações e os avisos de limite
- **Barra de volume e brilho**, avisos de **Bluetooth, USB e rede** e uma lua para **Não perturbe**
- **Página de relógio e sistema** com hora, data, carga do processador e da memória
- **Monitor do sistema** (desligado por padrão): mantém a carga do processador e da memória na ilha quando nada mais está acontecendo, em anéis, barras ou números
- **Recolher:** arraste a ilha para cima para guardá-la e usar o que está atrás; ela volta com uma nova atividade ou quando o mouse fica parado no lugar dela
- **API local e linha de comando** (desligadas por padrão) para seus scripts e apps mostrarem atividades e avisos próprios, veja [docs/API.md](docs/API.md), e uma pequena [extensão de navegador](extension/README.md) que dá ao áudio da web o ícone do site
- **Atalho de teclado** para abrir e fechar a ilha (desligado por padrão, configurável)
- **Avisos** quando o Claude termina uma tarefa ou precisa de você
- **Avisos de limite:** um aviso quando o limite do Claude de 5 horas ou semanal chega a 75%, 90% e 100%, e quando renova
- **Notificações do Windows** na ilha, com o ícone do app que as enviou, se você ligar
- **Duas atividades ao mesmo tempo:** a segunda se separa numa bolha ao lado da ilha
- **Vários chats do Claude ao mesmo tempo:** a ilha os conta (por exemplo `2 · 0:58`, ou `1/2 · Aguardando` quando um de dois espera você) e a página do Claude lista cada chat com seu estado e tempo
- **Permissões no Claude Desktop:** um chat do app de desktop do Claude que pede uma permissão ou faz uma pergunta aparece como esperando você, como no terminal A pergunta (quando é curta) aparece no aviso, com um link **Abrir o Claude** na página do Claude que traz o app para a frente para você responder.
- **Responder permissões pela ilha (experimental, desligado por padrão):** quando o Claude Code vai pedir uma permissão, como ler um banco de dados ou aplicar uma migration, a ilha mostra a ferramenta e o que ela quer fazer com **Permitir** e **Negar**. Usa um gancho que você liga nos Ajustes; se você não clicar a tempo, o Claude pergunta como sempre
- **Ícones de sites:** com a extensão de navegador, o áudio da web mostra o ícone do próprio site, e as notificações de sites mostram o ícone e o nome do site
- **Movimento fluido:** forma e conteúdo mudam com molas, nunca com transições de tempo fixo
- **A linha fina é o visual padrão:** uma linha fina que mostra nela mesma o que mais importa: um brilho enquanto o Claude trabalha, um equalizador pequeno para a música, um preenchimento para o timer, um download ou a carga, um pulso quando o Claude espera você ou quando você está gravando, e dois anéis pequenos para o processador e a memória
- **Discreta:** nunca rouba o foco, cliques ao redor chegam à janela de baixo, vira uma linha fina quando há uma janela maximizada na frente e some em tela cheia
- **Leve:** cerca de 40 MB de memória e menos de 1% de CPU com música tocando
- Instala só para o seu usuário, com atalhos no menu Iniciar e na Área de Trabalho e início com o Windows
- Inglês e português do Brasil
- Atualizações automáticas assinadas
- **Nunca lê seu login ou token do Claude**. As únicas conexões são a verificação de atualizações neste repositório e, se você usa a extensão de navegador, o download de um ícone pequeno por site e, só se você ligar o **Placar de futebol**, o placar público da ESPN

</details>

## Estados

| Estado | Visual |
|---|---|
| Ociosa | <img src="docs/images/idle-pt.png" alt="Pílula ociosa" width="260"> |
| Música tocando, com o Claude trabalhando ao lado | <img src="docs/images/musiccompact-pt.gif" alt="Música compacta com a bolha do Claude" width="380"> |
| Claude trabalhando, com o tempo decorrido | <img src="docs/images/claudecompact-pt.gif" alt="Atividade compacta do Claude" width="320"> |
| Codex trabalhando | <img src="docs/images/codexcompact-pt.gif" alt="Atividade compacta do Codex" width="320"> |
| Claude terminou ou precisa de você | <img src="docs/images/banner-pt.gif" alt="Aviso do Claude" width="420"> |
| Uma notificação do Windows | <img src="docs/images/notification-pt.gif" alt="Aviso de notificação" width="420"> |
| Bateria carregando | <img src="docs/images/battery-charging-pt.gif" alt="Aviso de carregamento" width="420"> |
| Bateria baixa | <img src="docs/images/battery-low-pt.gif" alt="Aviso de bateria baixa" width="420"> |
| Claude expandido, com os limites | <img src="docs/images/claudeexpanded-pt.gif" alt="Claude expandido com os anéis de uso" width="460"> |
| Claude precisa de você (uma permissão ou uma pergunta) | <img src="docs/images/banner-waiting-pt.gif" alt="Claude precisa de você (uma permissão ou uma pergunta)" width="420"> |
| Claude Code pede uma permissão | <img src="docs/images/permission-pt.gif" alt="Claude Code pede uma permissão" width="460"> |
| Limite do Claude em 75% ou 90% | <img src="docs/images/usage-90-pt.gif" alt="Limite do Claude em 75% ou 90%" width="420"> |
| Limite do Claude atingido | <img src="docs/images/usage-100-pt.gif" alt="Limite do Claude atingido" width="420"> |
| Limite do Claude renovado | <img src="docs/images/usage-reset5-pt.gif" alt="Limite do Claude renovado" width="420"> |
| Vários chats trabalhando ao mesmo tempo | <img src="docs/images/chats-working-compact-pt.gif" alt="Vários chats trabalhando ao mesmo tempo" width="320"> |
| Um de dois chats esperando você | <img src="docs/images/chats-waiting-compact-pt.gif" alt="Um de dois chats esperando você" width="320"> |
| A página do Claude lista cada chat | <img src="docs/images/chats-waiting-expanded-pt.gif" alt="A página do Claude lista cada chat" width="460"> |
| Monitor do sistema: anéis, barras ou números | <img src="docs/images/systemcompact-pt.gif" alt="Monitor do sistema: anéis, barras ou números" width="260"> |
|  | <img src="docs/images/systemcompact-bars-pt.gif" alt="Monitor do sistema: anéis, barras ou números" width="260"> |
|  | <img src="docs/images/systemcompact-numbers-pt.gif" alt="Monitor do sistema: anéis, barras ou números" width="260"> |
| Volume ou brilho mudando | <img src="docs/images/level-pt.gif" alt="Volume bar" width="260"> |

### A linha fina

A ilha recolhida é uma linha fina por padrão (Ajustes, Comportamento, "Linha fina sempre"). Ela mostra só o estado mais importante, desenhado na própria linha; passe o mouse para abrir a ilha.

| Estado | Visual |
|---|---|
| Em repouso | <img src="docs/images/thin-idle-pt.png" alt="Em repouso" width="300"> |
| Claude trabalhando: um brilho corre por ela (amarelo ou vermelho conforme o limite enche) | <img src="docs/images/thin-claude-pt.gif" alt="Claude trabalhando: um brilho corre por ela (amarelo ou vermelho conforme o limite enche)" width="300"> |
| Claude espera você: a linha pulsa | <img src="docs/images/thin-waiting-pt.gif" alt="Claude espera você: a linha pulsa" width="300"> |
| Música: um equalizador pequeno na cor da capa | <img src="docs/images/thin-music-pt.gif" alt="Música: um equalizador pequeno na cor da capa" width="300"> |
| Timer: a linha esvazia conforme o tempo acaba | <img src="docs/images/thin-timer-pt.gif" alt="Timer: a linha esvazia conforme o tempo acaba" width="300"> |
| Um download, uma compilação ou um jogo da Steam: a linha enche com o progresso | <img src="docs/images/thin-progress-pt.gif" alt="Um download, uma compilação ou um jogo da Steam: a linha enche com o progresso" width="300"> |
| Carregando: a linha enche com a carga | <img src="docs/images/thin-charging-pt.gif" alt="Carregando: a linha enche com a carga" width="300"> |
| Gravação de tela (OBS ou Game Bar): uma linha vermelha fraca com um ponto piscando | <img src="docs/images/thin-recording-pt.gif" alt="Gravação de tela (OBS ou Game Bar): uma linha vermelha fraca com um ponto piscando" width="300"> |
| Bateria baixa ou erro: a linha pulsa em vermelho | <img src="docs/images/thin-alert-pt.gif" alt="Bateria baixa ou erro: a linha pulsa em vermelho" width="300"> |
| Sem internet: uma linha vermelha tracejada que pisca | <img src="docs/images/thin-offline-pt.gif" alt="Sem internet: uma linha vermelha tracejada que pisca" width="300"> |
| Concluído: a linha fica verde | <img src="docs/images/thin-done-pt.gif" alt="Concluído: a linha fica verde" width="300"> |
| Foco: uma luazinha | <img src="docs/images/thin-focus-pt.gif" alt="Foco: uma luazinha" width="300"> |
| Monitor do sistema: dois anéis pequenos, processador e memória | <img src="docs/images/thin-gauges-pt.gif" alt="Monitor do sistema: dois anéis pequenos, processador e memória" width="300"> |
| Microfone e câmera: pontos dentro da linha | <img src="docs/images/thin-privacy-pt.png" alt="Microfone e câmera: pontos dentro da linha" width="300"> |
| Uma segunda atividade: uma bolinha pequena ao lado da linha | <img src="docs/images/thin-bubble-pt.gif" alt="Uma segunda atividade: uma bolinha pequena ao lado da linha" width="300"> |
| Troca de música: a capa, o nome e um símbolo | <img src="docs/images/thin-peek-pt.gif" alt="Troca de música: a capa, o nome e um símbolo" width="300"> |
| Volume ou brilho: uma barrinha | <img src="docs/images/thin-level-pt.gif" alt="Volume ou brilho: uma barrinha" width="300"> |

### Primeiro uso e Ajustes

Na primeira vez que roda (e na primeira vez depois de atualizar para a 4.0), uma boas-vindas curta pergunta como a ilha deve ficar (linha fina quando parada, sempre linha fina ou pílula) e o que mostrar (música, Claude, notificações do Windows, pontos de microfone e câmera, permissões do Claude, iniciar com o Windows). Os Ajustes têm dois modos: **Essenciais**, com poucos interruptores, e **Avançado**, com todos os ajustes em quatro abas e uma caixa de busca.

<img src="docs/images/welcome-1-pt.png" alt="Welcome, step 1" width="200"> <img src="docs/images/welcome-2-pt.png" alt="Welcome, step 2" width="200"> <img src="docs/images/welcome-3-pt.png" alt="Welcome, step 3" width="200">

## Instalar

1. **Baixe** o `WindowsIsland.exe` da [última release](https://github.com/leotrvs/windows-island/releases/latest). O Windows pode mostrar um aviso do SmartScreen porque o app não tem certificado pago de assinatura de código: escolha **Mais informações** e depois **Executar assim mesmo**.
2. **Abra o arquivo** e escolha **Sim**. Ele instala só para o seu usuário do Windows, sem direitos de administrador, cria atalhos no menu Iniciar e na Área de Trabalho e inicia com o Windows.
3. A ilha aparece no topo da tela, e o ícone dela na bandeja.

Para testar sem instalar, rode `WindowsIsland.exe --portable`.

## Uso

1. **Passe o mouse** sobre a ilha para expandir, ou **clique** nela. Tire o mouse e ela recolhe.
2. **Clique** na barra de progresso para ir até aquele ponto da música.
3. **Clique** num aviso ou notificação para dispensar.
4. **Clique** no ícone da bandeja, ou abra o atalho do Windows Island com ele rodando, para abrir os **Ajustes**. Clique com o botão direito no ícone para as ações rápidas e **Sair**. Clique de novo no ícone da bandeja para fechar os Ajustes.

<img src="docs/images/settings-pt.png" alt="A janela de ajustes" width="900">

| Opção | O que faz |
|---|---|
| Mostrar música | Mostra o que está tocando |
| Mostrar Claude | Mostra a atividade e os limites do Claude Code |
| Avisos do Claude | Expande a ilha quando o Claude termina ou precisa de você |
| Mostrar Codex, Avisos do Codex | Atividade e limites do Codex, e o aviso quando ele termina. Só aparecem enquanto o Codex está trabalhando |
| Avisos de limite | Um aviso quando o limite do Claude de 5 horas ou o semanal chega a 75%, 90% e 100%, e quando renova |
| Pontos de microfone e câmera | Mostra um pontinho ao lado da ilha enquanto um app usa o microfone (laranja) ou a câmera (verde) |
| Avisos de bateria | Carregador conectado ou removido, bateria baixa e carregada. Só em notebooks |
| Mostrar notificações do Windows | Mostra as notificações novas na ilha. Vem ligado |
| Tamanho dos avisos | Avisos e notificações pequenos ou normais |
| Aviso de captura de tela | Uma nota bem pequena quando você tira um print com Print Screen ou Win+Shift+S |
| Cor dos anéis de uso | Azul, verde ou roxo. Os anéis ficam amarelos e vermelhos perto do limite |
| Idioma | Segue o Windows por padrão. Inglês, português, espanhol e russo |
| Ilha vazia | Mantém a ilha recolhida como uma pílula simples, sem atividades. Passe o mouse ou clique para vê-las |
| Mostrar pílula ociosa | Mantém a pílula na tela quando nada está acontecendo |
| Expandir ao passar o mouse | Expande quando o mouse para sobre a ilha. Desligado, é preciso clicar |
| Esconder em tela cheia | Esconde a ilha em jogos, vídeos e apresentações |
| Posição | Topo da tela, ou logo acima da barra de tarefas |
| Monitor | O principal, um específico, todos, ou o que está com o mouse |
| Tamanho recolhida, tamanho expandida | De 80% a 130%, separado para cada estado |
| Distância da borda, posição horizontal | Afasta a ilha da borda ou move para o lado |
| Esconder sozinha quando ociosa | Esconde a ilha depois de um tempo sem mostrar nada. Ela volta com uma atividade nova ou quando você leva o mouse até o lugar dela |
| Duração dos avisos | Quanto tempo os avisos do Claude e as notificações ficam na tela |
| Esconder música pausada após | Quanto tempo uma música pausada fica na ilha |
| Iniciar com o Windows | Abre o app quando você entra no Windows |
| Abrir arquivo de configurações | Abre o `settings.json`, onde `OffsetY` define a distância da borda |

## Como os dados são lidos

- **Atividade do Claude:** o Claude Code mantém um arquivo pequeno por sessão aberta em `~/.claude/sessions`. A ilha lê apenas o tipo, o ID do processo, o status, o nome e o horário do status. Funciona com o terminal e com o app de desktop. O app de desktop mantém um chat como ocupado enquanto pede uma permissão, então a ilha também lê o histórico do chat, só nomes de ferramentas e horários, nunca o que foi dito, para saber que uma ferramenta espera a sua resposta.
- **Limites do Claude:** a cada 2 minutos, e quando você abre o painel do Claude, a ilha roda `claude -p /usage` em segundo plano, os mesmos números que você vê no Claude Code. Não chama o modelo e não gasta tokens. Se o [Claude Usage Taskbar](https://github.com/leotrvs/claude-usage-taskbar) estiver instalado e tiver números mais recentes, a ilha usa esses.
- **Codex:** o Codex guarda cada sessão como um arquivo em `~/.codex/sessions`. A cada 2 segundos a ilha lê o final do mais recente e pega só se a última tarefa começou ou terminou e as porcentagens de limite que o Codex registrou. Elas se atualizam sempre que você usa o Codex. O login do Codex nunca é lido.
- **Notificações:** a API oficial do Windows para ler notificações exige um pacote assinado, então a ilha lê, somente leitura, o banco local onde o Windows as guarda. Mostra só as notificações que chegam enquanto ela está aberta e não guarda nada. O Windows continua mostrando o próprio aviso: desligue **Mostrar faixas de notificação** de um app nas Configurações do Windows se quiser só a ilha.
- **Ícones de sites:** com a extensão de navegador instalada e uma aba tocando som, a ilha baixa uma vez o `apple-touch-icon.png` público do site (só HTTPS, sem redirecionamentos, um PNG pequeno) para mostrar um ícone nítido. O site vê um pedido comum vindo do seu endereço IP. As notificações de sites do Chrome ou do Edge trazem o ícone do site como um arquivo que o próprio navegador salva na pasta `Notification Resources`; a ilha lê só esse arquivo.

Detalhes em [SECURITY.md](docs/SECURITY.pt-BR.md).

## Atualizações

O histórico completo de mudanças está em [CHANGELOG.pt-BR.md](CHANGELOG.pt-BR.md) (português) e [CHANGELOG.md](CHANGELOG.md) (inglês, o original).

A cada hora a ilha confere a última release do GitHub. Ela só instala uma atualização se a assinatura digital bater com a chave embutida no app e o hash do arquivo bater com a lista assinada, então um arquivo adulterado é recusado. Ela reinicia na versão nova quando você não está usando a ilha e mostra um aviso rápido. Desligue em **Atualizações automáticas** no menu da bandeja, onde **Procurar atualizações** faz uma verificação na hora.

## Desinstalar

Vá em **Configurações do Windows > Aplicativos > Aplicativos instalados > Windows Island > Desinstalar**. Isso remove o app, os atalhos e as configurações.

## Compilar a partir do código

Você precisa do Windows 10 versão 2004 ou mais recente, ou do Windows 11, e do [SDK do .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0). O exe gerado roda sem o .NET instalado.

```powershell
git clone https://github.com/leotrvs/windows-island.git
cd windows-island
pwsh -File scripts/build.ps1
.\bin\WindowsIsland.exe
```

Para contribuir, leia o [CONTRIBUTING.md](CONTRIBUTING.md).

## Próximos passos

A lista completa do que está planejado fica em [docs/ROADMAP.md](docs/ROADMAP.md).

## Licença

[MIT](LICENSE). O nome e o logo do Claude não estão cobertos.

A fonte Inter vai junto no app, sob a SIL Open Font License, veja [src/Assets/fonts/NOTICE.md](src/Assets/fonts/NOTICE.md).
