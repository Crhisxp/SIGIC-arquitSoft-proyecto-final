import { CallToolResult } from "@modelcontextprotocol/sdk/types.js";
import { SddToolHandler, SddAuditCheck } from "./inventoryTools.js";

/**
 * Tool 3: validar_ergonomia_pos_sdd
 * Analiza componentes React 18 (TSX/JSX) del Mostrador POS verificando:
 * - RF-POS-003: Soporte nativo de teclado (keydown / hotkeys F1-F12, Enter, Escape) y ausencia de clics de ratón sin atajo o acción de teclado equivalente.
 * - RNF-USA-002: Contención en resolución 1024x768 sin scroll horizontal y protección de textos largos de pernería/tornillería.
 */
export const validarErgonomiaPosSddTool: SddToolHandler = {
  definition: {
    name: "validar_ergonomia_pos_sdd",
    description:
      "Audita componentes React 18 (TSX/JSX) del Mostrador POS de SIGIC v2.2 para certificar el cumplimiento de RF-POS-003 (operación 100% por teclado y escáner, manejadores keydown/hotkeys F1-F12/Enter/Escape y prohibición de interacciones que dependan exclusivamente del ratón) y RNF-USA-002 (layout 1024x768 sin desbordamiento horizontal).",
    inputSchema: {
      type: "object",
      properties: {
        codigoComponenteReact: {
          type: "string",
          description:
            "Código fuente completo del componente o hook en React 18 (TypeScript/TSX) destinado al canal POS.",
        },
        esPantallaDespachoWeb: {
          type: "boolean",
          description:
            "Indica si el componente corresponde a la pantalla ligera de entrega de pedidos web en tienda (RF-POS-011).",
        },
      },
      required: ["codigoComponenteReact"],
    },
  },
  execute: async (args: Record<string, unknown>): Promise<CallToolResult> => {
    const codigo =
      typeof args.codigoComponenteReact === "string"
        ? args.codigoComponenteReact
        : "";
    const esPantallaDespachoWeb = Boolean(args.esPantallaDespachoWeb);

    if (!codigo.trim()) {
      return {
        isError: true,
        content: [
          {
            type: "text",
            text: JSON.stringify(
              {
                status: "ERROR_PARAMETROS",
                mensaje:
                  "El parámetro 'codigoComponenteReact' no puede estar vacío.",
              },
              null,
              2
            ),
          },
        ],
      };
    }

    const checks: SddAuditCheck[] = [];

    // 1. Verificación RF-POS-003: Manejadores explícitos de teclado (keydown / hotkeys)
    const hasKeyboardListeners =
      /onKeyDown|addEventListener\s*\(\s*["']keydown["']|useHotkeys|usePosKeyboard/i.test(
        codigo
      );
    const handlesFunctionOrActionKeys =
      /F[1-9]|F1[0-2]|Escape|Enter|ArrowUp|ArrowDown/i.test(codigo);

    const passedKeyboardNavigation =
      hasKeyboardListeners && handlesFunctionOrActionKeys;

    checks.push({
      requirementId: "RF-POS-003 (Manejadores de Teclado)",
      title:
        "Soporte Nativo de Navegación por Teclado y Teclas de Función (F1-F12, Enter, Escape, Flechas)",
      passed: passedKeyboardNavigation,
      severity: "BLOCKER",
      evidence: passedKeyboardNavigation
        ? "El componente implementa escuchas de teclado ('keydown' / 'onKeyDown') y procesa teclas operativas (F1-F12 / Enter / Escape / ArrowKeys)."
        : `Deficiencia de teclado detectada: [Listener keydown presente: ${
            hasKeyboardListeners ? "SÍ" : "NO"
          }, Mapeo de teclas F1-F12/Enter/Escape: ${
            handlesFunctionOrActionKeys ? "SÍ" : "NO"
          }].`,
      remediation: passedKeyboardNavigation
        ? undefined
        : "Agregue un listener 'window.addEventListener(\"keydown\", ...)' o manejador 'onKeyDown' que capture teclas F1-F12, Enter, Escape y flechas direccionales con 'event.preventDefault()'.",
    });

    // 2. Verificación RF-POS-003: Prohibición de elementos con clic obligatorio en contenedores no enfocables
    const hasNonAccessibleClickTarget =
      /<(div|span|td|li)\b[^>]*onClick=/i.test(codigo) &&
      !/tabIndex=\{?0\}?[\s\S]*onKeyDown=/i.test(codigo);

    const passedMouseFreeOperation =
      !hasNonAccessibleClickTarget && hasKeyboardListeners;

    checks.push({
      requirementId: "RF-POS-003 (Cero Dependencia de Ratón)",
      title: "Ausencia de Interacciones Exclusivas con Ratón",
      passed: passedMouseFreeOperation,
      severity: "BLOCKER",
      evidence: passedMouseFreeOperation
        ? "Todas las acciones interactivas son alcanzables por teclado o atajos directos sin requerir el ratón."
        : "Se detectaron elementos no interactivos (<div/span/td onClick=...>) sin soporte de foco (tabIndex) ni comando de teclado equivalente.",
      remediation: passedMouseFreeOperation
        ? undefined
        : "Sustituya cualquier '<div onClick=...>' por elementos '<button type=\"button\">' asociados a un atajo de teclado (F1-F12/Enter) o incorpore 'tabIndex={0}' y 'onKeyDown'.",
    });

    // 3. Verificación RNF-USA-002: Ergonomía 1024x768, sin scroll horizontal y protección de descripciones largas
    const preventsHorizontalOverflow =
      /overflow-hidden|overflow-x-hidden|max-w-[1024px]|w-screen/i.test(
        codigo
      );
    const handlesLongHardwareDescriptions =
      /line-clamp-2|flex-shrink-0|shrink-0|tabular-nums|tabular-data/i.test(
        codigo
      );

    const passedViewportErgonomics =
      preventsHorizontalOverflow && handlesLongHardwareDescriptions;

    checks.push({
      requirementId: "RNF-USA-002",
      title:
        "Ergonomía en Resolución 1024x768 y Legibilidad de Nombres Extensos de Tornillería/Pernos",
      passed: passedViewportErgonomics,
      severity: "CRITICAL",
      evidence: passedViewportErgonomics
        ? "El layout previene el scroll horizontal ('overflow-hidden' / 'overflow-x-hidden') y aplica reglas de densidad ('line-clamp-2', 'shrink-0', 'tabular-nums')."
        : `Faltan garantías visuales para 1024x768: [Prevención de scroll horizontal: ${
            preventsHorizontalOverflow ? "SÍ" : "NO"
          }, Control de textos largos y números tabulares: ${
            handlesLongHardwareDescriptions ? "SÍ" : "NO"
          }].`,
      remediation: passedViewportErgonomics
        ? undefined
        : "Aplique 'overflow-x-hidden' en el contenedor raíz, 'line-clamp-2' para descripciones técnicas extensas, 'shrink-0' en el badge de medidas del perno/herramienta y 'tabular-nums' en precios y stocks.",
    });

    // 4. Verificación específica para RF-POS-011 si es la pantalla ligera de despacho web
    if (esPantallaDespachoWeb) {
      const referencesInternalGuide =
        /guiaInterna|guia_interna|GuiaTraslado|codigoRetiro|ENTREGADO/i.test(
          codigo
        );

      checks.push({
        requirementId: "RF-POS-011",
        title:
          "Pantalla Ligera de Despacho en Tienda con Validación de Guía Interna (RF-INV-003)",
        passed: referencesInternalGuide,
        severity: "CRITICAL",
        evidence: referencesInternalGuide
          ? "La vista de despacho valida la guía interna de traslado o código de retiro y permite confirmar la entrega por teclado/escáner."
          : "La vista de despacho web no muestra ni valida el estado de la Guía Interna de Traslado proveniente del Almacén Central.",
        remediation: referencesInternalGuide
          ? undefined
          : "Incluya en el componente la lectura por escáner del código de pedido/Guía Interna ('guiaInterna') y la confirmación de estado 'ENTREGADO' mediante la tecla Enter.",
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
              herramienta: "validar_ergonomia_pos_sdd",
              estadoCertificacionSdd: allPassed
                ? "CUMPLIMIENTO_VERIFICADO"
                : "RECHAZADO_POR_INCUMPLIMIENTO_SDD",
              esPantallaDespachoWeb,
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

export const posSddTools: SddToolHandler[] = [validarErgonomiaPosSddTool];
