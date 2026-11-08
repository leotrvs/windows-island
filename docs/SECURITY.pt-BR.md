# Segurança

[English](../SECURITY.md) | **Português (Brasil)**

## O que o app acessa

| Item | Acesso | Por quê |
|---|---|---|
| Controles de mídia do Windows | Lê título, artista, capa, posição e estado do player atual. Envia tocar, pausar, próxima, anterior e avançar quando você clica | Mostrar e controlar a música |
| `~/.claude/sessions` | Lê tipo, ID do processo, status, nome e horário do status de cada sessão, a cada segundo | Mostrar se o Claude Code está trabalhando ou esperando você. Nada é guardado. |
| `~/.claude/projects/*/<sessão>.jsonl` (chats do Claude Desktop) | Com um chat ocupado, lê o final do histórico uma vez por segundo, só quando o arquivo mudou | Encontra uma chamada de ferramenta ainda sem resultado, para mostrar "aguardando você" quando o Claude Desktop pede uma permissão. Pega só nomes de ferramentas e horários, nunca o que foi dito. Nada é guardado |
| Pasta `Notification Resources` do Chrome e do Edge | Lê o único arquivo de ícone para o qual uma notificação de site aponta, só em notificações do Chrome ou do Edge e só dentro das pastas de perfil do próprio navegador (sem links, PNG, JPEG ou ICO, até 512 KB) | Mostra o ícone do site na notificação. Nada é guardado |
| Comando `/usage` do Claude Code | Roda a cada 2 minutos e quando você abre o painel do Claude | Guarda só as porcentagens e os horários de renovação, em memória. Não chama o modelo e não gasta tokens. |
| `%LOCALAPPDATA%\ClaudeUsageTaskbar\usage.json` | Lê quando o arquivo muda, se existir | Usar os limites do Claude Usage Taskbar quando forem mais recentes |
| `%LOCALAPPDATA%\Microsoft\Windows\Notifications\wpndatabase.db` | Lê, somente leitura, a cada 1,5 segundo, só com **Mostrar notificações do Windows** ligado | Mostrar app, título e texto das notificações que chegam com a ilha aberta. Nada é guardado. |
| `~/.codex/sessions` | Lê o final do arquivo de sessão mais recente a cada 2 segundos, com **Mostrar Codex** ligado | Pega só se a última tarefa começou ou terminou e as porcentagens e horários de renovação dos limites. Nada é guardado. |
| `HKCU\...\CapabilityAccessManager\ConsentStore` | Lê dois horários por app (último início e fim do uso do microfone e da câmera) a cada 2 segundos, com **Pontos de microfone e câmera** ligado | Mostra os pontos. Nunca grava nem escuta áudio ou vídeo, e não lê qual app é |
| A área de transferência do Windows, para prints | É avisada quando a área de transferência muda (sem ficar consultando), com **Aviso de captura de tela** ligado, e só confere que chegou uma imagem e que o Explorador do Windows ou a Ferramenta de Captura a colocou ali | Mostra uma nota pequena. Nunca lê a imagem nem textos da área de transferência, e não vigia o teclado |
| `HKCU\...\CapabilityAccessManager\ConsentStore\graphicsCapture*` | Lê dois horários por app (último início e fim da captura de tela) a cada 2 segundos, com **Pontos de microfone e câmera** e **Compartilhamento de tela** ligados | Mostra o ponto azul enquanto um app compartilha ou grava a tela. Nunca captura nem lê a tela |
| Sessões de áudio do Windows (Core Audio) | Pergunta a cada 2 segundos se algum microfone tem uma sessão de gravação ativa, com **Pontos de microfone e câmera** ligado | Mostra o ponto. Lê só o estado ativo/inativo e o número do processo: nenhum áudio é aberto ou escutado, e nenhum nome de app é guardado |
| `%APPDATA%\WindowsIsland\settings.json` | Lê e grava | Guardar as preferências |
| `%LOCALAPPDATA%\Programs\WindowsIsland` | Lê e grava | Guardar o app instalado |
| Chave Run do Windows, lista de Aplicativos, atalhos | Lê e grava | Iniciar com o Windows, entrada de desinstalação e atalhos no menu Iniciar e na Área de Trabalho |

## O que o app nunca faz

