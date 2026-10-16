# Modelo C4 — Nivel 4: Diagrama de Código (Clases y Secuencias Críticas)

> **Documento:** `04-modelo-c4/Nivel4-DiagramadeCodigo.md`  
> **Versión de Especificación:** 2.2 (Oficial – SIGIC Ferretería)  
> **Estándar:** Modelo C4 para Visualización de Arquitectura de Software (Simon Brown) – Nivel 4 (Código e Implementación)  
> **Sistema:** SIGIC Ferretería (Monolito Modular ASP.NET Core 8 Web API)

---

## 1. Propósito del Nivel 4 (Code)

El **Diagrama de Código (Nivel 4)** desciende al detalle más granular de la arquitectura de software. En sistemas donde los componentes generales ocultan patrones de diseño complejos o condiciones de carrera de misión crítica, el Nivel 4 formaliza:
1. El **Diagrama de Clases Estructural** de los Agregados de Dominio más significativos (`Articulo`, `InventarioStock`, `PedidoWeb`, `ReservaStockPedido`).
2. El **Diagrama de Secuencia 1:** Flujo de Checkout Web con revalidación atómica de precio y existencias, bloqueo pesimista ordenado (`ORDER BY producto_id ASC`) y asignación de TTL dinámico (`RF-WEB-002`, `RF-WEB-005`, `RNF-CON-003`).
3. **El Diagrama de Secuencia 2:** Flujo de Venta Rápida en Mostrador POS mediante escáner óptico y atajos de teclado sin mouse (`RF-POS-003`, `RF-POS-004`).

---

## 2. Diagrama de Clases del Agregado de Inventario y Reservas

El siguiente diagrama modela la estructura de clases de la Capa de Dominio encargada de salvaguardar la regla cardinal: $\text{Stock Disponible} = \text{Stock Físico} - \text{Stock Comprometido}$.

```mermaid
classDiagram
    class Articulo {
        +Guid Id
        +string CodigoInterno
        +string Ean13
        +string NombreTecnico
        +UnidadMedida UnidadBase
        +Guid FamiliaId
        +Guid SubfamiliaId
        +bool DisponibleEnWeb
        +decimal CostoPromedioPonderado
        +ActualizarCostoPonderado(decimal cantEntrante, decimal costoUnitario) void
        +EsVisibleParaWeb() bool
    }

    class InventarioStock {
        +Guid Id
        +Guid AlmacenId
        +Guid ArticuloId
        +decimal StockFisico
        +decimal StockComprometido
        +decimal StockDisponible() decimal
        +BloquearParaReserva(decimal cantidad) void
        +LiberarReserva(decimal cantidad) void
        +RegistrarSalidaFisica(decimal cantidad) void
        +RegistrarEntradaFisica(decimal cantidad) void
    }

    class PedidoWeb {
        +Guid Id
        +string CodigoPedido
        +Guid ClienteWebId
        +EstadoPedido Estado
        +ModalidadDespacho Modalidad
        +decimal MontoTotal
        +DateTime FechaCreacionUtc
        +List~PedidoDetalle~ Detalles
        +ReservaStockPedido ReservaAsociada
        +AsignarReserva(ReservaStockPedido reserva) void
        +AprobarPagoPorTesoreria(Guid usuarioTesoreroId) void
        +CancelarPorExpiracion() void
        +MarcarListoParaRetiro() void
        +EntregarEnMostrador(string dniReceptor) void
    }

    class ReservaStockPedido {
        +Guid Id
        +Guid PedidoId
        +EstadoReserva Estado
        +DateTime FechaInicioUtc
        +DateTime FechaExpiracionUtc
        +TimeSpan TtlAsignado
        +bool RequiereComprobanteManual
        +DateTime? FechaLimiteComprobanteUtc
        +EsVencida() bool
        +Consolidar() void
        +Revocar() void
    }

    class Almacen {
        +Guid Id
        +string Nombre
        +TipoAlmacen Tipo
        +bool EsAlmacenCentral
    }

    Articulo "1" <-- "1..*" InventarioStock : registra existencia
    Almacen "1" <-- "1..*" InventarioStock : pertenece a
    PedidoWeb "1" *-- "1" ReservaStockPedido : gobierna TTL de
    InventarioStock "1" <-- "0..*" ReservaStockPedido : compromete existencias
```

---

## 3. Diagrama de Secuencia 1: Checkout Web con Revalidación Atómica y TTL Dinámico

Este flujo modela la resolución de `RF-WEB-002`, `RF-WEB-005` y `RNF-CON-003` ante la concurrencia con el mostrador físico:

