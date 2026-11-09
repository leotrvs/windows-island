# Windows Island

[English](README.md) | [Português (Brasil)](README.pt-BR.md) | **Español** | [Русский](README.ru.md)

Una Dynamic Island para Windows. La música, Claude Code, sus límites de uso y tus notificaciones viven en una pequeña píldora en la parte superior de la pantalla, con el aspecto del iPhone y animaciones de resorte.

<p align="center">
<img src="docs/images/demo-en.gif" alt="Windows Island animada: contraída, expandiéndose, música, Claude, temporizador y un aviso" width="575">
</p>

<p align="center">
<a href="https://github.com/leotrvs/windows-island/releases/latest/download/WindowsIsland.exe"><img alt="Descargar para Windows" src="https://img.shields.io/badge/Descargar_para_Windows-0A84FF?style=for-the-badge&logo=windows&logoColor=white"></a>
</p>

> Proyecto no oficial de la comunidad. No está afiliado, respaldado ni mantenido por Apple, Anthropic ni OpenAI. "Claude" y el logo de Claude pertenecen a Anthropic, y "Codex" a OpenAI; la licencia MIT no los cubre.

## Funciones

<table>
<tr>
<td width="33%" valign="top"><b>Música</b><br>Portada, un ecualizador con los colores de la portada, barra de progreso y los controles, de cualquier reproductor</td>
<td width="33%" valign="top"><b>Claude y Codex</b><br>Actividad en vivo, límites de uso, avisos y permisos respondidos en la propia isla</td>
<td width="33%" valign="top"><b>Actividades en vivo</b><br>Descargas de Steam y del navegador, grabación de OBS y de la Game Bar, actualizaciones de Windows, el marcador de tu equipo, temporizadores</td>
</tr>
<tr>
<td width="33%" valign="top"><b>La línea fina</b><br>Recogida, la isla es una línea fina que muestra lo más importante; pasa el ratón para abrirla</td>
<td width="33%" valign="top"><b>Privacidad</b><br>Puntos de micrófono, cámara, pantalla compartida y grabación, con el nombre de la app</td>
<td width="33%" valign="top"><b>A tu manera</b><br>Ajustes Esenciales y Avanzado, una bienvenida en el primer uso, cuatro idiomas, Ajustes claros u oscuros</td>
</tr>
</table>

<details>
<summary><b>Todas las funciones</b></summary>

