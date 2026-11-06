---
id: "02-estrategia-cache"
doc: "02-arquitectura-software/estrategia-cache.md"
version: "2.2.0"
sistema: "SIGIC - Sistema Integrado de Gestión de Inventario y Comercialización"
metodologia: "Spec-Driven Development (SDD)"
autor: "Crhistian Emanuel Flores Marquina"
evaluacion: "IS-488 Arquitectura de Software - UNSCH"
criterio: "Criterio 11: Implementación de Caché Multinivel (L1 / L2)"
fecha: "2026-11-06"
estado: "VALIDADO_PARA_EXPOSICION"
---

# Estrategia de Caché Multinivel (L1 / L2) y Regla Cardinal de Bypass de Stock

> **Documento:** `02-arquitectura-software/estrategia-cache.md`  
> **Sistema:** SIGIC Ferretería v2.2  
> **Asignatura:** IS-488 Arquitectura de Software (UNSCH)  
> **Criterio Oficial de Evaluación:** **Criterio 11 — Implementación de Caché Multinivel (L1 / L2)**  
> **Patrón Arquitectónico:** Cache-Aside Multinivel con Invalidación Reactiva por Eventos  
> **Tecnologías:** .NET 8 `HybridCache` / `IMemoryCache`, Redis 7.2 y PostgreSQL 16 con Dapper

---

## 1. Arquitectura del Patrón Cache-Aside Multinivel

Para lograr tiempos de respuesta en catálogo público inferiores a 400 ms (AC01) sin comprometer la consistencia matemática del inventario de materiales pesados (AC02), SIGIC Ferretería implementa una **estrategia de almacenamiento en caché segregada en dos niveles jerárquicos (L1 y L2)** con una política estricta de **Bypass Transaccional**:

```mermaid
flowchart TD
    subgraph PeticionesEntrantes ["Flujo de Peticiones HTTP"]
        REQ_CAT["1. Consulta de Catálogo Web / Metadatos<br/>(GET /api/v1/public/catalogo)"]
        REQ_STOCK["2. Consulta de Stock en Tiempo Real y Checkout<br/>(POST /api/v1/checkout/procesar)"]
    end

    subgraph L1_Cache ["Nivel 1: In-Memory Cache (L1)"]
        MEM_L1["ASP.NET Core 8 IMemoryCache<br/>Metadatos inmutables: Unidades, Familias, IGV<br/>TTL: 60 minutos | Latencia: < 1 µs"]
    end

    subgraph L2_Cache ["Nivel 2: Distributed Cache (L2)"]
        REDIS_L2["Redis 7.2 / HybridCache (.NET 8)<br/>Catálogo de productos desnormalizado en JSON<br/>TTL: 5 minutos | Latencia: < 2 ms"]
    end

    subgraph InvalidationService ["Invalidación Reactiva"]
        EVENT_BUS["CatalogoActualizadoDomainEvent<br/>(Disparado al actualizar precios o stock inicial)"]
    end

    subgraph DatabaseEngine ["Base de Datos PostgreSQL 16 (sigic_oltp)"]
        DB_QUERY["Lectura Dapper SQL<br/>(Latencia: < 15 ms)"]
        DB_ACID["Núcleo Transaccional ACID<br/>(SELECT ... FOR UPDATE / Cero Lecturas Sucias)"]
    end

    REQ_CAT --> MEM_L1
    MEM_L1 -->|Cache Miss L1| REDIS_L2
    REDIS_L2 -->|Cache Miss L2| DB_QUERY
    DB_QUERY -->|Repuebla L2 y L1| REDIS_L2

    EVENT_BUS -. Purga claves redis .-> REDIS_L2
    EVENT_BUS -. Invalida memoria .-> MEM_L1

    %% Regla Cardinal de Negocio: Bypass Total de Caché
    REQ_STOCK ==>|BYPASS TOTAL DE CACHÉ (100% ACID)| DB_ACID

    style REQ_STOCK fill:#ef4444,stroke:#991b1b,stroke-width:2px,color:#fff
    style DB_ACID fill:#10b981,stroke:#065f46,stroke-width:2px,color:#fff
```

---

## 2. Nivel 1: In-Memory Cache de Proceso (L1 — `IMemoryCache`)

* **Tecnología:** `Microsoft.Extensions.Caching.Memory.IMemoryCache` embebida en el espacio de memoria de proceso de ASP.NET Core 8 Web API.
* **Latencia de Acceso:** Sub-microsegundo ($< 1\text{ }\mu\text{s}$), sin llamadas de red ni serialización I/O.
* **Datos Custodiados (Metadatos Inmutables de Alta Lectura):**
  1. Catálogo de unidades de medida y factores de conversión (bolsa, varilla, kg, millar, docena).
  2. Árbol de categorización fija (Familias y Subfamilias ferreteras).
  3. Parámetros tributarios estables (Tasa de IGV 18%, series de comprobantes autorizadas).
  4. Listas de precios vigentes asignadas a roles (Factores de descuento para Maestro de Obra y Contratista).
* **Política de Expiración:**
  - **TTL Absoluto:** **60 minutos** (`AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)`).
  - **Límite de Tamaño:** `SizeLimit = 1024` entradas fijas para prevenir fugas de memoria (*Out of Memory*).

---

## 3. Nivel 2: Distributed Cache (L2 — Redis 7.2 / `HybridCache`)

