# Redragon S136 Software

App mínima y portable para ver la batería del ratón y el teclado inalámbricos del combo **Redragon S136** (receptor 2.4G `VID 320F / PID 50B8`).

Un solo `.exe` de ~24 KB. Sin instalar.

## Qué hace

- Ventana chica con la batería del ratón y del teclado.
- Icono en la bandeja de sistema: dos barras (izquierda ratón, derecha teclado) con color según carga.
- Clic derecho en el icono: muestra `Ratón: X%` y `Teclado: X%`.
- Opciones **Iniciar con Windows** e **Iniciar minimizado**.
- Lee la batería cada 60 s y al abrir el menú.

## Uso

1. Baja `S136Battery.exe` de [Releases](../../releases).
2. Ábrelo. Listo.

Cerrar la ventana la esconde en la bandeja. Para salir: clic derecho → **Salir**.

Requiere .NET Framework 4.x (ya viene con Windows 10 y 11).

## Compilar

```bat
build.cmd
```

Usa el `csc.exe` de .NET Framework que trae Windows. No hace falta Visual Studio.

## Protocolo

Sacado del software oficial (análisis estático). Paquetes de 64 bytes por la interfaz HID del fabricante (`UsagePage 0xFF1C`):

| Byte | Valor |
|---|---|
| 0 | `0x04` (report ID) |
| 1–2 | suma de bytes 3..63 (little endian) |
| 3 | comando |
| 4 | largo de datos |
| 32 | destino: `1` teclado, `2` ratón |

- `0xAA` → estado de enlace. Respuesta byte 8 = teclado activo (`0xFF`), byte 9 = ratón activo.
- `0x1A` (largo 6) → batería. Respuesta byte 8 = porcentaje.

Solo lee. No cambia nada del aparato.

## Aviso

Proyecto no oficial. No tiene relación con Redragon. Redragon es marca de su dueño.