- **Música** de cualquier reproductor que aparezca en los controles multimedia de Windows, como Spotify o un navegador: carátula, un ecualizador con el color de la carátula, una barra de progreso en la que puedes hacer clic para avanzar, y reproducir, pausar, siguiente y anterior
- **Actividad en vivo de Claude Code:** el símbolo de Claude "piensa" mientras Claude trabaja, con el tiempo transcurrido, y late cuando te está esperando
- **Uso de Claude:** anillos para los límites de **5 horas** y **semanal**, con la hora en que se renuevan
- **Codex:** una actividad en vivo mientras Codex trabaja, sus límites de 5 horas y semanal, y un aviso cuando termina
- **Páginas:** con la isla expandida, desplaza la rueda sobre ella o haz clic en los puntos para cambiar entre música, Claude y temporizador, que también tiene sus botones allí
- **Temporizador y cronómetro:** inícialos desde el menú de la bandeja; aparecen como actividad en vivo y avisan cuando se acaba el tiempo
- **Puntos de micrófono y cámara:** naranja con el micrófono en uso, verde con la cámara, como en el iPhone
- **Batería** en portátiles: aparece como actividad al cargar, además de avisos de cargador conectado o retirado, batería baja y carga completa
- **Nombres de las apps en los puntos:** mira qué app usa el micrófono o la cámara al pasar el ratón por la isla
- **Punto de pantalla compartida:** un punto azul mientras una app comparte o graba la pantalla, como en una llamada o una grabación
- **Descargas de Steam (activado por defecto):** el progreso de un juego que se descarga o actualiza en Steam, leído de los archivos de manifiesto que Steam guarda junto a los juegos
- **Más actividades en vivo:** OBS grabando, actualizaciones de Windows, descargas del navegador (con la extensión) y el marcador en vivo de tu equipo (desactivado por defecto), además de una línea fina más rica con un icono pequeño de lo más importante
- **Discord silenciado o ensordecido (experimental, desactivado por defecto):** un micrófono tachado o unos auriculares tachados junto a la isla mientras estás silenciado en Discord, lo hayas hecho como lo hayas hecho. Necesita una app tuya en Discord, mira [docs/DISCORD.md](docs/DISCORD.md)
- **Ajustes claros u oscuros:** la ventana de Ajustes puede seguir el tema de Windows o quedarse en claro u oscuro; la isla siempre es negra
- **Tu orden y tus tiempos:** elige el orden de las páginas, qué avisos de límite mostrar (75%, 90%, 100%, renovado) y cuánto duran los avisos, las notificaciones y los avisos de límite
- **Barra de volumen y brillo**, avisos de **Bluetooth, USB y red** y una luna para **No molestar**
- **Página de reloj y sistema** con la hora, la fecha, la carga del procesador y de la memoria
- **Monitor del sistema** (desactivado por defecto): mantiene la carga del procesador y de la memoria en la isla cuando no pasa nada más, en anillos, barras o números
- **Guardar:** arrastra la isla hacia arriba para guardarla y usar lo que hay detrás; vuelve con una actividad nueva o cuando el ratón se queda quieto en su sitio
- **API local y línea de comandos** (desactivadas por defecto) para que tus scripts y apps muestren sus propias actividades y avisos, mira [docs/API.md](docs/API.md), y una pequeña [extensión de navegador](extension/README.md) que da al audio web el icono del sitio
- **Atajo de teclado** para abrir y cerrar la isla (desactivado por defecto, configurable)
- **Avisos** cuando Claude termina una tarea o te necesita
- **Avisos de límite:** un aviso cuando el límite de Claude de 5 horas o el semanal llega al 75%, 90% y 100%, y cuando se renueva
- **Notificaciones de Windows** en la isla, con el icono de la app que las envió, si las activas
- **Dos actividades a la vez:** la segunda se separa en una burbuja junto a la isla
- **Varios chats de Claude a la vez:** la isla los cuenta (por ejemplo `2 · 0:58`, o `1/2 · Esperando` cuando uno de dos te espera) y la página de Claude lista cada chat con su estado y tiempo
- **Permisos en Claude Desktop:** un chat de la app de escritorio de Claude que pide un permiso o hace una pregunta aparece como esperándote, igual que en el terminal La pregunta (si es corta) aparece en el aviso, con un enlace **Abrir Claude** en la página de Claude que trae la app al frente para responder.
- **Responder permisos en la isla (experimental, desactivado por defecto):** cuando Claude Code va a pedir un permiso, como leer una base de datos o aplicar una migración, la isla muestra la herramienta y lo que quiere hacer con **Permitir** y **Denegar**. Usa un gancho que activas en Ajustes; si no haces clic a tiempo, Claude pregunta como siempre
- **Iconos de sitios:** con la extensión del navegador, el audio web muestra el icono del propio sitio, y las notificaciones de sitios muestran el icono y el nombre del sitio
- **Movimiento fluido:** la forma y el contenido cambian con resortes, nunca con transiciones de duración fija
- **La línea fina es el aspecto por defecto:** una línea fina que muestra en sí misma lo más importante: un brillo mientras Claude trabaja, un ecualizador pequeño para la música, un relleno para el temporizador, una descarga o la carga, un pulso cuando Claude te espera o cuando grabas, y dos anillos pequeños para el procesador y la memoria
- **Discreta:** nunca toma el foco, los clics a su alrededor llegan a la ventana de abajo, se reduce a una línea fina cuando hay una ventana maximizada delante y se oculta en pantalla completa
- **Ligera:** unos 40 MB de memoria y menos del 1% de CPU con música sonando
- Se instala solo para tu usuario, con accesos directos en el menú Inicio y el Escritorio e inicio con Windows
- Inglés y portugués de Brasil
- Actualizaciones automáticas firmadas
- **Nunca lee tu inicio de sesión ni tu token de Claude**. Sus únicas conexiones son buscar actualizaciones en este repositorio y, si usas la extensión del navegador, la descarga de un icono pequeño por sitio y, solo si activas el **Marcador de fútbol**, el marcador público de ESPN

</details>

## Estados

