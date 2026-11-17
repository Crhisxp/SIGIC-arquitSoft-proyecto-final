#!/usr/bin/env node
import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
  ErrorCode,
  McpError,
} from "@modelcontextprotocol/sdk/types.js";
import {
  listRegisteredSddTools,
  invokeRegisteredSddTool,
} from "./tools/index.js";

/**
 * Servidor MCP de Gobernanza SDD para SIGIC Ferretería v2.2.
 * Opera sobre transporte estándar de entrada/salida (Stdio).
 */
class SigicSddMcpServer {
  private readonly server: Server;

  constructor() {
    this.server = new Server(
      {
        name: "sigic-sdd-mcp-server",
        version: "2.2.0",
      },
      {
        capabilities: {
          tools: {},
        },
      }
    );

    this.registerHandlers();
    this.registerSignalAndErrorHandlers();
  }

  private registerHandlers(): void {
    this.server.setRequestHandler(ListToolsRequestSchema, async () => {
      return {
        tools: listRegisteredSddTools(),
      };
    });

    this.server.setRequestHandler(CallToolRequestSchema, async (request) => {
      const toolName = request.params.name;
      const rawArgs = request.params.arguments;

      if (!toolName || typeof toolName !== "string") {
        throw new McpError(
          ErrorCode.InvalidParams,
          "El nombre de la herramienta ('name') es obligatorio."
        );
      }

      const safeArgs: Record<string, unknown> =
        rawArgs && typeof rawArgs === "object" && !Array.isArray(rawArgs)
          ? (rawArgs as Record<string, unknown>)
          : {};

      return await invokeRegisteredSddTool(toolName, safeArgs);
    });
  }

  private registerSignalAndErrorHandlers(): void {
    this.server.onerror = (error) => {
      console.error("[SIGIC-SDD-MCP Error]:", error);
    };

    const gracefulShutdown = async (signal: string): Promise<void> => {
      console.error(
        `[SIGIC-SDD-MCP] Señal ${signal} recibida. Cerrando servidor...`
      );
      await this.server.close();
      process.exit(0);
    };

    process.on("SIGINT", () => {
      void gracefulShutdown("SIGINT");
    });

    process.on("SIGTERM", () => {
      void gracefulShutdown("SIGTERM");
    });
  }

  public async start(): Promise<void> {
    const transport = new StdioServerTransport();
    await this.server.connect(transport);
    console.error(
      "[SIGIC-SDD-MCP] Servidor v2.2 activo en transporte Stdio."
    );
  }
}

const app = new SigicSddMcpServer();
app.start().catch((err) => {
  console.error("[SIGIC-SDD-MCP] Error fatal de arranque:", err);
  process.exit(1);
});
