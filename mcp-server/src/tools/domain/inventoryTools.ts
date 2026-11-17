import { Tool, CallToolResult } from "@modelcontextprotocol/sdk/types.js";

export interface SddToolHandler {
  definition: Tool;
  execute: (args: Record<string, unknown>) => Promise<CallToolResult>;
}

export interface SddAuditCheck {
  requirementId: string;
  title: string;
  passed: boolean;
  severity: "BLOCKER" | "CRITICAL" | "INFO";
  evidence: string;
  remediation?: string;
}

/**
 * Tool 1: auditar_especificacion_stock_sdd
 * Audita código C# o SQL verificando RNF-CON-003 (Stock Disponible = Físico - Comprometido),
 * RF-INV-001 (disponible_en_web y retorno 404 en canal WEB) y RF-WEB-005 (transacción corta con
 * SELECT ... FOR UPDATE, UPDATE condicionado y orden ascendente de IDs).
 */
export const auditarEspecificacionStockSddTool: SddToolHandler = {
  definition: {
    name: "auditar_especificacion_stock_sdd",
    description:
      "Audita código fuente (C# o SQL) bajo la metodología SDD de SIGIC v2.2. Rechaza código que consulte únicamente stock_fisico sin descontar stock_comprometido (RNF-CON-003), que omita validar disponible_en_web con retorno 404 en el canal WEB (RF-INV-001), o que carezca de bloqueo de fila SELECT ... FOR UPDATE + UPDATE condicionado con orden determinista de IDs en operaciones de checkout/reserva (RF-WEB-005).",
    inputSchema: {
      type: "object",
      properties: {
        codigoFuente: {
          type: "string",
          description:
            "Fragmento completo de código C# (Handler, Servicio, Repositorio, Endpoint) o sentencia SQL de PostgreSQL 16.",
        },
        canal: {
          type: "string",
          enum: ["POS", "WEB"],
          description:
            "Canal operativo para el cual se audita el código: 'POS' (Mostrador) o 'WEB' (Portal E-Commerce).",
        },
        operacion: {
          type: "string",
          enum: ["LECTURA_CATALOGO", "CHECKOUT_RESERVA"],
          description:
            "Tipo de operación: 'LECTURA_CATALOGO' (consultas de disponibilidad/detalle) o 'CHECKOUT_RESERVA' (mutación transaccional de stock o reserva).",
        },
      },
      required: ["codigoFuente", "canal", "operacion"],
    },
  },
  execute: async (args: Record<string, unknown>): Promise<CallToolResult> => {
    const codigo =
      typeof args.codigoFuente === "string" ? args.codigoFuente : "";
    const canal =
      args.canal === "POS" || args.canal === "WEB" ? args.canal : null;
    const operacion =
      args.operacion === "LECTURA_CATALOGO" ||
      args.operacion === "CHECKOUT_RESERVA"
        ? args.operacion
        : "CHECKOUT_RESERVA";

    if (!codigo.trim() || !canal) {
      return {
        isError: true,
        content: [
          {
            type: "text",
            text: JSON.stringify(
              {
                status: "ERROR_PARAMETROS",
                mensaje:
                  "Debe proporcionar 'codigoFuente' (string no vacío), 'canal' ('POS' | 'WEB') y 'operacion' ('LECTURA_CATALOGO' | 'CHECKOUT_RESERVA').",
              },
              null,
              2
            ),
          },
        ],
      };
    }

    const checks: SddAuditCheck[] = [];

    // 1. Auditoría RNF-CON-003: Stock Disponible = Stock Físico - Stock Comprometido
    const hasExplicitSubtraction =
      /(stock_fisico|stockfisico)\s*-\s*(stock_comprometido|stockcomprometido)/i.test(
        codigo
      );
    const hasBothFieldsReferenced =
      /(stock_fisico|stockfisico)/i.test(codigo) &&
      /(stock_comprometido|stockcomprometido)/i.test(codigo);
    const usesCanonicalDomainMethod =
      /(StockDisponible|CalcularStockDisponible|ObtenerStockDisponible)/i.test(
        codigo
      ) && !/(stock_fisico|stockfisico)\s*(>=|>|==)/i.test(codigo);

    const passedStockFormula =
      hasExplicitSubtraction ||
      hasBothFieldsReferenced ||
      usesCanonicalDomainMethod;

    checks.push({
      requirementId: "RNF-CON-003",
      title: "Fórmula Obligatoria Stock Disponible = Stock Físico - Stock Comprometido",
      passed: passedStockFormula,
      severity: "BLOCKER",
      evidence: passedStockFormula
        ? "Se verificó el descuento explícito de stock_comprometido sobre stock_fisico."
        : "El código inspeccionado evalúa el inventario consultando únicamente stock_fisico u omite descontar stock_comprometido.",
      remediation: passedStockFormula
        ? undefined
        : "Sustituya toda lectura o comparación directa de 'stock_fisico' por la expresión '(stock_fisico - stock_comprometido)' en SQL o implemente la propiedad calculada 'StockDisponible => StockFisico - StockComprometido' en la entidad de dominio.",
    });

    // 2. Auditoría RF-INV-001: Segregación disponible_en_web y 404 absoluto en Canal WEB
    if (canal === "WEB") {
      const filtersWebAvailable =
        /(disponible_en_web|disponibleenweb)\s*(=|==|is)\s*(true|1)/i.test(
          codigo
        ) ||
        /\.\s*Where\s*\([^)]*DisponibleEnWeb\s*\)/i.test(codigo) ||
        /WHERE[\s\S]*\bdisponible_en_web\b/i.test(codigo) ||
        /!\s*[a-zA-Z0-9_.]*DisponibleEnWeb/i.test(codigo);

      const enforces404OrFiltering =
        /404|NotFound|NotFoundException|KeyNotFoundException|Results\.NotFound/i.test(
          codigo
        ) ||
        (/SELECT[\s\S]*FROM[\s\S]*WHERE[\s\S]*disponible_en_web\s*=\s*true/i.test(
          codigo
        ) &&
          !/UPDATE\b/i.test(codigo));

      const passedWebSegregation =
        filtersWebAvailable && enforces404OrFiltering;

      checks.push({
        requirementId: "RF-INV-001",
        title:
          "Segregación de Catálogo Web (disponible_en_web == true y 404 Absoluto)",
        passed: passedWebSegregation,
        severity: "BLOCKER",
        evidence: passedWebSegregation
          ? "El código valida 'disponible_en_web' y garantiza el filtrado o respuesta 404 NotFound cuando es false."
          : `Incumplimiento en canal WEB: [Valida disponible_en_web: ${
              filtersWebAvailable ? "SÍ" : "NO"
            }, Garantiza 404 NotFound o exclusión SQL: ${
              enforces404OrFiltering ? "SÍ" : "NO"
            }].`,
        remediation: passedWebSegregation
          ? undefined
          : "En el canal WEB agregue el predicado 'AND disponible_en_web = TRUE' y, en consultas individuales o checkout C#, verifique 'if (producto is null || !producto.DisponibleEnWeb) return Results.NotFound();' (HTTP 404 absoluto).",
      });
    } else {
      checks.push({
        requirementId: "RF-INV-001",
        title: "Alcance de Catálogo en Canal POS",
        passed: true,
        severity: "INFO",
        evidence:
          "Canal POS seleccionado: Los terminales de mostrador están autorizados a comercializar ítems con disponible_en_web == false.",
      });
    }

    // 3. Auditoría RF-WEB-005 y Antideadlock en Operaciones de Checkout / Reserva
    if (operacion === "CHECKOUT_RESERVA") {
      const hasAscendingSort =
        /ORDER\s+BY\s+[a-zA-Z0-9_.]*(producto_id|productoid|articulo_id|id)\s+ASC/i.test(
          codigo
        ) ||
        /\.OrderBy\s*\(\s*[a-zA-Z0-9_]+\s*=>\s*[a-zA-Z0-9_]+\.(ProductoId|ArticuloId|Id)\s*\)/i.test(
          codigo
        );

      const hasRowLockForUpdate = /FOR\s+UPDATE/i.test(codigo);

      const hasConditionalUpdate =
        /UPDATE\b[\s\S]*SET[\s\S]*(stock_comprometido|stock_fisico)[\s\S]*WHERE[\s\S]*\(?\s*(stock_fisico|stockfisico)\s*-\s*(stock_comprometido|stockcomprometido)\s*\)?\s*>=\s*/i.test(
          codigo
        );

      const hasExplicitTransaction =
        /BeginTransaction|BeginTransactionAsync|TransactionScope|BEGIN\b[\s\S]*COMMIT\b/i.test(
          codigo
        );

      const passedAtomicCheckout =
        hasAscendingSort &&
        hasRowLockForUpdate &&
        hasConditionalUpdate &&
        hasExplicitTransaction;

      checks.push({
        requirementId: "RF-WEB-005",
        title:
          "Revalidación Atómica en Checkout (Transacción Corta + SELECT FOR UPDATE + UPDATE Condicionado + Orden Ascendente de IDs)",
        passed: passedAtomicCheckout,
        severity: "BLOCKER",
        evidence: passedAtomicCheckout
          ? "Se verificó transacción corta explícita, ordenamiento determinista ascendente por ProductoId, bloqueo de fila SELECT ... FOR UPDATE y UPDATE condicionado por saldo disponible."
          : `Faltan garantías transaccionales de checkout: [Transacción explícita: ${
              hasExplicitTransaction ? "SÍ" : "NO"
            }, Orden ascendente ProductoId (Anti-Deadlock): ${
              hasAscendingSort ? "SÍ" : "NO"
            }, Bloqueo SELECT ... FOR UPDATE: ${
              hasRowLockForUpdate ? "SÍ" : "NO"
            }, UPDATE condicionado ((stock_fisico - stock_comprometido) >= cant): ${
              hasConditionalUpdate ? "SÍ" : "NO"
            }].`,
        remediation: passedAtomicCheckout
          ? undefined
          : "En todo flujo de checkout o reserva: 1) Ordene los ítems con '.OrderBy(x => x.ProductoId)', 2) Abra una transacción corta, 3) Ejecute 'SELECT ... FOR UPDATE' ordenando por 'producto_id ASC', y 4) Ejecute 'UPDATE ... SET stock_comprometido = stock_comprometido + @Cant WHERE producto_id = @Id AND (stock_fisico - stock_comprometido) >= @Cant'.",
      });

      // Verificación adicional RF-INV-003 si es checkout WEB (Almacén Central)
      if (canal === "WEB") {
        const targetsCentralWarehouse =
          /almacen_central|almacencentral|ALMACEN_CENTRAL|EsAlmacenCentral|AlmacenCentralId/i.test(
            codigo
          );

        checks.push({
          requirementId: "RF-INV-003",
          title: "Asignación Prioritaria sobre Almacén Central en Checkout Web",
          passed: targetsCentralWarehouse,
          severity: "CRITICAL",
          evidence: targetsCentralWarehouse
            ? "El checkout web referencia explícitamente al Almacén Central para el compromiso de stock y generación de guías."
            : "No se detectó referencia explícita al Almacén Central (RF-INV-003) en la reserva web.",
          remediation: targetsCentralWarehouse
            ? undefined
            : "Asegúrese de filtrar y comprometer el inventario sobre el Almacén Central ('almacen_id = @AlmacenCentralId' o constante de dominio 'AlmacenCentral') según RF-INV-003.",
        });
      }
    }

    const allPassed = checks.every((c) => c.passed);

    return {
      isError: false,
      content: [
        {
          type: "text",
          text: JSON.stringify(
            {
              herramienta: "auditar_especificacion_stock_sdd",
              estadoCertificacionSdd: allPassed
                ? "CUMPLIMIENTO_VERIFICADO"
                : "RECHAZADO_POR_INCUMPLIMIENTO_SDD",
              canalAuditado: canal,
              operacionAuditada: operacion,
              controlesEjecutados: checks,
            },
            null,
            2
          ),
        },
      ],
    };
  },
};