| Estado | Aspecto |
|---|---|
| En reposo | <img src="docs/images/idle-en.png" alt="Píldora en reposo" width="260"> |
| Música sonando, con Claude trabajando al lado | <img src="docs/images/musiccompact-en.png" alt="Música compacta con la burbuja de Claude" width="380"> |
| Claude trabajando, con el tiempo transcurrido | <img src="docs/images/claudecompact-en.png" alt="Actividad compacta de Claude" width="320"> |
| Codex trabajando | <img src="docs/images/codexcompact-en.png" alt="Actividad compacta de Codex" width="320"> |
| Claude terminó o te necesita | <img src="docs/images/banner-en.png" alt="Aviso de Claude" width="420"> |
| Una notificación de Windows | <img src="docs/images/notification-en.png" alt="Aviso de notificación" width="420"> |
| Batería cargando | <img src="docs/images/battery-charging-en.png" alt="Aviso de carga" width="420"> |
| Batería baja | <img src="docs/images/battery-low-en.png" alt="Aviso de batería baja" width="420"> |
| Claude expandido, con los límites | <img src="docs/images/claudeexpanded-en.png" alt="Claude expandido con los anillos de uso" width="460"> |
| Claude te necesita (un permiso o una pregunta) | <img src="docs/images/banner-waiting-en.png" alt="Claude te necesita (un permiso o una pregunta)" width="420"> |
| Claude Code pide un permiso | <img src="docs/images/permission-en.png" alt="Claude Code pide un permiso" width="460"> |
| Límite de Claude al 75% o 90% | <img src="docs/images/usage-90-en.png" alt="Límite de Claude al 75% o 90%" width="420"> |
| Límite de Claude alcanzado | <img src="docs/images/usage-100-en.png" alt="Límite de Claude alcanzado" width="420"> |
| Límite de Claude renovado | <img src="docs/images/usage-reset5-en.png" alt="Límite de Claude renovado" width="420"> |
| Varios chats trabajando a la vez | <img src="docs/images/chats-working-compact-en.png" alt="Varios chats trabajando a la vez" width="320"> |
| Uno de dos chats te espera | <img src="docs/images/chats-waiting-compact-en.png" alt="Uno de dos chats te espera" width="320"> |
| La página de Claude lista cada chat | <img src="docs/images/chats-waiting-expanded-en.png" alt="La página de Claude lista cada chat" width="460"> |
| Monitor del sistema: anillos, barras o números | <img src="docs/images/systemcompact-en.png" alt="Monitor del sistema: anillos, barras o números" width="260"> |
|  | <img src="docs/images/systemcompact-bars-en.png" alt="Monitor del sistema: anillos, barras o números" width="260"> |
|  | <img src="docs/images/systemcompact-numbers-en.png" alt="Monitor del sistema: anillos, barras o números" width="260"> |
| Volumen o brillo cambiando | <img src="docs/images/level-en.png" alt="Volume bar" width="260"> |

### La línea fina

La isla recogida es una línea fina por defecto (Ajustes, Comportamiento, "Línea fina siempre"). Muestra solo el estado más importante, dibujado en la propia línea; pasa el ratón para abrir la isla.

| Estado | Aspecto |
|---|---|
| En reposo | <img src="docs/images/thin-idle-en.png" alt="En reposo" width="300"> |
| Claude trabajando: un brillo recorre la línea (amarillo o rojo según se llena el límite) | <img src="docs/images/thin-claude-en.png" alt="Claude trabajando: un brillo recorre la línea (amarillo o rojo según se llena el límite)" width="300"> |
| Claude te espera: la línea pulsa | <img src="docs/images/thin-waiting-en.png" alt="Claude te espera: la línea pulsa" width="300"> |
| Música: un ecualizador pequeño del color de la portada | <img src="docs/images/thin-music-en.png" alt="Música: un ecualizador pequeño del color de la portada" width="300"> |
| Temporizador: la línea se vacía al acabarse el tiempo | <img src="docs/images/thin-timer-en.png" alt="Temporizador: la línea se vacía al acabarse el tiempo" width="300"> |
| Una descarga, una compilación o un juego de Steam: la línea se llena con el progreso | <img src="docs/images/thin-progress-en.png" alt="Una descarga, una compilación o un juego de Steam: la línea se llena con el progreso" width="300"> |
| Cargando: la línea se llena con la carga | <img src="docs/images/thin-charging-en.png" alt="Cargando: la línea se llena con la carga" width="300"> |
| Grabación de pantalla (OBS o Game Bar): una línea roja tenue con un punto parpadeante | <img src="docs/images/thin-recording-en.png" alt="Grabación de pantalla (OBS o Game Bar): una línea roja tenue con un punto parpadeante" width="300"> |
| Batería baja o un error: la línea pulsa en rojo | <img src="docs/images/thin-alert-en.png" alt="Batería baja o un error: la línea pulsa en rojo" width="300"> |
| Sin internet: una línea roja discontinua que parpadea | <img src="docs/images/thin-offline-en.png" alt="Sin internet: una línea roja discontinua que parpadea" width="300"> |
| Terminado: la línea se vuelve verde | <img src="docs/images/thin-done-en.png" alt="Terminado: la línea se vuelve verde" width="300"> |
| Enfoque: una lunita | <img src="docs/images/thin-focus-en.png" alt="Enfoque: una lunita" width="300"> |
| Monitor del sistema: dos anillos pequeños, procesador y memoria | <img src="docs/images/thin-gauges-en.png" alt="Monitor del sistema: dos anillos pequeños, procesador y memoria" width="300"> |
| Micrófono y cámara: puntos dentro de la línea | <img src="docs/images/thin-privacy-en.png" alt="Micrófono y cámara: puntos dentro de la línea" width="300"> |
| Una segunda actividad: una burbuja pequeña junto a la línea | <img src="docs/images/thin-bubble-en.png" alt="Una segunda actividad: una burbuja pequeña junto a la línea" width="300"> |
| Cambio de canción: la portada, el nombre y un símbolo | <img src="docs/images/thin-peek-en.png" alt="Cambio de canción: la portada, el nombre y un símbolo" width="300"> |
| Volumen o brillo: una barrita | <img src="docs/images/thin-level-en.png" alt="Volumen o brillo: una barrita" width="300"> |

