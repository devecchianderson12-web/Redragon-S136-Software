# Redragon S136 Software

App mínima y portable para el combo inalámbrico **Redragon S136** (receptor 2.4G `VID 320F / PID 50B8`): batería del ratón y del teclado, y control de luces RGB.

Un solo `.exe`. Sin instalar.

## Qué hace

- **Dispositivos:** tarjetas de Mouse y Teclado con su foto, batería y estado.
- **Iluminación** (clic en una tarjeta): modo, brillo, velocidad, color y multicolor. Se guarda en el aparato.
  - Teclado: 18 modos (Fijo, Respiración, Espectro, Ondulado, Ola, Nube, Serpentina, Reacción, Ondas, Travesía, Estrellas, Flores, Rodar, Caricatura, Lluvia, Escaneo, Cascada, Velocidad).
  - Ratón: Fijo, Respiración, Espectro, Ondulado, Pulso, Reacción, Apagado.
- **Bandeja:** clic derecho muestra `Mouse · 2.4G · 100% batería` y lo mismo para el teclado, más Dispositivos, Ajustes y Salir. Clic izquierdo abre la ventana.
- **Ajustes:** iniciar con Windows e iniciar minimizado.
- Lee la batería cada 60 s y al abrir el menú.

## Uso

1. Baja `S136Battery.exe` de [Releases](../../releases).
2. Ábrelo.

Cerrar la ventana la esconde en la bandeja. Para salir: clic derecho → **Salir**.

Requiere .NET Framework 4.x (ya viene con Windows 10 y 11).

## Compilar

```bat
build.cmd
```

Usa el `csc.exe` de .NET Framework que trae Windows. No hace falta Visual Studio. El icono se dibuja en vectores (`src/Art.cs`) y `tools/mkicon.cs` lo exporta a `.ico`.

## Protocolo

Sacado del software oficial (análisis estático) y probado en el aparato. Paquetes de 64 bytes por la interfaz HID del fabricante (`UsagePage 0xFF1C`):

| Byte | Valor |
|---|---|
| 0 | `0x04` (report ID) |
| 1–2 | suma de bytes 3..63 (little endian) |
| 3 | comando |
| 4 | largo de datos (máx. 24 con destino) |
| 5–6 | offset (little endian) |
| 7 | estado (`0xFF`/`0xFE` = error) |
| 8.. | datos |
| 32 | destino: `1` teclado, `2` ratón |

| Comando | Uso |
|---|---|
| `0xAA` | enlace: byte 8 teclado activo, byte 9 ratón activo (`0xFF`) |
| `0x1A` | batería: byte 8 = % |
| `0x05` | leer config |
| `0x01` / `0x06` / `0x02` | abrir, escribir config, cerrar |

Bloque de luces por perfil (byte 0 del bloque 0 = perfil activo). Hay que escribir el bloque entero desde su inicio:

| | Teclado (`0x63` bytes cada `0x64`) | Ratón (`0x4E` bytes cada `0x80`) |
|---|---|---|
| modo | 1 | 1 |
| brillo 0–4 | 2 | 2 |
| velocidad (4 − v) | 3 | 3 |
| multicolor | 5 | 5 |
| color | 6 R, 7 G, 8 B | 6 R, 7 B, 8 G |

En el ratón, el modo Fijo muestra el color elegido solo con multicolor apagado.

## Aviso

Proyecto no oficial. No tiene relación con Redragon. Redragon es marca de su dueño. Las fotos de los aparatos vienen del software oficial.
