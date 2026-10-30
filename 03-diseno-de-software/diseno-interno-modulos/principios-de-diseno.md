---
id: "03-principios-de-diseno"
doc: "03-diseno-de-software/diseno-interno-modulos/principios-de-diseno.md"
version: "2.2.0"
estado: "VALIDADO_PARA_EXPOSICION"
tipo: "Principios de Diseño de Software"
proyecto: "SIGIC-Ferreteria-v2.2"
autor: "Crhistian Emanuel Flores Marquina"
evaluacion: "IS-488 UNSCH"
fecha: "2026-10-30"
---

# Principios de Diseño de Software — SIGIC Ferretería

> **Documento:** `03-diseno-de-software/diseno-interno-modulos/principios-de-diseno.md`  
> **Versión de Especificación:** 2.2 (Oficial – SIGIC Ferretería)  
> **Disciplina:** Principios de Ingeniería de Software (SOLID, DRY, KISS, YAGNI, Clean Code)  
> **Sistema:** SIGIC Ferretería (Monolito Modular ASP.NET Core 8 Web API)  
> **Evaluación Académica:** IS-488 Arquitectura de Software (UNSCH)

---

## 1. Visión General de Principios Rectores

El desarrollo de **SIGIC Ferretería v2.2** no solo obedece a requerimientos funcionales, sino que se construye sobre principios rigurosos de ingeniería de software para prevenir la degradación de la base de código (*Software Rot*), maximizar la testabilidad unitaria y garantizar que las reglas del negocio ferretero permanezcan puras e inmutables frente a cambios tecnológicos:

