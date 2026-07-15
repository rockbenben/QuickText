<p align="left">
  <img src="../assets/branding/quicktext-256.png" width="72" alt="QuickText">
</p>

[English](../README.md) · [简体中文](../README.zh.md) · [繁體中文](README.zh-Hant.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · **Español** · [Português](README.pt.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Русский](README.ru.md) · [Tiếng Việt](README.vi.md) · [ไทย](README.th.md) · [Bahasa Indonesia](README.id.md) · [हिन्दी](README.hi.md) · [বাংলা](README.bn.md) · [العربية](README.ar.md) · [Türkçe](README.tr.md)

# QuickText

> Parte del **[Plan 365 de código abierto](https://github.com/rockbenben/365opensource)** — proyecto n.º 023 · un gestor de fragmentos y expansor de texto en la bandeja de Windows.

**Deja de escribir lo mismo dos veces.** QuickText vive en la bandeja de Windows: guarda una vez el texto al que recurres una y otra vez —correo, direcciones, firmas, plantillas, respuestas predefinidas, imágenes— y luego, en **cualquier campo de texto**, escribe unas pocas teclas o una abreviatura y aparece justo en el cursor. Varias líneas, caracteres especiales y emoji preservados carácter por carácter.

> Tu texto reutilizable, a unas pocas teclas de distancia: colocado directamente en el cursor.

- WPF / .NET 10, exe portátil de un solo archivo, **sin cuenta, sin conexión por defecto**: solo la comprobación de actualizaciones opcional contacta con GitHub.
- Los datos son **JSON local** en tu propia carpeta: ponlos en Dropbox / OneDrive / un NAS para sincronizarlos.
- Tema oscuro, **18 idiomas de interfaz** (con reflejo de derecha a izquierda para el árabe), los ajustes se aplican al instante.

---

## Dónde lo usarías

Allí donde **escribes lo mismo una y otra vez en Windows**. Vive en la bandeja y funciona en todos los campos de texto (ventanas de chat, formularios del navegador, editores, clientes de correo, sin atarse a una sola aplicación). Es un **gestor de fragmentos y expansor de texto en uno**, con búsqueda por pinyin, plantillas con variables e imágenes.

| Quién | Qué guardan |
|---|---|
| **Soporte / comercio electrónico** | Respuestas predefinidas, respuestas estándar, textos promocionales, códigos QR o fotos de productos |
| **Ventas / negocios** | Plantillas de correo, saludos de apertura, presupuestos, despedidas |
| **Desarrolladores / operaciones** | Comandos, configuración, JSON, código base (`{...}` se emite literalmente, nunca se interpreta) |
| **Oficina / rellenar formularios** | Correo, dirección, teléfono, números de identificación, plantillas de actas de reunión (te preguntan y recuerdan el último valor) |
| **RR. HH. / administración / legal** | Avisos de incorporación, notificaciones estándar, avisos legales: local, sin conexión, apto para contenido sensible |

## Míralo en 30 segundos

En **cualquier lugar donde puedas escribir**: por ejemplo, necesitas tu correo en un cuadro de chat:

1. **Invoca** — pulsa `Ctrl+Shift+8`; el panel aparece sobre la ventana activa.
2. **Busca** — escribe unas pocas letras; las coincidencias se resaltan. Busca por nombre, pinyin (completo + iniciales), abreviatura y cuerpo.
3. **Enter** — el correo se pega **justo donde estaba tu cursor**. Listo.

> No escribas nada para navegar por categoría, con `Recientes` / `Favoritos` fijados arriba; los que más usas **suben solos** de forma automática.
> `↑↓` mover · `←→` cambiar de categoría · `Alt+1–9` selección rápida · doble clic para enviar · `Esc` para cerrar.

## Tres formas de acceder: elige la que te venga bien

La misma biblioteca, tres maneras de sacar de ella; combínalas libremente:

| Forma | Cómo se activa | Ideal para |
|---|---|---|
| 🔍 **Búsqueda en el panel** | `Ctrl+Shift+8` → escribe → Enter | Muchos fragmentos, uso ocasional, elegir navegando |
| ⌨️ **Abreviatura en línea** | Escribe `;sig` y luego Espacio / Tab / Enter | Frases fijas de alta frecuencia, sin necesidad del panel |
| 🧩 **Plantilla con variables** | Saca un fragmento con `{variables}` por cualquiera de las anteriores | Correo / formularios: una plantilla, unas pocas palabras cambiadas |

- **Imágenes también**: añádelas desde el portapapeles o un archivo, se pegan como imagen al enviar; las imágenes pueden tener abreviaturas, así que escribe la abreviatura y obtén la imagen (códigos QR, firmas con logo).
- **Salida por fragmento**: las frases de chat se envían solas tras pegar, los fragmentos de código nunca lo hacen; no se interfieren.

Todos los detalles sobre abreviaturas y variables están en **En detalle**, más abajo: las secciones **Marcadores de posición** y **Abreviaturas**.

---

# En detalle

## Funciona nada más instalar

Haz doble clic en **`QuickText.exe`**; vive en la **bandeja del sistema** (sin botón en la barra de tareas). En el **primer arranque**:

- coloca una pequeña **biblioteca de ejemplo** (dos categorías, en tu **idioma de interfaz**) para que puedas probarla de inmediato;
- muestra un globo con la tecla de invocación, por defecto **`Ctrl+Shift+8`**.

> Los iconos de la esquina superior derecha del panel: **＋ Nuevo** · **Gestor** (abre el editor) · **Ajustes** · **📌 Fijar** (mantén el panel abierto tras un envío, para lanzar varios seguidos). Salta directamente al Gestor / Ajustes sin volver a la bandeja.

## Añadir / editar tu texto

- **Bandeja → Abrir Gestor** — el editor completo: categorías a la izquierda, fragmentos a la derecha, editor debajo. Añade/renombra/elimina categorías (con una etiqueta de **7 colores**), edita fragmentos (nombre, abreviatura, cuerpo, imagen). Arrastra para reordenar o mover entre categorías; `Ctrl+Z` deshace un borrado.
- **Bandeja → Nuevo desde el portapapeles** — crea un fragmento nuevo a partir del portapapeles actual y ábrelo en el Gestor para completarlo (se guarda al guardar/cerrar el Gestor).
- **Crear en el panel** — escribe el texto en el cuadro de búsqueda y pulsa `Ctrl+N` para guardarlo como un fragmento nuevo (ese texto pasa a ser el cuerpo; `@categoría …` lo archiva en esa categoría) y saltar al Gestor a completarlo (`Ctrl+E` edita el seleccionado).

## Marcadores de posición (una plantilla, muchas situaciones) · opcional por fragmento

Los marcadores de posición son un **interruptor por fragmento**: marca "**Activar marcadores {variable}**" en el editor del Gestor y los tokens de abajo se resuelven al enviar; **sin marcar (por defecto), el cuerpo se envía literalmente** —el código, los scripts y el JSON llenos de `{...}` literales nunca se interpretan mal ni piden datos.

> Al actualizar: los marcadores solían estar siempre activos. El primer arranque tras actualizar marca automáticamente el interruptor de los **fragmentos existentes** cuyo cuerpo contiene `{...}`, para que nada cambie de comportamiento; desmárcalo en el Gestor para los que en realidad son código.

Cuando están activados, estos marcadores se resuelven al enviar:

| Marcador | Qué hace |
|---|---|
| `{name}` (cualquier etiqueta) | **Te pide que lo rellenes** antes de pegar; **recuerda tu último valor** para que las repeticiones no vuelvan a preguntar |
| `{name:John}` | Variable con un **valor por defecto**, precargado en el cuadro |
| `{env\|dev\|test\|prod}` | Variable con **opciones**: el cuadro muestra un desplegable (la primera opción hace también de valor por defecto; se admite escritura libre) |
| `{clipboard}` | Inserta el contenido actual del portapapeles |
| `{cursor}` | Deja el cursor en este punto tras pegar (también suprime el Enter automático) |
| `{date}` `{time}` `{datetime}` | Inserta la fecha/hora actual; admite desplazamientos como `{date+7}` (7 días después). Los alias chinos `{日期}` / `{时间}` / `{日期时间}` también funcionan |
| `{uuid}` `{random}` | Valores aleatorios: un UUID / 6 dígitos, nuevos en cada aparición |
| `{snippet:name}` | **Inserta el cuerpo de otro fragmento** (3 niveles de profundidad, a prueba de ciclos): mantén las firmas compartidas en un solo sitio |

Ejemplo: una firma `Best regards,\n{name}` (con los marcadores activados) → pide el nombre al enviar → pega la firma completa.

## Abreviaturas (expandir mientras escribes, sin panel) · opcional

Dale a un fragmento una **abreviatura** para expandirlo directamente en cualquier campo de texto. El **prefijo de activación** se define una vez en Ajustes (por defecto `;`):

1. En el Gestor, escribe solo la **abreviatura en sí**, p. ej. `sig` para tu firma. El prefijo se añade automáticamente (no lo vuelvas a escribir); el campo muestra el prefijo actual `;` delante.
2. En cualquier campo de texto escribe **`;sig`** (= prefijo + abreviatura) y luego **Espacio / Tab / Enter** → borra `;sig` y expande el cuerpo (un `{marcador}` pide datos primero cuando ese fragmento tiene los marcadores activados).
3. **¿Te has equivocado?** Pulsa **Retroceso justo después** de la expansión para revertirla a `;sig` (misma ventana, en 5 s).

Detalles: la coincidencia **no distingue mayúsculas** (`;SIG` se activa con Bloq Mayús); una errata corregida con Retroceso aún se expande; dos fragmentos que comparten una abreviatura reciben un **aviso en línea** en el Gestor; y el menú de la bandeja tiene un interruptor de un clic **"Pausar expansión"** (para demostraciones/juegos).

> **Sobre el prefijo:** el prefijo (por defecto `;`) evita que la escritura normal / el pinyin activen por error una abreviatura desnuda. Cámbialo en **Ajustes → Abreviaturas → Prefijo de activación** (a `,`, `:`, …) o **déjalo vacío**. Una abreviatura con prefijo se autodelimita y se activa incluso pegada al texto anterior (p. ej. `thx;sig`); sin prefijo, solo se activa como palabra propia (nunca como el final de una más larga como `graf`). También puedes desactivar las abreviaturas por aplicación (una lista negra, p. ej. `cmd.exe; putty.exe`) o desactivarlas por completo.

## Salida y ajustes comunes (Bandeja → Ajustes)

- **Salida** — por defecto "pegar en la aplicación activa"; o "copiar solo al portapapeles" (pegas tú con `Ctrl+V`). Opcional: pulsar Enter tras pegar, un solo clic para enviar, restaurar el portapapeles. **Cada fragmento puede anular esto** (Gestor → Salida: seguir global / pegar / pegar + Enter / solo copiar): las frases de chat se envían solas, los fragmentos de código nunca.
- **Posición del panel** — seguir la ventana activa (por defecto) / seguir el cursor de texto / recordar la última posición.
- **Método de invocación (elige uno)** — ① **combinación de teclas**: haz clic en el cuadro y pulsa una nueva (las teclas normales necesitan `Ctrl`/`Alt`/`Shift`/`Win`; las teclas de función **`F1`–`F24` funcionan solas**); o ② **pulsar un modificador**: **una o dos pulsaciones de un modificador** (p. ej. `Ctrl` derecho, `Shift` derecho) para invocar (un modificador solo no puede ser una tecla rápida normal, así que se detecta por pulsación). Elegir la pulsación desactiva la combinación: las dos son mutuamente excluyentes, así que siempre queda claro cuál está activa.
- **Tecla de captura** — segunda combinación opcional que **guarda en silencio el portapapeles como un fragmento nuevo** (aviso con globo, sin ventana).
- **Carpeta de datos** — apúntala a una unidad de sincronización; **exportar / importar copia de seguridad** (zip, validada con confirmación de sobrescritura); **copia automática** diaria a esta máquina (se conservan las 10 más recientes, acceso a la carpeta con un clic).
- **Idioma** — **18 idiomas**: English · 简体中文 · 繁體中文 · 日本語 · 한국어 · Español · Português · Français · Deutsch · Italiano · Русский · Tiếng Việt · ไทย · Bahasa Indonesia · हिन्दी · বাংলা · العربية (RTL) · Türkçe, con cambio instantáneo. **Iniciar con Windows** opcional.
- **Buscar actualizaciones** — desactivado por defecto; si se activa, contacta con GitHub una vez al iniciar para comprobar si hay una versión nueva (la única vez que usa Internet). «Buscar ahora» lo ejecuta al momento.

## Chuleta de teclado

| Acción | Teclas |
|---|---|
| Invocar / cerrar panel | `Ctrl+Shift+8` (configurable) / `Esc` |
| Seleccionar / cambiar de categoría / selección rápida | `↑↓` / `←→` / `Alt+1–9` |
| Enviar | `Enter` o doble clic (un solo clic opcional) |
| Marcar / desmarcar favorito | `Ctrl+D` |
| Crear / editar en el panel | `Ctrl+N` / `Ctrl+E` |
| Deshacer borrado (Gestor) | `Ctrl+Z` |
| Abreviatura: activar / deshacer | escribe la abreviatura + Espacio·Tab·Enter / Retroceso tras expandir |

---

## Funciones de un vistazo

- **Invocación**: tecla rápida global — una **combinación de teclas** (las teclas de función funcionan como tecla única) o **una/dos pulsaciones de un modificador** (p. ej. `Ctrl` derecho), **elige una**; el panel sigue la ventana activa / el cursor de texto / la posición recordada; los botones de la fila superior saltan a **Nuevo / Gestor / Ajustes**; fíjalo para enviar varios seguidos; arrastra y redimensiona con tamaño recordado.
- **Búsqueda**: nombre / pinyin / iniciales / abreviatura / cuerpo, resaltados; **empates resueltos por frecuencia de uso (frecency)**; `@categoría palabras clave` limita la búsqueda a una categoría (`@categoría` a secas la navega).
- **Contenido**: texto plano (varias líneas, caracteres especiales, emoji sin pérdidas), marcadores de posición (valores por defecto / desplegables de opciones / anidamiento de fragmentos / uuid / aleatorio, **opcional por fragmento**), **imágenes** (desde el portapapeles o un archivo, pegadas como imagen al enviar; **las imágenes también pueden tener abreviaturas**: escribe la abreviatura, obtén la imagen).
- **Abreviaturas**: activadas por terminador, cuadro de variables, deshacer con una pulsación, corrección de erratas con Retroceso, sin distinción de mayúsculas, el clic rompe el token, aviso de duplicados, lista negra por aplicación, **pausa desde la bandeja con un clic**.
- **Salida**: pegar directamente / solo copiar; Enter automático opcional, restaurar portapapeles, envío con un solo clic; **anulación de salida por fragmento**; tecla de captura (portapapeles → fragmento con una pulsación).
- **Gestor**: 7 colores de categoría, reordenar / mover arrastrando, **mover / eliminar por lotes con selección múltiple** (selecciona con Ctrl / Shift y luego clic derecho), deshacer borrado, **papelera (restauración de 30 días, con vista previa del cuerpo)**, aviso de abreviatura duplicada, estadísticas de uso, modo sin ajuste de línea para código, confirmación de guardado.
- **Datos**: JSON local, recarga en caliente (fusiona automáticamente las ediciones externas / la sincronización), aviso de conflicto de sincronización, exportar / importar copia de seguridad, **copia automática diaria (se conservan 10)**, iniciar con Windows.
- **Localización**: **18 idiomas de interfaz** (chino simplificado / tradicional, English, 日本語, 한국어, Español, Français, Deutsch, Русский, العربية …) con **reflejo de derecha a izquierda para el árabe**, con cambio en vivo desde Ajustes.
- **Robustez**: instancia única (un segundo arranque invoca el panel de búsqueda en lugar de instalar los ganchos por duplicado); la CI ejecuta las pruebas más una comprobación de humo de ventana en cada push y publica un exe de un solo archivo en las etiquetas `v*`.

## Datos y sincronización

Carpeta de datos (por defecto `Documents\QuickText`, cambiable en Ajustes, puede apuntar a una unidad de sincronización):

```
<carpeta de datos>/
  ├─ index.json        # orden de categorías + nombre de archivo y color de cada categoría
  ├─ <categoría>.json  # los fragmentos de esa categoría (Snippet[])
  ├─ trash.json        # fragmentos eliminados (borrado suave, purgados tras 30 días)
  └─ images/           # archivos de imagen de los fragmentos con imagen
```

- `Snippet`: `{ id, name, abbr, body, useVariables, outputMode, imagePath?, updatedAt }`.
- **Escrituras atómicas** (`*.tmp` → `File.Replace`) para que una sincronización nunca lea un archivo a medio escribir; recarga en caliente con `FileSystemWatcher` (agrupada), con una protección contra la propia escritura.
- El estado propio de la máquina se mantiene **fuera de la carpeta de sincronización**: los ajustes en `%APPDATA%\QuickText\settings.json`, los recuentos de uso / favoritos en `%APPDATA%\QuickText\usage.stats` (cambian en cada envío y entrarían en conflicto entre máquinas), las copias automáticas diarias en `%APPDATA%\QuickText\backups\`.
- **Modo portátil** (sin rastro / USB): actívalo en **Ajustes → Datos → Modo portátil** — coloca un marcador `QuickText.portable` junto a `QuickText.exe` y **se aplica en el siguiente reinicio** (el primer arranque portátil lleva consigo tus ajustes y tu uso, así que no reconfiguras). Los ajustes, el uso, las copias de seguridad y la biblioteca por defecto pasan entonces a estar bajo `<carpeta del exe>\Data\` en lugar de `%APPDATA%` / Documents, y "iniciar con Windows" usa un acceso directo en la carpeta de Inicio en vez del registro, de modo que toda la herramienta viaja en una memoria USB sin dejar nada en el equipo anfitrión. La aplicación debe estar en una ubicación con permiso de escritura (una memoria USB, no `Program Files`); la **biblioteca de texto se mueve con Exportar / Importar copia de seguridad**. El inicio con Windows se registra por modo, así que vuelve a marcarlo en el nuevo modo tras cambiar si lo quieres. Déjalo desactivado para la disposición instalada de arriba (la opción correcta cuando la carpeta de datos es una unidad de sincronización).<br>*(Tiene que ser un archivo marcador, no un ajuste normal: decide dónde vive `settings.json` mismo; el interruptor solo surte efecto en el siguiente arranque, sin alterar nunca la sesión actual.)*

## Marca

Los recursos viven en `assets/branding/`: `quicktext-mark.svg` (principal), `quicktext-mark-mono.svg` (monocromo), `quicktext.ico` (16–256, icono de app / bandeja), `quicktext-256.png`, `quicktext-social.png` (og:image de 1200×630), `quicktext-social-2x.png` (2400×1260 @2x), `brand.html` (hoja de marca). El símbolo es un **cursor de inserción de texto (I-beam)** sobre un mosaico menta junto a un bloque ámbar de texto recién colocado —"text▮"—, es decir, colocar tu texto en el cursor. Paleta "terminal dusk": menta (el color de acento de la app `#3DC2A0`) + ámbar `#F2B457` sobre tinta con matiz azul verdoso; un logotipo de palo seco combinado con la monoespaciada Cascadia Code.

## Arquitectura

Core puro (sin Win32, con pruebas unitarias) mantenido separado de Win32/UI.

| Proyecto | Contenido |
|---|---|
| `src/QuickText.Core` | `Models`, `Persistence` (`Store`, `UsageStore`, `JsonConfig`), `Search` (`SearchIndex`), `Abbr` (`AbbrMatcher`), `Snippets` (`Placeholders`), `Pinyin`, `Settings`, `Localization` (.resx, 18 idiomas) |
| `src/QuickText.App` | Interfaz WPF (`SearchPanel` / `ManagerWindow` / `SettingsWindow` / `AppDialog` / `VariablesDialog`), `Ui/Theme.xaml` (tema oscuro), `Interop` (`GlobalHotkey`, `KeyboardHook`, `PasteEngine`, `Autostart`, `NativeMethods`) |
| `tests/QuickText.Core.Tests` | Pruebas unitarias de Core (xUnit) |

## Compilar y ejecutar

```bash
dotnet build QuickText.sln -c Debug
dotnet test  tests/QuickText.Core.Tests/QuickText.Core.Tests.csproj
dotnet run  --project src/QuickText.App        # o ejecuta QuickText.exe en bin
```

Publica una versión portátil de un solo archivo (win-x64):

```bash
dotnet publish src/QuickText.App -c Release -p:PublishProfile=win-x64
```

Requiere el SDK de .NET 10. Solo Windows (tecla rápida global de Win32 / gancho de teclado / portapapeles).

## Acerca del Plan 365 de código abierto

Este es el proyecto n.º 023 del [Plan 365 de código abierto](https://github.com/rockbenben/365opensource).

Una persona + IA, más de 300 proyectos de código abierto en un año. [Envía tu idea →](https://my.feishu.cn/share/base/form/shrcnI6y7rrmlSjbzkYXh6sjmzb)

## Licencia

[MIT License](../LICENSE) · Copyright © 2026 rockbenben. Libre de usar, modificar y distribuir.
