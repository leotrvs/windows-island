# Segurança

[English](../SECURITY.md) | **Português (Brasil)**

## O que o app acessa

| Item | Acesso | Por quê |
|---|---|---|
| Controles de mídia do Windows | Lê título, artista, capa, posição e estado do player atual. Envia tocar, pausar, próxima, anterior e avançar quando você clica | Mostrar e controlar a música |
| `~/.claude/sessions` | Lê tipo, ID do processo, status, nome e horário do status de cada sessão, a cada segundo | Mostrar se o Claude Code está trabalhando ou esperando você. Nada é guardado. |
| Comando `/usage` do Claude Code | Roda a cada 2 minutos e quando você abre o painel do Claude | Guarda só as porcentagens e os horários de renovação, em memória. Não chama o modelo e não gasta tokens. |
| `%LOCALAPPDATA%\ClaudeUsageTaskbar\usage.json` | Lê quando o arquivo muda, se existir | Usar os limites do Claude Usage Taskbar quando forem mais recentes |
| `%LOCALAPPDATA%\Microsoft\Windows\Notifications\wpndatabase.db` | Lê, somente leitura, a cada 1,5 segundo, só com **Mostrar notificações do Windows** ligado | Mostrar app, título e texto das notificações que chegam com a ilha aberta. Nada é guardado. |
| `~/.codex/sessions` | Lê o final do arquivo de sessão mais recente a cada 2 segundos, com **Mostrar Codex** ligado | Pega só se a última tarefa começou ou terminou e as porcentagens e horários de renovação dos limites. Nada é guardado. |
| `HKCU\...\CapabilityAccessManager\ConsentStore` | Lê dois horários por app (último início e fim do uso do microfone e da câmera) a cada 2 segundos, com **Pontos de microfone e câmera** ligado | Mostra os pontos. Nunca grava nem escuta áudio ou vídeo, e não lê qual app é |
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

## Acesso à rede

A própria ilha faz um único tipo de conexão: uns 30 segundos depois de abrir e depois a cada hora, ela baixa os arquivos de release deste repositório no GitHub, a menos que você desligue **Atualizações automáticas**. Faz o mesmo quando você clica em **Procurar atualizações**. O GitHub vê seu endereço IP, como em qualquer download. Nenhum dado do Claude ou pessoal é enviado. Uma atualização só é instalada quando a assinatura RSA bate com a chave pública embutida no app e o SHA-256 do arquivo bate com o manifesto assinado.

O comando `/usage` faz parte do Claude Code, que se conecta à Anthropic com o seu próprio login. A ilha nunca vê esse login.

## Relatar uma vulnerabilidade

Use **Report a vulnerability** na aba Security deste repositório. Não abra uma issue pública.