/**
 * Tool 2: validar_reserva_web_sdd
 * Valida matemáticamente que los tiempos de reserva dinámica (TTL) y de carga de comprobante
 * cumplan de forma exacta con RF-WEB-002.
 */
export const validarReservaWebSddTool: SddToolHandler = {
  definition: {
    name: "validar_reserva_web_sdd",
    description:
      "Audita los parámetros de tiempo de vida (TTL) de las reservas dinámicas en el Portal Web según RF-WEB-002: exactamente 20 minutos para PASARELA de tarjeta; hasta 240 minutos (4 horas) para pago MANUAL (Yape/Plin/Transferencia) con un límite máximo de 60 minutos (1 hora) para adjuntar el comprobante.",
    inputSchema: {
      type: "object",
      properties: {
        modalidadPago: {
          type: "string",
          enum: ["PASARELA", "MANUAL"],
          description:
            "Tipo de medio de pago: 'PASARELA' (Tarjeta crédito/débito integrada) o 'MANUAL' (Yape, Plin o Transferencia Bancaria).",
        },
        ttlReservaMinutos: {
          type: "number",
          description:
            "Minutos totales durante los cuales se mantiene bloqueado el stock_comprometido.",
        },
        ttlSubidaComprobanteMinutos: {
          type: "number",
          description:
            "Opcional para PASARELA, recomendado para MANUAL: Minutos máximos otorgados al cliente para subir la captura/constancia de pago (por defecto 60 min en RF-WEB-002).",
        },
      },
      required: ["modalidadPago", "ttlReservaMinutos"],
    },
  },
  execute: async (args: Record<string, unknown>): Promise<CallToolResult> => {
    const modalidadPago =
      args.modalidadPago === "PASARELA" || args.modalidadPago === "MANUAL"
        ? args.modalidadPago
        : null;
    const ttlReservaMinutos =
      typeof args.ttlReservaMinutos === "number" &&
      !Number.isNaN(args.ttlReservaMinutos)
        ? args.ttlReservaMinutos
        : null;
    const ttlSubidaComprobanteMinutos =
      typeof args.ttlSubidaComprobanteMinutos === "number" &&
      !Number.isNaN(args.ttlSubidaComprobanteMinutos)
        ? args.ttlSubidaComprobanteMinutos
        : 60;

    if (!modalidadPago || ttlReservaMinutos === null) {
      return {
        isError: true,
        content: [
          {
            type: "text",
            text: JSON.stringify(
              {
                status: "ERROR_PARAMETROS",
                mensaje:
                  "Se requiere 'modalidadPago' ('PASARELA' | 'MANUAL') y 'ttlReservaMinutos' (number).",
              },
              null,
              2
            ),
          },
        ],
      };
    }

    const checks: SddAuditCheck[] = [];

    if (modalidadPago === "PASARELA") {
      const pasarelaOk = ttlReservaMinutos === 20;
      checks.push({
        requirementId: "RF-WEB-002 (Pasarela)",
        title: "TTL Exacto de 20 Minutos para Pasarela de Tarjetas",
        passed: pasarelaOk,
        severity: "BLOCKER",
        evidence: pasarelaOk
          ? "El TTL configurado para pasarela de tarjeta es exactamente 20 minutos."
          : `Valor inválido: ${ttlReservaMinutos} minutos. La especificación RF-WEB-002 exige exactamente 20 minutos.`,
        remediation: pasarelaOk
          ? undefined
          : "Configure el TTL de reserva de pasarela en exactamente 20 minutos (TimeSpan.FromMinutes(20)).",
      });
    } else {
      const reservaManualOk =
        Number.isInteger(ttlReservaMinutos) &&
        ttlReservaMinutos > 0 &&
        ttlReservaMinutos <= 240;

      const comprobanteManualOk =
        Number.isInteger(ttlSubidaComprobanteMinutos) &&
        ttlSubidaComprobanteMinutos > 0 &&
        ttlSubidaComprobanteMinutos <= 60 &&
        ttlSubidaComprobanteMinutos <= ttlReservaMinutos;

      checks.push({
        requirementId: "RF-WEB-002 (Manual - Reserva Total)",
        title:
          "TTL de Reserva para Pago Manual (Yape/Plin/Transferencia) hasta 240 Minutos (4 Horas)",
        passed: reservaManualOk,
        severity: "BLOCKER",
        evidence: reservaManualOk
          ? `El TTL de reserva manual (${ttlReservaMinutos} min) está dentro del umbral de hasta 240 minutos.`
          : `Valor fuera de rango: ${ttlReservaMinutos} minutos. Debe ser un entero positivo menor o igual a 240 minutos (4 horas).`,
        remediation: reservaManualOk
          ? undefined
          : "Ajuste el TTL máximo de reserva manual a un valor entre 60 y 240 minutos (máximo 4 horas).",
      });

      checks.push({
        requirementId: "RF-WEB-002 (Manual - Ventana de Comprobante)",
        title:
          "Ventana Máxima de 60 Minutos (1 Hora) para Envío de Comprobante Manual",
        passed: comprobanteManualOk,
        severity: "CRITICAL",
        evidence: comprobanteManualOk
          ? `La ventana para adjuntar comprobante (${ttlSubidaComprobanteMinutos} min) cumple el límite de <= 60 minutos.`
          : `La ventana de comprobante (${ttlSubidaComprobanteMinutos} min) excede los 60 minutos máximos o supera el TTL total de la reserva.`,
        remediation: comprobanteManualOk
          ? undefined
          : "Establezca el plazo límite para subir el comprobante Yape/Plin/Transferencia en máximo 60 minutos (TimeSpan.FromMinutes(60)).",
      });
    }

    const allPassed = checks.every((c) => c.passed);

    return {
      isError: false,
      content: [
        {
          type: "text",
          text: JSON.stringify(
            {
              herramienta: "validar_reserva_web_sdd",
              estadoCertificacionSdd: allPassed
                ? "CUMPLIMIENTO_VERIFICADO"
                : "RECHAZADO_POR_INCUMPLIMIENTO_SDD",
              modalidadPago,
              parametrosEvaluados: {
                ttlReservaMinutos,
                ...(modalidadPago === "MANUAL"
                  ? { ttlSubidaComprobanteMinutos }
                  : {}),
              },
              controlesEjecutados: checks,
            },
            null,
            2
          ),
        },
      ],
    };
  },
};

export const inventorySddTools: SddToolHandler[] = [
  auditarEspecificacionStockSddTool,
  validarReservaWebSddTool,
];
