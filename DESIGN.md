# Sistema de Diseño Industrial de Precisión Técnica — SIGIC v2.2

Especificación visual, ergonómica y arquitectónica de interfaz para **SIGIC Ferretería (Propuesta Técnica Oficial v2.2)**. Este sistema adapta la estética de precisión técnica de **Linear** (`#08090a`, `#0f1011`, `#161718`, `#23252a`) combinada con el acento industrial de alta visibilidad (`#e4f222`), diseñada específicamente para retail ferretero de alta densidad informativa, operación continua en mostrador sin fatiga visual y comercio electrónico B2C/B2B.

---

## 1. Filosofía Visual y Principios de Ingeniería UI

1. **Densidad Informativa sin Ruido:** En ferretería técnica se operan miles de SKUs donde un carácter cambia el producto (ej. rosca corriente `UNC` vs. rosca fina `UNF`, grado `SAE 5` vs. `SAE 8`). La interfaz elimina sombras difusas, gradientes decorativos y espaciados excesivos, priorizando bordes estructurales de `1px` (`#23252a`) y jerarquía tipográfica estricta.
2. **Contraste Funcional para Entornos Industriales:** El lienzo oscuro profundo (`#08090a` y `#0f1011`) evita el deslumbramiento bajo iluminación fluorescente o LED de almacenes y mostradores. El color de acento `#e4f222` (Amarillo-Lima Industrial) se reserva **exclusivamente** para el foco activo de teclado, confirmación de lectura de escáner láser, totales de liquidación e indicadores primarios de acción.
3. **Alineación Numérica Determinista:** Todo código SKU, medida dimensional, conteo de inventario (`Stock Físico`, `Stock Comprometido`, `Stock Disponible`) y monto monetario utiliza obligatoriamente fuentes monoespaciadas con `font-variant-numeric: tabular-nums`.

---

## 2. Paleta de Colores y Tokens CSS / Tailwind CSS v4

El siguiente bloque define la configuración oficial de tokens de diseño para el archivo CSS global (`app.css` / `globals.css`) compatible con **Tailwind CSS v4** tanto en el SPA React 18 (Mostrador POS) como en Next.js (Portal Web):