- Ler `~/.claude/.credentials.json`, `~/.codex/auth.json` ou qualquer outro login, token ou cookie do Claude ou do Codex
- Chamar as APIs da Anthropic
- Guardar prompts, respostas, nomes de sessão ou caminhos de projeto
- Ler suas notificações sem você ligar a opção, ou ler o conteúdo de outras janelas
- Enviar análises ou telemetria
- Pedir direitos de administrador

## Responder as permissões do Claude Code (desligado por padrão)

**Responder permissões pela ilha**, nos Ajustes, adiciona uma entrada às configurações do usuário do Claude Code (`~/.claude/settings.json`): um gancho `PermissionRequest` que roda `WindowsIsland.exe permission-hook`. O Claude Code só o executa quando vai pedir uma permissão a você. O gancho envia o nome da ferramenta e um resumo curto do que ela quer fazer (até 280 caracteres do comando, arquivo, endereço ou consulta) para a API local da ilha neste computador, com o token da API, e espera o seu clique.

- **Só o clique decide.** Permitir e Negar só são respondidos clicando nos botões. Se você não clicar em 60 segundos, a ilha não estiver aberta, a API estiver desligada ou algo falhar, o gancho não imprime nada e termina normalmente, então o Claude Code mostra o pedido dele como sempre.
- **Mostra um resumo, não tudo.** O resumo é o próprio comando, arquivo, endereço ou consulta (nunca uma descrição que o modelo escreveu), numa linha com as quebras de linha mostradas como ⏎, e com a pasta do projeto. Quando passa de 280 caracteres, ou a ferramenta não tem esse campo, **o Permitir não é oferecido**: você pode Negar ou abrir o Claude para decidir lá.
- **O clique tem que ser deliberado.** Os botões acordam cerca de meio segundo depois de o pedido aparecer, e um clique só vale se for apertado e solto no mesmo botão, então um clique que era para a janela de baixo (ou um clique duplo) não responde o pedido.
- **Uma entrada, com cópia de segurança.** Só a entrada da ilha é adicionada ou removida. Uma cópia do seu arquivo é salva ao lado como `settings.json.island-backup` na primeira vez (e `settings.json.island-backup-last` antes de cada mudança), o arquivo não é reescrito quando a entrada já está certa, e nada é alterado se o arquivo não puder ser lido. Comentários dentro do arquivo se perdem quando ele é reescrito; a cópia os mantém. Desligar a opção, ou desinstalar a ilha, remove a entrada.
- **O resumo fica só na memória** e nunca é guardado. A ilha só envia o token ao próprio processo (ela confere quem é o dono da porta antes).
- **Qualquer programa com o token poderia mostrar um pedido**, mas um pedido só é respondido pelo seu clique e só volta para o programa que o fez.

## Acesso à rede

A própria ilha faz um tipo principal de conexão (a segunda, abaixo, é opcional): uns 30 segundos depois de abrir e depois a cada hora, ela baixa os arquivos de release deste repositório no GitHub, a menos que você desligue **Atualizações automáticas**. Faz o mesmo quando você clica em **Procurar atualizações**. O GitHub vê seu endereço IP, como em qualquer download. Nenhum dado do Claude ou pessoal é enviado. Uma atualização só é instalada quando a assinatura RSA bate com a chave pública embutida no app e o SHA-256 do arquivo bate com o manifesto assinado.

Se a extensão de navegador estiver instalada e uma aba estiver tocando som, a ilha também baixa o `/apple-touch-icon.png` público do site (só HTTPS, sem redirecionamentos, PNG de até 256 KB, uma vez por site) para mostrar um ícone nítido. O site vê um pedido comum vindo do seu endereço IP e nada mais é enviado. Esse download sai direto do seu computador, sem passar pelo proxy ou pela VPN do navegador, e também acontece em abas anônimas se a extensão estiver liberada nelas. A ilha recusa endereços que apontam para este computador ou para a rede local e dá a cada download quatro segundos e 256 KB.

O comando `/usage` faz parte do Claude Code, que se conecta à Anthropic com o seu próprio login. A ilha nunca vê esse login.

## Relatar uma vulnerabilidade

Use **Report a vulnerability** na aba Security deste repositório. Não abra uma issue pública.
