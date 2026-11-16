<div align="center">

# 🛒 StorePOS 2.0

**Punto de venta e inventario ligero para tiendas pequeñas — pensado para negocios donde la mercancía es de más de una persona.**

[![Release](https://img.shields.io/github/v/release/sillycatdevelopment/StorePOS2?label=versi%C3%B3n)](https://github.com/sillycatdevelopment/StorePOS2/releases/latest)
[![Build](https://img.shields.io/github/actions/workflow/status/sillycatdevelopment/StorePOS2/release.yml?label=build)](https://github.com/sillycatdevelopment/StorePOS2/actions)
[![License: MIT](https://img.shields.io/badge/licencia-MIT-blue.svg)](LICENSE)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6)

[**Descargar**](https://github.com/sillycatdevelopment/StorePOS2/releases/latest) · [Instalación](#-instalación) · [Funciones](#-funciones) · [Para desarrolladores](docs/DEVELOPMENT.md)

</div>

---

StorePOS funciona en una PC con Windows común — incluso una vieja con 4 GB de RAM — y se usa desde el
navegador de esa PC, de una tablet o del celular conectados al mismo Wi‑Fi. Sin internet obligatorio, sin
mensualidades y sin instalar servidores de base de datos. Todo en **español** y en **pesos mexicanos (MXN)**.

Lo que lo hace diferente: **la existencia de un mismo producto puede ser de varios dueños** (por ejemplo,
familiares que compran cada quien una parte de la mercancía). Cada venta guarda de quién era lo que se vendió
y a qué costo, así las ventas y la ganancia de cada dueño siempre quedan separadas y exactas.

## ✨ Funciones

| | |
|---|---|
| 🧾 **Punto de venta** | Lector de código de barras, búsqueda por nombre, venta por pieza o por kilo, descuentos, efectivo / tarjeta / transferencia, cambio y ticket impreso. |
| 👥 **Varios dueños** | El mismo producto con existencia de varios dueños; cada venta guarda de quién salió la mercancía, su costo y su ganancia. Traspasos entre dueños. |
| 📦 **Inventario** | Existencia por dueño, lotes (lo más antiguo se vende primero), mermas, conteos físicos, historial completo de movimientos y alertas de existencia baja. |
| 💵 **Caja y corte** | Fondo inicial, gastos, retiros y depósitos; efectivo esperado contra contado; las tarjetas nunca se cuentan como efectivo. Corte impreso en ticket. |
| 🚚 **Proveedores** | Días de visita de cada proveedor (Bimbo, Coca‑Cola…), próximas entregas y registro de lo que realmente llegó. |
| 📋 **Listas de surtido** | Sugerencias de compra según ventas, días a la próxima entrega y existencia mínima. Se imprimen, se exportan y se convierten en inventario al recibir. |
| 🙋 **Solicitudes de clientes** | Anota en segundos lo que piden y no tienes; las solicitudes repetidas se suman. |
| 📈 **Reportes** | Ventas y ganancias por día, semana, mes, año, dueño, categoría, proveedor y forma de pago; más y menos vendidos; tendencias y temporadas. |
| 📊 **Excel** | Ventas, ganancias, reporte por dueño, inventario, entregas, cortes, listas y solicitudes. |
| 🔒 **Usuarios y seguridad** | Cajero, Encargado y Administrador; bitácora de auditoría de cancelaciones, ajustes, precios y cortes. |
| 💾 **Respaldos** | Respaldos automáticos y manuales seguros mientras se vende, y restauración con un clic. |
| 🔄 **Actualizaciones** | Al abrirse revisa si hay una versión nueva y se actualiza sola, sin tocar tus datos. |

## 💻 Requisitos

- Windows 10 u 11 de 64 bits (funciona en equipos modestos: AMD A6 / 4 GB RAM / disco duro).
- Un navegador (Chrome o Edge).
- Opcional: lector de código de barras USB e impresora térmica de tickets de 80 mm.

No hace falta instalar .NET ni ningún otro programa.

## 🚀 Instalación

1. Descarga **`StorePOS-Windows.zip`** de la [última versión](https://github.com/sillycatdevelopment/StorePOS2/releases/latest).
2. Extráelo en **`C:\StorePOS`** (evita Escritorio y Descargas).
3. Abre **`Iniciar-StorePOS.bat`**. Si Windows muestra “Windows protegió su PC”, elige **Más información → Ejecutar de todas formas**.
   Si pregunta por el firewall, permite el acceso en **redes privadas**.
4. Se abre el navegador en `http://localhost:5000`: crea la cuenta de **administrador**, el nombre de la tienda y el primer dueño.

Crea un acceso directo: clic derecho en `Iniciar-StorePOS.bat` → **Enviar a → Escritorio**.
La ventana negra debe quedar abierta mientras usas el sistema.

### Usarlo desde el celular o una tablet

En la PC escribe `ipconfig` en una terminal y busca **Dirección IPv4** (por ejemplo `192.168.1.20`).
En el otro dispositivo, conectado al mismo Wi‑Fi, abre `http://192.168.1.20:5000`.

### Primeros pasos

1. **Productos** → registra tus productos (o escanea el código al crearlos).
2. **Inventario** o **Proveedores → Registrar entrega** → indica cuánto hay y de qué dueño es.
3. **Caja y corte** → abre la caja con el fondo inicial.
4. **Punto de venta** → ¡a vender! Al final del día haz el **corte de caja**.

## 🔄 Actualizaciones

Cada vez que abres `Iniciar-StorePOS.bat`, StorePOS revisa si hay una versión nueva en GitHub y la instala
automáticamente. **Tus datos no se tocan**: antes de actualizar se guarda una copia de la base de datos en
`respaldo_actualizacion\`. Si no hay internet, simplemente abre la versión instalada.
También puedes buscar actualizaciones cuando quieras con `Actualizar-StorePOS.bat`.

## 🗂️ Tus datos

Todo se guarda en un solo archivo: **`C:\StorePOS\data\storepos.db`**. Los respaldos están en
`data\backups\` (o en la carpeta que elijas en *Configuración → Respaldos*). Copia los respaldos a una USB o a
la nube de vez en cuando. **No borres la carpeta `data`.**

## ❓ Problemas comunes

| Problema | Solución |
|---|---|
| El celular dice “No se puede acceder al sitio” | Revisa que la PC y el celular estén en el mismo Wi‑Fi y permite el puerto 5000 en el Firewall de Windows (redes privadas). |
| “Sin permiso” en una sección | Tu usuario no tiene ese rol; un administrador puede cambiarlo en *Configuración → Usuarios*. |
| El ticket sale cortado | Usa una impresora de **80 mm**, márgenes “Ninguno” y sin encabezados/pies de página en el diálogo de impresión. |
| Olvidé la contraseña del administrador | Otro administrador puede asignarle una nueva en *Configuración → Usuarios*. |

## 🛠️ Para desarrolladores

ASP.NET Core 10 (Razor Pages) · SQLite + Entity Framework Core · ClosedXML · JavaScript sin frameworks.
Compilación, pruebas, arquitectura y cómo publicar versiones: **[docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)** y
**[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)**.

```
dotnet run --project src/StorePos.Web      # http://localhost:5000
dotnet test StorePos2.slnx                 # pruebas automáticas
git tag v2.0.4 && git push origin v2.0.4   # GitHub compila y publica la versión
```

## 📄 Licencia

StorePOS se distribuye bajo la [licencia MIT](LICENSE): puedes usarlo, modificarlo y compartirlo libremente,
conservando el aviso de copyright. Se ofrece “tal cual”, sin garantía.

Usa estos componentes de código abierto: [.NET y ASP.NET Core](https://github.com/dotnet/aspnetcore),
[Entity Framework Core](https://github.com/dotnet/efcore), [Microsoft.Data.Sqlite / SQLite](https://www.sqlite.org/)
y [ClosedXML](https://github.com/ClosedXML/ClosedXML) — licencias MIT y dominio público.

<div align="center"><sub>Hecho con ❤️ para las tienditas de la esquina · © 2026 sillycatdevelopment</sub></div>