```css
@import "tailwindcss";

@theme {
  /* 1. Superficies de Precisión Técnica (Linear Dark Industrial) */
  --color-sigic-canvas: #08090a;          /* Lienzo raíz de la aplicación (Fondo global) */
  --color-sigic-surface-1: #0f1011;       /* Paneles primarios, contenedor de grilla POS y tarjetas Web */
  --color-sigic-surface-2: #161718;       /* Cabeceras de tabla, inputs, modales y filas pares */
  --color-sigic-surface-hover: #1c1e22;   /* Estado hover en Web o preselección secundaria */
  --color-sigic-border: #23252a;          /* Borde estructural principal de 1px */
  --color-sigic-border-subtle: #18191c;   /* Divisores horizontales internos en tablas densas */

  /* 2. Acento Industrial de Alta Visibilidad */
  --color-sigic-accent: #e4f222;          /* Anillo de foco por teclado, totales y CTA primario */
  --color-sigic-accent-hover: #d3e015;    /* Variación activa del acento primario */
  --color-sigic-accent-muted: rgba(228, 242, 34, 0.14); /* Fondo de fila activa en navegación con flechas */
  --color-sigic-accent-border: rgba(228, 242, 34, 0.45); /* Borde suave para paneles con foco */
  --color-sigic-accent-fg: #08090a;       /* Texto de ultra-contraste sobre fondo #e4f222 */

  /* 3. Jerarquía Tipográfica */
  --color-sigic-text-primary: #f7f8f8;    /* Descripciones técnicas, nombres de producto y montos */
  --color-sigic-text-secondary: #8a8f98;  /* Metadatos, marca, ubicación de anaquel y etiquetas */
  --color-sigic-text-muted: #5e636e;      /* Atajos inactivos, placeholders y líneas sin stock */

  /* 4. Semáforo Semántico de Inventario (RNF-CON-003: Disponible = Físico - Comprometido) */
  --color-sigic-stock-ok: #22c55e;        /* Stock Disponible > Stock Mínimo */
  --color-sigic-stock-ok-bg: rgba(34, 197, 94, 0.12);
  --color-sigic-stock-warn: #f59e0b;      /* Stock Disponible crítico o reserva temporal activa */
  --color-sigic-stock-warn-bg: rgba(245, 158, 11, 0.14);
  --color-sigic-stock-danger: #ef4444;    /* Stock Disponible <= 0 (Bloqueo de venta/reserva) */
  --color-sigic-stock-danger-bg: rgba(239, 68, 68, 0.14);
  --color-sigic-info: #38bdf8;            /* Indicadores de Guía Interna de Traslado (RF-INV-003) */

  /* 5. Familias Tipográficas */
  --font-sans: "Inter", -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
  --font-mono: "JetBrains Mono", "Fira Code", "Cascadia Code", monospace;
}

/* Reglas Base de Comportamiento Ergonómico */
@layer base {
  /* Contención estricta para terminal de mostrador 1024x768 (RNF-USA-002) */
  body.sigic-pos-viewport {
    background-color: var(--color-sigic-canvas);
    color: var(--color-sigic-text-primary);
    font-family: var(--font-sans);
    width: 100vw;
    height: 100vh;
    max-width: 1024px;
    max-height: 768px;
    overflow: hidden; /* Prohibido el scroll horizontal y el scroll global de página */
    user-select: none;
    -webkit-font-smoothing: antialiased;
  }

  /* Utilidad obligatoria para datos numéricos, SKUs, medidas y precios */
  .sigic-tabular {
    font-family: var(--font-mono);
    font-variant-numeric: tabular-nums;
    letter-spacing: -0.02em;
  }

  /* Foco de teclado obligatorio e inconfundible para operación sin ratón (RF-POS-003) */
  *:focus-visible {
    outline: 2px solid var(--color-sigic-accent) !important;
    outline-offset: -1px !important;
    background-color: var(--color-sigic-accent-muted);
  }
}

/* Utilidades de truncamiento técnico controlado para pernería y herramientas */
@layer utilities {
  .sigic-hardware-desc {
    display: -webkit-box;
    -webkit-line-clamp: 2;
    -webkit-box-orient: vertical;
    overflow: hidden;
    line-height: 1.15rem;
    font-size: 0.8125rem; /* 13px */
    color: var(--color-sigic-text-primary);
    word-break: break-word;
  }

  .sigic-dimension-badge {
    flex-shrink: 0;
    font-family: var(--font-mono);
    font-variant-numeric: tabular-nums;
    font-size: 0.75rem; /* 12px */
    font-weight: 600;
    padding: 0.125rem 0.375rem;
    background-color: var(--color-sigic-surface-2);
    border: 1px solid var(--color-sigic-border);
    color: var(--color-sigic-accent);
    border-radius: 0.25rem;
    white-space: nowrap;
  }
}
```

---

## 3. Ergonomía del Mostrador POS en `1024x768` (`RNF-USA-002` y `RF-POS-003`)

### 3.1 Presupuesto Exacto de Píxeles en Viewport `1024px × 768px`
Para garantizar **cero scroll horizontal (`overflow-x: hidden`)** y visibilidad instantánea de toda la transacción en monitores estándar de punto de venta de `1024x768`:

| Zona Estructural | Ancho × Alto Exacto | Contenido y Comportamiento |
| :--- | :--- | :--- |
| **A. Barra de Telemetría y Turno** | `1024px × 36px` | Indicador de conexión a PgBouncer (`POS-PRIORITY-POOL`), sucursal activa, cajero, estado de lectora láser y hora del servidor en `--font-mono`. |
| **B. Barra Omnicanal de Escaneo (`F1`)** | `1024px × 52px` | Input principal de código de barras / búsqueda rápida (`width: 620px`) con borde activo `#e4f222` + tarjeta compacta de Cliente/RUC (`F3`, `width: 404px`). |
| **C. Área Central de Trabajo** | `1024px × 560px` | Dividida en 2 columnas fijas sin desbordamiento:<br>• **Columna Izquierda (`714px × 560px`):** Tabla de ticket actual con scroll vertical interno exclusivo por teclado (`↑`/`↓`/`PgUp`/`PgDn`).<br>• **Columna Derecha (`310px × 560px`):** Inspector Técnico del ítem seleccionado y panel de stock multi-almacén. |
| **D. Zócalo de Liquidación y Comandos** | `1024px × 120px` | Fila superior (`64px`): Resumen tributario (Op. Gravada, Descuento, IGV 18%, **TOTAL A PAGAR** en `28px bold #e4f222`).<br>Fila inferior (`56px`): Grilla de 12 bloques físicos visuales para los atajos `F1` a `F12` + `Enter` y `Esc`. |