```mermaid
sequenceDiagram
    autonumber
    actor Cliente as Cliente Web (Navegador)
    participant Ctrl as CheckoutWebController
    participant MediatR as IniciarCheckoutCommandHandler
    participant Tarifas as MotorTarifasDomainService
    participant InvService as InventarioDomainService
    participant DB as PostgreSQL 16 (sigic_oltp via pool_web)
    participant Gateway as IPaymentGatewayAdapter

    Cliente->>Ctrl: POST /api/v1/public/checkout (Items, Modalidad, MedioPago)
    Ctrl->>MediatR: IniciarCheckoutCommand
    
    MediatR->>MediatR: 1. Ordena artículos por ProductoId ASC en memoria
    MediatR->>DB: BEGIN TRANSACTION (Read Committed)
    
    Note over MediatR,DB: Bloqueo Pesimista Ordenado Anti-Deadlock (RNF-CON-002)
    MediatR->>DB: SELECT id, stock_fisico, stock_comprometido FROM inventario_stock WHERE almacen_id = @AlmacenCentralId AND articulo_id IN (@IdsOrdenados) ORDER BY articulo_id ASC FOR UPDATE;
    
    Note over MediatR,Tarifas: 2. Revalidación Atómica de Precio Vigente (RF-WEB-005)
    MediatR->>Tarifas: RevalidarPreciosVigentes(Items, Cliente.Perfil)
    alt Precio Cambió respecto al Carrito en Caché
        MediatR->>DB: ROLLBACK;
        MediatR-->>Ctrl: Error: PrecioModificadoResult
        Ctrl-->>Cliente: HTTP 409 Conflict (Muestra nuevo precio y solicita confirmación)
    end

    Note over MediatR,InvService: 3. Revalidación Atómica de Stock Disponible
    loop Por cada artículo
        MediatR->>InvService: ValidarDisponible(stock_fisico, stock_comprometido, cantSolicitada)
        alt Stock Disponible < Cantidad Solicitada
            MediatR->>DB: ROLLBACK;
            MediatR-->>Ctrl: Error: StockInsuficienteResult
            Ctrl-->>Cliente: HTTP 409 Conflict (Artículo sin stock suficiente)
        end
    end

    Note over MediatR: 4. Asignación de TTL Dinámico (RF-WEB-002)
    alt MedioPago == Tarjeta (Pasarela)
        Note over MediatR: TTL = 20 minutos
    else MedioPago == Yape / Plin / Transferencia
        Note over MediatR: TTL = 4 horas (Límite carga comprobante = 1 hora)
    end

    MediatR->>DB: UPDATE inventario_stock SET stock_comprometido = stock_comprometido + @Cant WHERE ...;
    MediatR->>DB: INSERT INTO pedido_web (...) VALUES (...);
    MediatR->>DB: INSERT INTO reserva_stock_pedido (pedido_id, fecha_expiracion, ttl, ...) VALUES (...);
    
    alt MedioPago == Tarjeta
        MediatR->>Gateway: InicializarTransaccionAsync(PedidoId, Monto)
        Gateway-->>MediatR: SesionToken / CheckoutUrl
    end

    MediatR->>DB: COMMIT;
    MediatR-->>Ctrl: CheckoutExitosoDto (PedidoId, TTL, Redirección)
    Ctrl-->>Cliente: HTTP 201 Created (Orden creada y Stock Comprometido reservado)
```

---

## 4. Diagrama de Secuencia 2: Venta Rápida en Mostrador POS sin Mouse

Este flujo modela la interacción de alta velocidad para el cajero descrita en `RF-POS-003`, `RF-POS-004` y `RNF-PERF-001`:

```mermaid
sequenceDiagram
    autonumber
    actor Cajero as Cajero Mostrador (Teclado/Escáner)
    participant SPA as SPA POS React 18 (LAN)
    participant Ctrl as VentasPOSController
    participant MediatR as RegistrarVentaPOSCommandHandler
    participant InvService as InventarioDomainService
    participant DB as PostgreSQL 16 (sigic_oltp via pool_pos)
    participant PSE as PseFacturacionAdapter
    participant Impresora as Impresora Térmica ESC/POS

    Note over Cajero,SPA: Operación por escáner láser sin tocar el mouse
    Cajero->>SPA: Pasa código de barras (EAN-13)
    SPA->>SPA: Agrega ítem a la grilla y mantiene foco listo para el siguiente
    Cajero->>SPA: Presiona F9 (Totalizar y Panel de Pagos)
    Cajero->>SPA: Ingresa pago mixto (Efectivo + Código Operación Yape)
    Cajero->>SPA: Presiona F12 (Confirmar e Imprimir)

    SPA->>Ctrl: POST /api/v1/internal/pos/ventas (Payload Venta)
    Ctrl->>MediatR: RegistrarVentaPOSCommand
    
    MediatR->>DB: BEGIN TRANSACTION
    MediatR->>DB: SELECT id, stock_fisico, stock_comprometido FROM inventario_stock WHERE almacen_id = @AlmacenMostradorId AND articulo_id IN (@Ids) ORDER BY articulo_id ASC FOR UPDATE;
    
    MediatR->>InvService: ValidarExistenciasFisicas(stock_fisico, cant)
    alt Existencias Suficientes
        MediatR->>DB: UPDATE inventario_stock SET stock_fisico = stock_fisico - @Cant WHERE id = @Id;
        MediatR->>DB: INSERT INTO kardex_movimiento (tipo = 'SALIDA_VENTA_POS', ...) VALUES (...);
        MediatR->>DB: INSERT INTO venta_pos (...) VALUES (...);
        MediatR->>DB: INSERT INTO caja_movimiento (turno_id, ...) VALUES (...);
        
        alt Comprobante Electrónico (Boleta/Factura)
            MediatR->>PSE: GenerarPayloadTributario(Venta)
            PSE-->>MediatR: JSON/XML Payload estructurado
            MediatR->>DB: INSERT INTO comprobante_electronico (...) VALUES (...);
        end

        MediatR->>DB: COMMIT;
        MediatR-->>Ctrl: VentaConfirmadaDto (Correlativo, Monto, TicketData)
        Ctrl-->>SPA: HTTP 200 OK (Venta exitosa en < 1 s)
        SPA->>Impresora: Emite comandos directos de impresión térmica ESC/POS
        SPA-->>Cajero: Pantalla limpia con foco en campo de escáner para nueva venta
    else Existencias Insuficientes
        MediatR->>DB: ROLLBACK;
        MediatR-->>Ctrl: Error: StockFisicoAgotado
        Ctrl-->>SPA: HTTP 400 Bad Request
        SPA-->>Cajero: Alerta sonora y visual: "Stock insuficiente en mostrador"
    end
```
