import { Tool, CallToolResult } from "@modelcontextprotocol/sdk/types.js";
import { SddToolHandler } from "./types.js";
import { domainSddTools, inventorySddTools, posSddTools } from "./domain/index.js";
import {
  governanceSddTools,
  sddChainValidatorTool,
  architectureDriftAuditorTool,
  qualityGateEnforcerTool,
  obsidianVaultSyncTool,
} from "./governance/index.js";

/**
 * Registro unificado y extensible de las 7 herramientas MCP para SIGIC v2.2:
 * 3 Herramientas de Dominio Ferretero + 4 Herramientas de Gobernanza Arquitectónica y SDD.
 */
const allTools: SddToolHandler[] = [
  ...domainSddTools,
  ...governanceSddTools,
];

export const sddToolRegistry: ReadonlyMap<string, SddToolHandler> = new Map(
  allTools.map((handler) => [handler.definition.name, handler])
);

/**
 * Devuelve las definiciones JSON Schema de las 7 herramientas registradas.
 */
export function listRegisteredSddTools(): Tool[] {
  return Array.from(sddToolRegistry.values()).map((item) => item.definition);
}

/**
 * Enruta y ejecuta de forma segura una herramienta MCP por su nombre.
 */
export async function invokeRegisteredSddTool(
  toolName: string,
  args: Record<string, unknown> = {}
): Promise<CallToolResult> {
  const handler = sddToolRegistry.get(toolName);

  if (!handler) {
    return {
      isError: true,
      content: [
        {
          type: "text",
          text: JSON.stringify(
            {
              error: `La herramienta MCP '${toolName}' no existe en sigic-sdd-mcp-server.`,
              herramientasDisponibles: Array.from(sddToolRegistry.keys()),
            },
            null,
            2
          ),
        },
      ],
    };
  }

  try {
    return await handler.execute(args);
  } catch (error) {
    const message =
      error instanceof Error ? error.message : "Error desconocido en la ejecución de la tool.";
    return {
      isError: true,
      content: [
        {
          type: "text",
          text: JSON.stringify(
            {
              error: `Fallo interno al ejecutar '${toolName}': ${message}`,
            },
            null,
            2
          ),
        },
      ],
    };
  }
}

export {
  domainSddTools,
  governanceSddTools,
  inventorySddTools,
  posSddTools,
  sddChainValidatorTool,
  architectureDriftAuditorTool,
  qualityGateEnforcerTool,
  obsidianVaultSyncTool,
  SddToolHandler,
};
