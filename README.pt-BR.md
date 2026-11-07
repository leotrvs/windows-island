# Windows Island

[English](README.md) | **Português (Brasil)** | [Español](README.es.md) | [Русский](README.ru.md)

Uma Dynamic Island para o Windows. Música, Claude Code e os limites de uso ficam numa pequena pílula no topo da tela, com o visual do iPhone e animações de mola.

<p align="center">
<img src="docs/images/musicexpanded-pt.png" alt="A ilha expandida com a música tocando" width="460">
</p>

> Projeto não oficial da comunidade. Não é afiliado, endossado ou mantido pela Apple ou pela Anthropic. "Claude" é uma marca registrada da Anthropic.

> **Versão de prévia.** Ainda não há instalador. [Compile a partir do código](#compilar-a-partir-do-código) para testar.

## Recursos

- **Música** de qualquer player que aparece nos controles de mídia do Windows, como Spotify ou um navegador: capa, equalizador na cor da capa, barra de progresso que avança ao clicar, e tocar, pausar, próxima e anterior
- **Atividade ao vivo do Claude Code:** há quanto tempo o Claude está trabalhando e quando ele está esperando você
- **Uso do Claude:** anéis dos limites de **5 horas** e **semanal**, com o horário em que renovam
- **Avisos** quando o Claude termina uma tarefa ou precisa de você
- **Duas atividades ao mesmo tempo:** a segunda se separa numa bolha ao lado da ilha
- **Movimento fluido:** forma e conteúdo mudam com molas, nunca com transições de tempo fixo
- **Discreta:** nunca rouba o foco, cliques ao redor chegam à janela de baixo, vira uma linha fina quando há uma janela maximizada na frente e some em tela cheia
- Posição no topo ou embaixo, iniciar com o Windows, inglês e português do Brasil
- **Nunca lê seu login ou token do Claude** e não faz conexões de rede

## Estados

| Estado | Visual |
|---|---|
| Ociosa | <img src="docs/images/idle-pt.png" alt="Pílula ociosa" width="260"> |
| Música tocando, com o Claude trabalhando ao lado | <img src="docs/images/musiccompact-pt.png" alt="Música compacta com a bolha do Claude" width="380"> |
| Claude trabalhando, com o tempo decorrido | <img src="docs/images/claudecompact-pt.png" alt="Atividade compacta do Claude" width="320"> |
| Claude terminou ou precisa de você | <img src="docs/images/banner-pt.png" alt="Aviso do Claude" width="420"> |
| Claude expandido, com os limites | <img src="docs/images/claudeexpanded-pt.png" alt="Claude expandido com os anéis de uso" width="460"> |

## Uso

1. **Passe o mouse** sobre a ilha para expandir, ou **clique** nela. Tire o mouse e ela recolhe.
2. **Clique** na barra de progresso para ir até aquele ponto da música.
3. **Clique com o botão direito** no ícone da bandeja para ver as opções e **Sair**.

| Opção | O que faz |
|---|---|
| Mostrar música | Mostra o que está tocando |
| Mostrar Claude | Mostra a atividade e os limites do Claude Code |
| Avisos do Claude | Expande a ilha quando o Claude termina ou precisa de você |
| Mostrar pílula ociosa | Mantém a pílula na tela quando nada está acontecendo |
| Expandir ao passar o mouse | Expande quando o mouse para sobre a ilha. Desligado, é preciso clicar |
| Esconder em tela cheia | Esconde a ilha em jogos, vídeos e apresentações |
| Posição | Topo da tela, ou logo acima da barra de tarefas |
| Iniciar com o Windows | Abre o app quando você entra no Windows |
| Abrir arquivo de configurações | Abre o `settings.json`, onde `OffsetY` define a distância da borda |

## Como os dados do Claude são lidos

- **Atividade:** o Claude Code mantém um arquivo pequeno por sessão aberta em `~/.claude/sessions`. A ilha lê apenas o tipo, o ID do processo, o status, o nome e o horário do status. Funciona com o terminal e com o app de desktop.
- **Limites:** a ilha lê as porcentagens e os horários de renovação que o [Claude Usage Taskbar](https://github.com/leotrvs/claude-usage-taskbar) salva. Sem ele, a área de limites mostra uma dica e o resto continua funcionando.

Detalhes em [SECURITY.md](docs/SECURITY.pt-BR.md).

## Compilar a partir do código

Você precisa do Windows 10 versão 2004 ou mais recente, ou do Windows 11, e do [SDK do .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
git clone https://github.com/leotrvs/windows-island.git
cd windows-island
pwsh -File scripts/build.ps1
.\bin\WindowsIsland.exe
```

Para contribuir, leia o [CONTRIBUTING.md](CONTRIBUTING.md).

## Próximos passos

- Instalador e atualizações automáticas assinadas
- Ler os limites sozinha, sem o Claude Usage Taskbar
- Notificações do Windows na ilha
- Timers, bateria, e microfone e câmera em uso
- Um jeito de scripts e outros apps mostrarem as próprias atividades ao vivo
- Janela de configurações, temas e vários monitores
- Menos uso de memória

## Licença

[MIT](LICENSE)