1. **Rendimiento y Baja Latencia (AC01):** Interfaces segregadas y consultas directas sin sobrecarga.
2. **Consistencia Transaccional ACID (AC02):** Encapsulamiento puro y políticas *Fail-Fast* sobre el inventario.
3. **Trazabilidad Inmutable (AC03):** Registro de auditoría estricto y diseño de dominio basado en comportamiento (*Tell, Don't Ask*).

---

## 2. Aplicación de Principios SOLID en el Dominio Ferretero

### 2.1. S — Single Responsibility Principle (Principio de Responsabilidad Única)
- **Definición:** *Una clase o módulo debe tener una, y solo una, razón para cambiar.*
- **Aplicación en SIGIC:**
  - Se evita la creación de servicios o controladores "Dios" (*God Objects*). Por ejemplo, el proceso de reserva web se segrega estrictamente:
    1. `ReservaDinamicaService`: Su única responsabilidad es aplicar las reglas de caducidad temporal (TTL 20 min o 4 h) y validar si el stock comprometido puede incrementarse (`AC02`).
    2. `ReservaExpirationWorker`: Su única responsabilidad es ejecutar periódicamente la comprobación de tiempo y liberar reservas vencidas.
    3. `CheckoutWebController`: Su única responsabilidad es recibir la solicitud HTTP pública, invocar el comando MediatR y responder con el DTO correspondiente.
- **Beneficio:** Un cambio en la política de vencimiento de reservas no altera en lo absoluto el cálculo de IGV, la persistencia en base de datos ni los controladores de mostrador.

---

### 2.2. O — Open/Closed Principle (Principio de Abierto/Cerrado)
- **Definición:** *Las entidades de software deben estar abiertas para su extensión, pero cerradas para su modificación.*
- **Aplicación en SIGIC:**
  - El motor de tarifas mayoristas (`IPrecioStrategy`) permite incorporar nuevas listas de precios (ej. Lista de "Convenios Estatales" o "Club Ferretero") simplemente creando una nueva clase que implemente la interfaz, sin alterar ni una sola línea del código existente en el núcleo de ventas POS o del carrito web.

---

### 2.3. L — Liskov Substitution Principle (Principio de Sustitución de Liskov)
- **Definición:** *Los objetos de un programa deben ser reemplazables por instancias de sus subtipos sin alterar el correcto funcionamiento del sistema.*
- **Aplicación en SIGIC:**
  - Las diferentes implementaciones de generadores de comprobantes (`BoletaGenerator`, `FacturaGenerator`) respetan rigurosamente el contrato de `IComprobanteGenerator`. Si el sistema procesa una venta y recibe cualquier subtipo de comprobante, el cálculo de base imponible y desglose de IGV al 18 % mantiene invariante el comportamiento esperado.

---

### 2.4. I — Interface Segregation Principle (Principio de Segregación de Interfaces)
- **Definición:** *Los clientes no deben verse obligados a depender de interfaces que no utilizan.*
- **Aplicación en SIGIC:**
  - En lugar de una interfaz gigantesca `IInventarioRepository`, el sistema define interfaces cohesivas y de propósito específico:
    - `IInventarioReadRepository`: Métodos de solo lectura para el catálogo público y consultas de stock rápido ($P95 < 250\text{ ms}$, `AC01`).
    - `IInventarioStockCommandRepository`: Métodos transaccionales con bloqueo de fila (`BloquearParaCheckoutAsync`, `DecrementarStockAsync`).
    - `IGuiaTrasladoRepository`: Métodos exclusivos para la gestión logística de guías de remisión interna.
- **Beneficio:** Los consumidores de catálogo no tienen acceso accidental a métodos de mutación transaccional.

---

### 2.5. D — Dependency Inversion Principle (Principio de Inversión de Dependencias)
- **Definición:** *Los módulos de alto nivel no deben depender de los módulos de bajo nivel; ambos deben depender de abstracciones.*
- **Aplicación en SIGIC:**
  - La capa de Dominio y la capa de Aplicación jamás referencian a `Npgsql`, `Microsoft.EntityFrameworkCore` o librerías de pasarelas de pago.
  - La capa de Aplicación define el puerto:
    ```csharp
    public interface IPaymentGatewayAdapter
    {
        Task<ResultadoPagoDto> ProcesarWebhookAsync(string payload, string firma);
    }
    ```
  - La capa de Infraestructura provee el adaptador concreto (`CulqiPaymentAdapter`). La inyección de dependencias en el arranque de la aplicación (`Program.cs`) ensambla las instancias concretas en tiempo de ejecución.

---

## 3. Principios DRY, KISS y YAGNI en la Arquitectura Omnicanal

### 3.1. DRY (Don't Repeat Yourself — No te repitas)
- **La Violación Clásica en Ferreterías:** Duplicar la validación de stock disponible, el cálculo del IGV al 18 % y la verificación de precios mayoristas en dos lugares: una vez en el código del POS de mostrador y otra vez en el backend del e-commerce.
- **Solución en SIGIC v2.2:**
  - Ambos canales (la SPA React 18 del mostrador y el portal Next.js 14) son clientes desacoplados que consumen el **mismo núcleo transaccional en ASP.NET Core 8 Web API**.
  - La fórmula matemática $\text{Stock Disponible} = \text{stock\_fisico} - \text{stock\_comprometido}$ y el cálculo del IGV residen **exclusivamente en la capa de Dominio**. Ningún frontend calcula importes fiscales oficiales.

### 3.2. KISS (Keep It Simple, Stupid — Mantenlo simple)
- **Decisión Arquitectónica:** En lugar de desplegar una arquitectura de microservicios distribuidos con orquestación Kubernetes, coordinadores de transacciones distribuidas (Sagas / 2PC) y colas Kafka (lo cual introduciría complejidad innecesaria para una ferretería en Huamanga), se optó por un **Monolito Modular con PostgreSQL 16 y PgBouncer**.
- **Resultado:** Despliegue simple, cero latencia inter-servicio en red LAN para el POS y transacciones ACID inmediatas directas.

### 3.3. YAGNI (You Aren't Gonna Need It — No lo vas a necesitar todavía)
- **Delimitación de Alcance:**
  - Se descartó explícitamente implementar rastreo satelital GPS en tiempo real de vehículos de reparto o algoritmos complejos de *machine learning* predictivo de inventario.
  - El sistema resuelve con precisión matemática lo que el negocio ferretero realmente necesita hoy: control riguroso de Kardex por Costo Promedio Ponderado, eliminación de sobreventas, venta rápida por teclado y entrega ágil en tienda (`RF-POS-011`).

---

## 4. Principios de Robustez en Dominio: Fail-Fast y Tell, Don't Ask

### 4.1. Principio Fail-Fast (Falla Rápido - AC02)
- Todas las operaciones críticas validan sus invariantes en las primeras líneas de ejecución mediante cláusulas de guarda (*Guard Clauses*), antes de abrir transacciones costosas de base de datos o modificar memoria:
```csharp
public void ReservarStock(decimal cantidadSolicitada, TimeSpan ttl)
{
    if (cantidadSolicitada <= 0)
        throw new CantidadInvalidaException("La cantidad a reservar debe ser estrictamente mayor a cero.");
        
    if ((StockFisico - StockComprometido) < cantidadSolicitada)
        throw new StockInsuficienteException($"Stock disponible insuficiente. Físico: {StockFisico}, Comprometido: {StockComprometido}.");
        
    // Procede únicamente si los invariantes se cumplen
    StockComprometido += cantidadSolicitada;
}
```

### 4.2. Principio Tell, Don't Ask (Ordena, no preguntes - AC03)
- Los objetos del dominio ferretero no son simples estructuras de datos anémicas con *getters* y *setters* públicos. En lugar de consultar el estado de una entidad desde un servicio externo y modificar sus propiedades, el servicio le ordena al objeto que ejecute la transición de negocio:
- **Incorrecto (Modelo Anémico / Ask):**
  ```csharp
  // Mal: El servicio pregunta y modifica externamente
  if (guia.Estado == EstadoGuia.EnTransito) {
      guia.Estado = EstadoGuia.Recibido;
      guia.FechaConfirmacion = DateTime.UtcNow;
  }
  ```
- **Correcto en SIGIC (Tell, Don't Ask):**
  ```csharp
  // Bien: El agregado encapsula y protege su máquina de estados
  guia.ConfirmarRecepcionFisica(usuarioReceptorId, DateTime.UtcNow);
  ```