### Primer uso y Ajustes

La primera vez que se ejecuta (y la primera vez tras actualizar a la 4.0), una breve bienvenida pregunta cómo debe verse la isla (línea fina en reposo, siempre línea fina o píldora) y qué debe mostrar (música, Claude, notificaciones de Windows, puntos de micrófono y cámara, permisos de Claude, iniciar con Windows). Los Ajustes tienen dos modos: **Esenciales**, con pocos interruptores, y **Avanzado**, con todos los ajustes en cuatro pestañas y un cuadro de búsqueda.

<img src="docs/images/welcome-1-en.png" alt="Welcome, step 1" width="200"> <img src="docs/images/welcome-2-en.png" alt="Welcome, step 2" width="200"> <img src="docs/images/welcome-3-en.png" alt="Welcome, step 3" width="200">

## Instalar

1. **Descarga** `WindowsIsland.exe` de la [última versión](https://github.com/leotrvs/windows-island/releases/latest). Windows puede mostrar un aviso de SmartScreen porque la app no tiene un certificado de firma de código de pago: elige **Más información** y luego **Ejecutar de todas formas**.
2. **Ábrelo** y elige **Sí**. Se instala solo para tu usuario de Windows, sin derechos de administrador, crea accesos directos en el menú Inicio y el Escritorio y se inicia con Windows.
3. La isla aparece en la parte superior de la pantalla, y su icono en la bandeja.

Para probarla sin instalar, ejecuta `WindowsIsland.exe --portable`.

## Uso

1. **Pasa el ratón** sobre la isla para expandirla, o **haz clic** en ella. Aparta el ratón y se contrae.
2. **Haz clic** en la barra de progreso para saltar a ese punto de la canción.
3. **Haz clic** en un aviso o notificación para descartarlo.
4. **Haz clic** en el icono de la bandeja, o abre el acceso directo de Windows Island mientras se ejecuta, para abrir **Settings**. Haz clic derecho en el icono para las acciones rápidas y **Salir**. Haz clic de nuevo en el icono de la bandeja para cerrar Settings.

<img src="docs/images/settings-en.png" alt="La ventana de ajustes" width="900">

| Opción | Qué hace |
|---|---|
| Show music | Muestra lo que está sonando |
| Show Claude | Muestra la actividad y los límites de Claude Code |
| Claude alerts | Expande la isla cuando Claude termina o te necesita |
| Show Codex, Codex alerts | Actividad y límites de Codex, y el aviso cuando termina. Solo aparecen mientras Codex está trabajando |
| Microphone and camera dots | Muestra un punto junto a la isla mientras una app usa el micrófono (naranja) o la cámara (verde) |
| Battery alerts | Cargador conectado o retirado, batería baja y carga completa. Solo en portátiles |
| Show Windows notifications | Muestra las notificaciones nuevas en la isla. Activado por defecto |
| Alert size | Avisos y notificaciones pequeños o normales |
| Screenshot alert | Una nota muy pequeña cuando haces una captura con Impr Pant o Win+Mayús+S |
| Usage ring color | Azul, verde o morado. Los anillos pasan a ámbar y rojo cerca del límite |
| Language | Sigue a Windows por defecto. Inglés, portugués, español y ruso |
| Empty island | Mantiene la isla contraída como una píldora simple, sin actividades. Pasa el ratón o haz clic para verlas |
| Show pill when idle | Mantiene la píldora en pantalla cuando no pasa nada |
| Expand on hover | Expande al dejar el ratón sobre la isla. Si está desactivado, haz clic |
| Hide in fullscreen | Oculta la isla en juegos, vídeos y presentaciones |
| Position | Parte superior de la pantalla, o justo encima de la barra de tareas |
| Monitor | El principal, uno concreto, todos o el que tiene el ratón |
| Collapsed size, Expanded size | De 80% a 130%, por separado para cada estado |
| Distance from the edge, Horizontal position | Aleja la isla del borde o la mueve hacia un lado |
| Auto-hide when idle | Oculta la isla cuando lleva un rato sin mostrar nada. Vuelve con una actividad nueva o al llevar el ratón a su lugar |
| Alert duration | Cuánto tiempo quedan en pantalla los avisos de Claude y las notificaciones |
| Hide paused music after | Cuánto tiempo queda en la isla una canción en pausa |
| Start with Windows | Abre la app al iniciar sesión en Windows |
| Open settings file | Abre `settings.json`, donde `OffsetY` define la distancia al borde |

La app está en inglés y portugués, por eso las opciones aparecen en inglés.

## Cómo se leen los datos

- **Actividad de Claude:** Claude Code guarda un archivo pequeño por cada sesión abierta en `~/.claude/sessions`. La isla lee solo su tipo, el ID del proceso, el estado, el nombre y la hora del estado. Funciona con la terminal y con la app de escritorio. La app de escritorio mantiene un chat como ocupado mientras pide un permiso, así que la isla también lee la transcripción del chat, solo nombres de herramientas y horas y nunca lo que se dijo, para saber que una herramienta espera tu respuesta.
- **Límites de Claude:** cada 2 minutos, y al abrir el panel de Claude, la isla ejecuta `claude -p /usage` en segundo plano, los mismos números que ves en Claude Code. No llama al modelo ni gasta tokens. Si [Claude Usage Taskbar](https://github.com/leotrvs/claude-usage-taskbar) está instalado y tiene números más recientes, la isla usa esos.
- **Codex:** Codex guarda cada sesión como un archivo en `~/.codex/sessions`. Cada 2 segundos la isla lee el final del más reciente y toma solo si la última tarea empezó o terminó y los porcentajes de límite que Codex registró. Se actualizan cada vez que usas Codex. Nunca lee el inicio de sesión de Codex.
- **Notificaciones:** la API oficial de Windows para leer notificaciones necesita un paquete firmado, así que la isla lee, solo lectura, la base de datos local donde Windows las guarda. Muestra solo las notificaciones que llegan mientras está abierta y no guarda nada. Windows sigue mostrando su propio aviso: desactiva los avisos de notificación de una app en la Configuración de Windows si solo quieres la isla.
- **Iconos de sitios:** con la extensión del navegador instalada y una pestaña que reproduce sonido, la isla descarga una vez el `apple-touch-icon.png` público del sitio (solo HTTPS, sin redirecciones, un PNG pequeño) para mostrar un icono nítido. El sitio ve una petición normal desde tu dirección IP. Las notificaciones de sitios de Chrome o Edge traen el icono del sitio como un archivo que el propio navegador guarda en su carpeta `Notification Resources`; la isla lee solo ese archivo.

Los detalles están en [SECURITY.md](SECURITY.md).

## Actualizaciones

El historial completo de cambios está en [CHANGELOG.md](CHANGELOG.md) (inglés) y [CHANGELOG.pt-BR.md](CHANGELOG.pt-BR.md) (portugués).

Cada hora la isla revisa la última versión en GitHub. Solo instala una actualización si su firma digital coincide con la clave incluida en la app y el hash del archivo coincide con la lista firmada, así que un archivo manipulado se rechaza. Se reinicia en la versión nueva cuando no estás usando la isla y muestra un aviso breve. Desactívalo con **Automatic updates** en el menú de la bandeja, donde **Check for updates** busca una actualización al momento.

## Desinstalar

Ve a **Configuración de Windows > Aplicaciones > Aplicaciones instaladas > Windows Island > Desinstalar**. Esto elimina la app, los accesos directos y la configuración.

## Compilar desde el código

Necesitas Windows 10 versión 2004 o posterior, o Windows 11, y el [SDK de .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0). El exe generado funciona sin tener .NET instalado.

```powershell
git clone https://github.com/leotrvs/windows-island.git
cd windows-island
pwsh -File scripts/build.ps1
.\bin\WindowsIsland.exe
```

Si quieres contribuir, lee [CONTRIBUTING.md](CONTRIBUTING.md).

## Próximos pasos

La lista completa de lo planeado está en [docs/ROADMAP.md](docs/ROADMAP.md).

## Licencia

[MIT](LICENSE). El nombre y el logo de Claude no están cubiertos.

La fuente Inter va incluida bajo la SIL Open Font License, ver [src/Assets/fonts/NOTICE.md](src/Assets/fonts/NOTICE.md).