* **Tecnología:** **Redis 7.2** gestionado mediante la abstracción oficial de .NET 8 **`HybridCache`**, combinando caché L1 local y caché L2 distribuido en clúster.
* **Latencia de Acceso:** Menor a 2 milisegundos ($< 2\text{ ms}$) sobre red privada.
* **Datos Custodiados:**
  - Listados paginados del catálogo público web (`GET /api/v1/public/catalogo?categoria=ACEROS&pagina=1`).
  - Fichas técnicas completas de productos para el e-commerce (especificaciones de Cemento Portland, calibres de fierro, cédulas de tuberías).
* **Serialización Optimizada:** Serialización JSON binaria de alta velocidad con `System.Text.Json` y *Source Generators* de C# 12 para evitar costo de *reflection*.
* **Política de Expiración:**
  - **TTL:** **5 minutos** (300 segundos).
  - **Invalidación Reactiva Inmediata:** Si el Superadmin modifica un precio o realiza una carga masiva (`RF-INV-008`), el backend publica el evento de dominio `CatalogoActualizadoDomainEvent`, el cual purga instantáneamente las claves bajo el patrón `sigic:cat:*` en Redis, evitando que los clientes web visualicen precios desactualizados.

---

## 4. Regla Cardinal de Negocio Ferretero: Bypass Absoluto de Caché

> [!CAUTION]
> **Invariante de Dominio:** En un negocio ferretero con materiales de construcción de alta rotación (cemento y fierro), **JAMÁS se almacena en caché el stock disponible en tiempo real ni el proceso de checkout**.

### Justificación de Ingeniería:
1. **Riesgo Crítico de Lecturas Sucias (*Dirty Reads*):** Si el stock disponible se consultara de una caché con apenas 5 segundos de vigencia, dos clientes podrían ver "50 bolsas disponibles" cuando un maestro de obra ya compró 40 en el mostrador físico, provocando una **sobreventa catastrófica**.
2. **Garantía ACID Estricta:**
   - La consulta de stock en tiempo real y el checkout **omiten al 100% la capa de caché**.
   - Se conectan directamente a PostgreSQL 16 mediante consultas de persistencia Dapper ejecutando:
     $$\text{Stock Disponible} = \text{stock\_fisico} - \text{stock\_comprometido}$$
   - En el instante del checkout, se aplica bloqueo pesimista de fila (`SELECT ... FOR UPDATE` ordenado por `producto_id ASC`), asegurando consistencia serializada sin intermediarios de memoria volátil.

---

## 5. Cabeceras HTTP y Validación Condicional de Caché (ETags)

Para optimizar el ancho de banda y la experiencia en dispositivos móviles, los endpoints públicos de catálogo emiten cabeceras estándar RFC 7234:

### 5.1. Cabecera `Cache-Control`
```http
Cache-Control: public, max-age=300, stale-while-revalidate=60
```
- **`max-age=300`:** El navegador y la CDN pueden servir la respuesta desde su caché durante 5 minutos.
- **`stale-while-revalidate=60`:** Si arriba una petición entre el minuto 5 y el minuto 6, la CDN entrega la versión en caché de inmediato y refresca el contenido en segundo plano con el servidor de origen.

### 5.2. Generación Determinista de `ETag`
El backend calcula un hash criptográfico SHA-256 basado en la última marca de tiempo de actualización del catálogo:
```http
ETag: W/"sigic-cat-v2.2-8f92b7c4"
```
Si el cliente web revalida el catálogo enviando `If-None-Match: W/"sigic-cat-v2.2-8f92b7c4"`, y el catálogo no ha sufrido modificaciones, el servidor responde **`HTTP 304 Not Modified`** con payload vacío, ahorrando el 100% de transferencia de datos.

---

## 6. Implementación Técnica de Referencia en .NET 8 (C#)

```csharp
namespace SIGIC.Infrastructure.Caching;

using Microsoft.Extensions.Caching.Hybrid;
using System.Text.Json;

/// <summary>
/// Servicio de gestión de caché multinivel Cache-Aside para el catálogo público (Criterio 11).
/// Aplica HybridCache (.NET 8) e implementa invalidación reactiva.
/// </summary>
public class CatalogoCacheService : ICatalogoCacheService
{
    private readonly HybridCache _hybridCache;
    private readonly ICatalogoDapperRepository _repository;

    public CatalogoCacheService(HybridCache hybridCache, ICatalogoDapperRepository repository)
    {
        _hybridCache = hybridCache;
        _repository = repository;
    }

    public async Task<IReadOnlyList<ProductoCatalogoDto>> GetCatalogoPublicoAsync(
        string categoria, 
        CancellationToken ct = default)
    {
        string cacheKey = $"sigic:cat:{categoria.ToLowerInvariant()}";

        // Cache-Aside Multinivel: L1 In-Memory + L2 Redis con TTL de 5 minutos
        return await _hybridCache.GetOrCreateAsync(
            cacheKey,
            async token => await _repository.ConsultarCatalogoPublicoAsync(categoria, token),
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(5),
                LocalCacheExpiration = TimeSpan.FromMinutes(1)
            },
            cancellationToken: ct);
    }

    /// <summary>
    /// Manejador de eventos de dominio para invalidación reactiva inmediata de caché.
    /// </summary>
    public async Task InvalidateCatalogoAsync(CancellationToken ct = default)
    {
        // Purga reactiva ante actualización masiva o cambio de precios
        await _hybridCache.RemoveByTagAsync("catalogo_publico", ct);
    }
}
```
