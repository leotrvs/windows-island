# Segurança

[English](../SECURITY.md) | **Português (Brasil)**

## O que o app acessa

| Item | Acesso | Por quê |
|---|---|---|
| Controles de mídia do Windows | Lê título, artista, capa, posição e estado do player atual. Envia tocar, pausar, próxima, anterior e avançar quando você clica | Mostrar e controlar a música |
| `~/.claude/sessions` | Lê tipo, ID do processo, status, nome e horário do status de cada sessão, a cada segundo | Mostrar se o Claude Code está trabalhando ou esperando você. Nada é guardado. |
| `%LOCALAPPDATA%\ClaudeUsageTaskbar\usage.json` | Lê quando o arquivo muda | Mostrar os limites de 5 horas e semanal salvos pelo Claude Usage Taskbar |
| `%APPDATA%\WindowsIsland\settings.json` | Lê e grava | Guardar as preferências |
| Chave Run do Windows | Grava só quando você liga **Iniciar com o Windows** | Abrir o app quando você entra no Windows |

## O que o app nunca faz

- Ler `~/.claude/.credentials.json` ou qualquer outro login, token ou cookie do Claude
- Chamar as APIs da Anthropic ou se conectar à internet
- Guardar prompts, respostas, nomes de sessão ou caminhos de projeto
- Ler o conteúdo das suas notificações ou de outras janelas
- Enviar análises ou telemetria
- Pedir direitos de administrador

## Relatar uma vulnerabilidade

Use **Report a vulnerability** na aba Security deste repositório. Não abra uma issue pública.