### 3.2 Distribución de Columnas en la Tabla de Facturación POS (`Ancho total: 714px`)
Cada fila del carrito tiene una altura máxima fija de `44px` con borde inferior `1px solid #18191c`:

1. **`#` (Índice):** `34px` (`--font-mono`, `#5e636e`, alineado al centro).
2. **`SKU / Código`:** `96px` (`--font-mono`, `#8a8f98`, alineado a la izquierda).
3. **`Descripción Técnica y Medida`:** `334px` (Aplica `.sigic-hardware-desc` de hasta 2 líneas + `.sigic-dimension-badge` con `flex-shrink: 0`).
4. **`Cant. / UM`:** `76px` (`--font-mono`, editable con `F4`, borde `#e4f222` al editar, alineado a la derecha).
5. **`P. Unit (S/)`:** `84px` (`--font-mono tabular-nums`, `#f7f8f8`, alineado a la derecha).
6. **`Subtotal (S/)`:** `90px` (`--font-mono tabular-nums font-semibold`, `#e4f222` en la fila activa, alineado a la derecha).

### 3.3 Regla Anti-Truncamiento para Tornillería, Pernería y Tuberías
Los nombres ferreteros colocan la especificación crítica al final del texto (ej. `TORNILLO AUTOPERFORANTE CABEZA HEXAGONAL CON ARANDELA DE NEOPRENO ZINCADO #12 X 2-1/2"`).
- **Prohibición:** Está prohibido usar `truncate` (`white-space: nowrap; text-overflow: ellipsis`) de una sola línea sobre el nombre completo.
- **Implementación Obligatoria en Componentes POS:**
  - El componente de celda debe dividir o renderizar un contenedor `flex items-start justify-between gap-2`.
  - A la izquierda, el texto descriptivo con `line-clamp-2` (`max-height: 2.3rem`).
  - A la derecha, el badge inmutable (`.sigic-dimension-badge` con `shrink-0`) mostrando la dimensión crítica (`#12 × 2-1/2"`), asegurando que el cajero y el despachador lean siempre el calibre y largo exactos.

### 3.4 Mapa Oficial de Navegación y Operación Híbrida 100% Teclado y 100% Ratón (`RF-POS-003`)
La terminal POS es **operable 100% por teclado y 100% por ratón**. Un listener global (`window.addEventListener('keydown', ...)`) y los controles visuales interactivos en pantalla garantizan que cualquier acción se ejecute indistintamente mediante atajos físicos o interacción táctil/ratón:
- **Botones de Acción Táctiles y de Ratón:** Cada bloque de comando del zócalo inferior posee dimensiones mínimas de **$44 \times 44\text{ px}$** (al menos 44×44 px) con badge visible de la tecla asignada (`F1` a `F12`, `Esc`, `Enter`).
- **Preservación de EAN-13 como Texto:** La captura de códigos de barras conserva rigurosamente los ceros a la izquierda (formato `string`).
- **Secuencia Inequívoca de Foco y Auto-Refocus:** Para armonizar la agilidad del escaneo continuo con la edición en grilla y los flujos de cobro modal sin colisiones de foco:
  1. **Acciones en Grilla y Carrito Principal:** Tras cada clic de ratón sobre acciones estándar (ej. agregar ítem, botones de comando directo o incremento), el foco retorna de inmediato al input principal de escaneo EAN-13 (`F1`) en $\le 50\text{ ms}$, eliminando la necesidad de reubicar manualmente el cursor antes de escanear.
  2. **Apertura de Modales o Diálogos (`F2`, `F3`, `F7`, `F9`, `F12`):** La apertura de un modal suspende temporalmente el auto-refocus al escáner y activa un *focus trap*. El foco se transfiere de inmediato al primer campo de entrada editable del modal (p. ej., `input-monto-recibido` en el modal de cobro `F9` o el input de búsqueda en `F2` y `F3`), permitiendo digitación inmediata por teclado físico o numérico.
  3. **Edición en Línea de Cantidad (`F4` o Clic en Celda):** Al enfocar un campo editable en la grilla mediante clic o atajo, el foco permanece en dicho campo hasta que el usuario confirma con `Enter` o `Tab`, o cancela con `Escape`. Tras la confirmación o cancelación, el foco retorna de inmediato al escáner EAN-13 en $\le 50\text{ ms}$.
  4. **Cierre de Modales y Restitución:** Al cerrarse cualquier diálogo o modal (`Escape`, botón cerrar o confirmación de cobro), el foco visible (anillo `#e4f222`) se restituye de forma incondicional al input principal de escaneo EAN-13 (`F1`) en $\le 50\text{ ms}$, dejando la terminal lista para el siguiente escaneo.

