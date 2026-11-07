# Windows Island

[English](README.md) | [Português (Brasil)](README.pt-BR.md) | **Español** | [Русский](README.ru.md)

Una Dynamic Island para Windows. La música, Claude Code y sus límites de uso viven en una pequeña píldora en la parte superior de la pantalla, con el aspecto del iPhone y animaciones de resorte.

<p align="center">
<img src="docs/images/musicexpanded-en.png" alt="La isla expandida con la canción sonando" width="460">
</p>

> Proyecto no oficial de la comunidad. No está afiliado, respaldado ni mantenido por Apple ni por Anthropic. "Claude" es una marca registrada de Anthropic.

> **Versión preliminar.** Todavía no hay instalador. [Compílalo desde el código](#compilar-desde-el-código) para probarlo.

## Funciones

- **Música** de cualquier reproductor que aparezca en los controles multimedia de Windows, como Spotify o un navegador: carátula, un ecualizador con el color de la carátula, una barra de progreso en la que puedes hacer clic para avanzar, y reproducir, pausar, siguiente y anterior
- **Actividad en vivo de Claude Code:** cuánto tiempo lleva Claude trabajando y cuándo te está esperando
- **Uso de Claude:** anillos para los límites de **5 horas** y **semanal**, con la hora en que se renuevan
- **Avisos** cuando Claude termina una tarea o te necesita
- **Dos actividades a la vez:** la segunda se separa en una burbuja junto a la isla
- **Movimiento fluido:** la forma y el contenido cambian con resortes, nunca con transiciones de duración fija
- **Discreta:** nunca toma el foco, los clics a su alrededor llegan a la ventana de abajo, se reduce a una línea fina cuando hay una ventana maximizada delante y se oculta en pantalla completa
- Posición arriba o abajo, inicio con Windows, inglés y portugués de Brasil
- **Nunca lee tu inicio de sesión ni tu token de Claude** y no hace conexiones de red

## Estados

| Estado | Aspecto |
|---|---|
| En reposo | <img src="docs/images/idle-en.png" alt="Píldora en reposo" width="260"> |
| Música sonando, con Claude trabajando al lado | <img src="docs/images/musiccompact-en.png" alt="Música compacta con la burbuja de Claude" width="380"> |
| Claude trabajando, con el tiempo transcurrido | <img src="docs/images/claudecompact-en.png" alt="Actividad compacta de Claude" width="320"> |
| Claude terminó o te necesita | <img src="docs/images/banner-en.png" alt="Aviso de Claude" width="420"> |
| Claude expandido, con los límites | <img src="docs/images/claudeexpanded-en.png" alt="Claude expandido con los anillos de uso" width="460"> |

## Uso

1. **Pasa el ratón** sobre la isla para expandirla, o **haz clic** en ella. Aparta el ratón y se contrae.
2. **Haz clic** en la barra de progreso para saltar a ese punto de la canción.
3. **Haz clic derecho** en el icono de la bandeja para ver las opciones y **Salir**.

| Opción | Qué hace |
|---|---|
| Show music | Muestra lo que está sonando |
| Show Claude | Muestra la actividad y los límites de Claude Code |
| Claude alerts | Expande la isla cuando Claude termina o te necesita |
| Show pill when idle | Mantiene la píldora en pantalla cuando no pasa nada |
| Expand on hover | Expande al dejar el ratón sobre la isla. Si está desactivado, haz clic |
| Hide in fullscreen | Oculta la isla en juegos, vídeos y presentaciones |
| Position | Parte superior de la pantalla, o justo encima de la barra de tareas |
| Start with Windows | Abre la app al iniciar sesión en Windows |
| Open settings file | Abre `settings.json`, donde `OffsetY` define la distancia al borde |

La app está en inglés y portugués, por eso las opciones aparecen en inglés.

## Cómo se leen los datos de Claude

- **Actividad:** Claude Code guarda un archivo pequeño por cada sesión abierta en `~/.claude/sessions`. La isla lee solo su tipo, el ID del proceso, el estado, el nombre y la hora del estado. Funciona con la terminal y con la app de escritorio.
- **Límites:** la isla lee los porcentajes y las horas de renovación que guarda [Claude Usage Taskbar](https://github.com/leotrvs/claude-usage-taskbar). Sin él, el área de límites muestra una pista y todo lo demás sigue funcionando.

Los detalles están en [SECURITY.md](SECURITY.md).

## Compilar desde el código

Necesitas Windows 10 versión 2004 o posterior, o Windows 11, y el [SDK de .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
git clone https://github.com/leotrvs/windows-island.git
cd windows-island
pwsh -File scripts/build.ps1
.\bin\WindowsIsland.exe
```

Si quieres contribuir, lee [CONTRIBUTING.md](CONTRIBUTING.md).

## Próximos pasos

- Instalador y actualizaciones automáticas firmadas
- Leer los límites por sí misma, sin Claude Usage Taskbar
- Notificaciones de Windows en la isla
- Temporizadores, batería, y micrófono y cámara en uso
- Una forma de que scripts y otras apps muestren sus propias actividades en vivo
- Ventana de ajustes, temas y varios monitores
- Menos uso de memoria

## Licencia

[MIT](LICENSE)
