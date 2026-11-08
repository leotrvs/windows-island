# Discord: muted and deafened on the island

**English** | [Português (Brasil)](#português-brasil)

Windows Island can show a crossed microphone (muted) or crossed headphones (deafened) beside the island while you are in Discord, however you muted yourself: a keyboard shortcut, the button, a headset button. It is **experimental** and off by default.

## How it works

Discord has a local RPC that other programs on the same computer can use to read your voice settings. Discord only lets a program use it after you create a **Discord application of your own** and approve it once. So there is a one-time setup:

1. Open <https://discord.com/developers/applications> and sign in with your Discord account.
2. Click **New Application**, name it (for example "My island") and create it.
3. Open **OAuth2**.
   - Copy the **Client ID** (also shown as Application ID on the first page).
   - Click **Reset Secret** and copy the **Client Secret**. It is shown once.
   - Under **Redirects**, add `http://127.0.0.1` and save.
4. You do **not** need to add yourself as a tester: the owner of the application can use it while it is in development (Discord even refuses the invitation, "you cannot invite yourself"). Testers are for **other people** who want to use your application (up to 50, under the application's settings, **App Testers**).
5. In Windows Island open **Settings, Activities, Discord (muted and deafened)**.
   - Paste the **Client ID** and the **Client Secret** and turn on **Show when I am muted in Discord**.
   - With Discord open, a window asks you to approve the application. Approve it.
   - The status line under the boxes says **Connected to Discord** when it works.

From then on a crossed microphone (muted) or crossed headphones (deafened) appears beside the island while you are muted.

## What it reads, and what is kept

- It reads only whether you are muted or deafened. It does not read messages, calls, servers or anything else.
- The Client Secret and the tokens Discord gives back are kept **encrypted for your Windows user** (DPAPI) in `%APPDATA%\WindowsIsland\discord.dat`. The Client ID is saved in `settings.json`.
- It sends the token only to Discord's own program on this computer: before anything is sent it checks that the program serving the pipe runs in your Windows session and is an executable inside your own `Discord`, `DiscordPTB` or `DiscordCanary` folder in `%LOCALAPPDATA%`, and the pipe cannot be used to act as you. A program that only calls itself Discord is not accepted.
- The permission you approve (`rpc` and `rpc.voice.read`) lets whoever holds the token read your voice settings and control the local Discord client, which is more than the island uses. That is why the token stays in the encrypted file and nowhere else, and **Forget the Discord data** in Settings deletes it.
- The only internet connection is to `https://discord.com/api/oauth2/token`, once to trade Discord's approval for a token and again when the token needs renewing.
- Turn the option off and nothing runs; the saved secret and tokens stay until you use **Forget the Discord data**.
- If Discord refuses (a wrong ID or secret), the island stops trying until you change the ID or the secret, so it does not make Discord show its approval window again and again.

## If it does not connect

| Status | What to do |
|---|---|
| Fill in the Client ID and the Client Secret | Both boxes are needed |
| Waiting for Discord to open | Open the Discord desktop app |
| Approve the request in the Discord window | A window is waiting inside Discord |
| Discord did not accept it: Invalid Client ID | The Client ID is wrong |
| Discord did not accept it (other) | Check the secret and the redirect `http://127.0.0.1`. Discord says the RPC permissions are only for approved applications; it worked for the owner of an application in development |

## For everyone

Discord only allows its RPC for approved applications, apart from the testers of an application (a limited number). Until Windows Island is approved by Discord, each person has to create their own application as above.

---

## Português (Brasil)

A Windows Island pode mostrar um microfone cortado (mudo) ou um fone cortado (ensurdecido) ao lado da ilha enquanto você está no Discord, não importa como você se mutou: atalho do teclado, botão, botão do headset. É **experimental** e vem desligado.

### Como funciona

O Discord tem uma API local (RPC) que outros programas do mesmo computador podem usar para ler suas configurações de voz. Ele só deixa um programa usar depois que você cria um **aplicativo do Discord só seu** e o aprova uma vez. Por isso há uma configuração única:

1. Abra <https://discord.com/developers/applications> e entre com a sua conta do Discord.
2. Clique em **New Application**, dê um nome (por exemplo "Minha ilha") e crie.
3. Abra **OAuth2**.
   - Copie o **Client ID** (também aparece como Application ID na primeira página).
   - Clique em **Reset Secret** e copie o **Client Secret**. Ele aparece uma vez só.
   - Em **Redirects**, adicione `http://127.0.0.1` e salve.
4. Você **não** precisa se adicionar como tester: o dono do aplicativo pode usá-lo enquanto ele está em desenvolvimento (o Discord até recusa o convite, "você não pode se convidar"). Testers são para **outras pessoas** que queiram usar o seu aplicativo (até 50, nas configurações do aplicativo, **App Testers**).
5. Na Windows Island abra **Ajustes, Atividades, Discord (mudo e ensurdecido)**.
   - Cole o **Client ID** e o **Client Secret** e ligue **Mostrar quando eu estou mudo no Discord**.
   - Com o Discord aberto, uma janela pede para você aprovar o aplicativo. Aprove.
   - A linha de status sob as caixas diz **Conectado ao Discord** quando funciona.

Daí em diante, um microfone cortado (mudo) ou um fone cortado (ensurdecido) aparece ao lado da ilha enquanto você está mudo.

### O que lê e o que é guardado

- Lê só se você está mudo ou ensurdecido. Não lê mensagens, chamadas, servidores nem mais nada.
- O Client Secret e os tokens que o Discord devolve ficam **criptografados para o seu usuário do Windows** (DPAPI) em `%APPDATA%\WindowsIsland\discord.dat`. O Client ID fica no `settings.json`.
- O token só vai para o programa do próprio Discord neste computador: antes de enviar qualquer coisa a ilha confere que o programa que atende o canal roda na sua sessão do Windows e é um executável dentro da sua pasta `Discord`, `DiscordPTB` ou `DiscordCanary` em `%LOCALAPPDATA%`, e que o canal não pode ser usado para agir como você. Um programa que só se chama Discord não é aceito.
- A permissão que você aprova (`rpc` e `rpc.voice.read`) deixa quem tem o token ler suas configurações de voz e controlar o cliente local do Discord, mais do que a ilha usa. Por isso o token fica só no arquivo criptografado, e **Esquecer os dados do Discord** nos Ajustes o apaga.
- A única conexão com a internet é com `https://discord.com/api/oauth2/token`, uma vez para trocar a aprovação do Discord por um token e de novo quando o token precisa ser renovado.
- Desligue a opção e nada roda; o secret e os tokens guardados ficam até você usar **Esquecer os dados do Discord**.
- Se o Discord recusar (ID ou secret errado), a ilha para de tentar até você mudar o ID ou o secret, para não fazer o Discord mostrar a janela de aprovação várias vezes.

### Se não conectar

| Status | O que fazer |
|---|---|
| Preencha o Client ID e o Client Secret | As duas caixas são necessárias |
| Esperando o Discord abrir | Abra o aplicativo do Discord |
| Aprove o pedido na janela do Discord | Há uma janela esperando dentro do Discord |
| O Discord não aceitou: Invalid Client ID | O Client ID está errado |
| O Discord não aceitou (outro) | Confira o secret e o redirect `http://127.0.0.1`. O Discord diz que as permissões do RPC são só para aplicativos aprovados; funcionou para o dono de um aplicativo em desenvolvimento |

### Para todo mundo

O Discord só libera o RPC para aplicativos aprovados, além dos testers de um aplicativo (um número limitado). Enquanto a Windows Island não for aprovada pelo Discord, cada pessoa precisa criar o seu aplicativo como acima.