| Tecla | Acción en el Mostrador POS | Regla de Foco y Comportamiento UI (Teclado y Ratón) |
| :--- | :--- | :--- |
| **`F1`** | **Foco en Escáner / Búsqueda SKU** | Selecciona todo el texto del input omnicanal superior y activa el borde `#e4f222`. Botón $\ge 44 \times 44\text{ px}$. |
| **`F2`** | **Buscador Paramétrico Ferretero** | Abre modal de búsqueda por Familia, Medida (Pulgadas/mm), Paso de Rosca y Grado. |
| **`F3`** | **Asignar Cliente (DNI / RUC)** | Abre buscador instantáneo de cliente (Público, Maestro de Obra, Contratista). |
| **`F4`** | **Editar Cantidad de Línea** | Enfoca el input numérico de cantidad en la fila activa; valida `cant <= StockDisponible`. |
| **`F5`** | **Inspeccionar Stock (`RNF-CON-003`)** | Muestra en el panel derecho el desglose: `Stock Físico`, `Stock Comprometido` y `Disponible` en Tienda y Almacén Central. |
| **`F6`** | **Pantalla Entrega Web (`RF-POS-011`)** | Conmuta a la vista ligera de entrega de pedidos web trasladados con Guía Interna. |
| **`F7`** | **Consultar Sustitutos / Equivalentes** | Lista SKUs equivalentes en dimensión/función cuando el `Stock Disponible` es `0`. |
| **`F8`** | **Eliminar Ítem del Ticket** | Elimina la fila seleccionada y reasigna el foco a la fila inmediata o al lector (`F1`). |
| **`F9`** | **Totalizar / Pago Mixto** | Abre panel modal de liquidación y cobro mixto (Efectivo, Tarjeta, Yape/Plin con código de operación bancaria). |
| **`F10`** | **Emitir Boleta Electrónica** | Preconfigura y emite Boleta de Venta Electrónica enviando payload a PSE/OSE. |
| **`F11`** | **Encolar / Recuperar Ticket en Espera** | Encola el carrito actual en memoria local (hasta 2 horas sin movimientos de Kardex, `RF-POS-005`) o lo recupera con revalidación de stock. |
| **`F12`** | **Emitir Factura Electrónica** | Verifica RUC 20/10 activo y abre el modal de liquidación rápida para Factura Electrónica. |
| **`↑` / `↓`** | **Recorrer Filas de Tabla** | Mueve la selección activa (`background: var(--color-sigic-accent-muted)`) entre ítems. También seleccionable por clic directo. |
| **`Enter`** | **Confirmar / Agregar / Cobrar** | En buscador: inserta el SKU. En edición `F4`: guarda cantidad. En cobro: emite comprobante. |
| **`Escape`** | **Cancelar / Regresar a Escáner** | Cierra cualquier modal activo y restituye inmediatamente el foco al input `F1`. |

---

## 4. Pantalla Ligera de Despacho Web en Tienda (`RF-POS-011` y `RF-INV-003`)

Activada mediante la tecla **`F6`** en los terminales autorizados de mostrador/despacho:
1. **Propósito:** Agilizar la entrega presencial de pedidos comprados en el Portal Web cuya mercadería fue reservada en el **Almacén Central** y enviada a la tienda mediante una **Guía Interna de Traslado** automática (`RF-INV-003`).
2. **Interfaz Ultra-Compacta (`1024x768`):**
   - Campo superior de lectura de **Código QR / Número de Pedido Web / Número de Guía Interna (`#GI-...`)** con foco automático.
   - Tabla de verificación rápida con 4 datos clave por línea: **SKU**, **Descripción + Badge de Medida**, **Ubicación en Gaveta de Retiro** y **Cantidad a Entregar**.
   - Confirmación en un solo paso: Escanear el código del cliente + presionar **`Enter`** marca la orden como `ENTREGADO_EN_TIENDA` y emite el acta de conformidad sin tocar el ratón.

