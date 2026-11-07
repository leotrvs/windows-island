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
| `%APPDATA%\WindowsIsland\settings.json` | Lê e grava | Guardar as preferências |
| `%LOCALAPPDATA%\Programs\WindowsIsland` | Lê e grava | Guardar o app instalado |
| Chave Run do Windows, lista de Aplicativos, atalhos | Lê e grava | Iniciar com o Windows, entrada de desinstalação e atalhos no menu Iniciar e na Área de Trabalho |

## O que o app nunca faz

- Ler `~/.claude/.credentials.json` ou qualquer outro login, token ou cookie do Claude
- Chamar as APIs da Anthropic ou se conectar à internet
- Guardar prompts, respostas, nomes de sessão ou caminhos de projeto
- Ler suas notificações sem você ligar a opção, ou ler o conteúdo de outras janelas
- Enviar análises ou telemetria
- Pedir direitos de administrador

O comando `/usage` faz parte do Claude Code, que se conecta à Anthropic com o seu próprio login. A ilha nunca vê esse login.

## Relatar uma vulnerabilidade

Use **Report a vulnerability** na aba Security deste repositório. Não abra uma issue pública.