---

## 5. Directrices Específicas para el Portal Web E-Commerce (Next.js)

### 5.1 Segregación de Catálogo (`RF-INV-001`)
- Los productos con `disponible_en_web == false` jamás se renderizan en listados, buscadores ni sugerencias. Si un usuario accede por URL directa a un SKU con `disponible_en_web == false`, Next.js invoca `notFound()` retornando una pantalla **HTTP 404** limpia sin exponer datos técnicos ni saldos de inventario.

### 5.2 Visualización Diferenciada por Perfil de Cliente, PDP y Calculadora de Mediciones (Módulo 5)
Gobernado por el **Módulo de Detalles del Producto (PDP), Mediciones y Descuentos** (`SIGIC.Modules.ProductDetailsPricing`):
- **Ficha Técnica PDP y Especificaciones:** Despliegue estructurado de atributos técnicos ferreteros (medidas en pulgadas/mm, cédula, rosca, norma técnica), fichas descargables en PDF y galerías de producto.
- **Calculadora de Mediciones y Factores de Conversión (`RF-INV-005`):** Selector interactivo de unidades comerciales (unidad, docena, bolsa, millar, metro, varilla) con conversión matemática exacta y automática a la unidad base de inventario con precisión de 4 decimales.
- **Escalas Tarifarias y Descuentos por Perfil B2B/B2C (`RF-PRE-001`, `RF-PRE-002`):**
  - **Público General (B2C):** Muestra Precio Unitario con IGV incluido y badge de disponibilidad basado estrictamente en `stock_fisico - stock_comprometido` del Almacén Central (con desfase asíncrono `<= 30s`).
  - **Maestro de Obra:** Destaca la tarifa preferencial (Lista 2) con un badge en `--color-sigic-accent` (`#e4f222` con texto `#08090a`: `TARIFA MAESTRO`).
  - **Contratista / Empresa (B2B):** Incluye selector global en la barra superior (`Precios Sin IGV` / `Precios Con IGV`), tabla de escalas por volumen (Unidad, Ciento, Millar/Caja Master) y ficha técnica descargable.
  - **Protección Inquebrantable de Margen:** Todo precio resultante de una tarifa o descuento cumple `precioVenta >= costoUnitario`, blindado por la excepción de dominio `MargenInvalidoException`. Prohibido vender bajo el costo.

### 5.3 Experiencia de Checkout Atómico y Reservas Dinámicas (`RF-WEB-002` y `RF-WEB-005`)
Al iniciar el Checkout, el backend ejecuta la transacción corta con `SELECT ... FOR UPDATE` + `UPDATE condicionado` ordenando los IDs ascendentemente:
1. **Notificación de Revalidación Atómica (`RF-WEB-005`):** Si el precio o el `Stock Disponible` cambió respecto a la vista en caché de 30 segundos, se despliega un bloque de alerta técnica con borde `--color-sigic-stock-warn` (`#f59e0b`) detallando el ajuste exacto antes de procesar el pago.
2. **Temporizadores Visuales de Reserva (`RF-WEB-002`):**
   - **Flujo Pasarela de Tarjeta:** Cronómetro regresivo persistente de **`20:00` minutos** en `--font-mono` con acento `#e4f222`, indicando que las unidades están comprometidas exclusivamente para su sesión.
   - **Flujo Pago Manual (Yape / Plin / Transferencia):** Panel dividido con dos relojes sincronizados:
     - **Reloj Crítico Primario (`60:00` minutos / 1 hora):** Plazo máximo para adjuntar la captura o número de operación del comprobante de pago.
     - **Vigencia Total de Reserva (`04:00:00` horas / 240 minutos):** Ventana máxima garantizada de stock comprometido mientras administración valida el abono en cuentas.
3. **Stepper de Trazabilidad Logística (`RF-INV-003`):**
   - Muestra en tiempo real los hitos: `Reserva Activa` → `Pago Verificado` → `Picking en Almacén Central` → `En Tránsito a Tienda (Guía Interna #GI-XXXX) / En Ruta a Obra` → `Listo para Retiro / Entregado`.

---

## 6. Verificación y Pruebas de Interfaz

La validación integral de los flujos de interacción omnicanal y las interfaces de usuario se especifica en [02-pruebas-e2e.md](docs/entregables/sprint-2/02-pruebas-e2e.md).
