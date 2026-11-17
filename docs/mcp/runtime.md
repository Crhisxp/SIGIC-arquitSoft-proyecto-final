---
doc: "docs/mcp/runtime.md"
tipo: "contrato"
estado: "DOCUMENTADO"
aprobado: false
---

# Runtime del Servidor MCP — SIGIC Ferretería v2.2

Este documento describe el punto de entrada y el mecanismo de despacho de herramientas del servidor MCP.

## 1. Arquitectura de Ejecución

El proceso del servidor se inicializa en [server.ts](../../mcp-server/src/server.ts):
- Instancia la clase `Server` del SDK oficial `@modelcontextprotocol/sdk`.
- Conecta el transporte `StdioServerTransport` para comunicarse mediante flujos estándar de entrada y salida.
- Registra manejadores para las solicitudes `tools/list` (`ListToolsRequestSchema`) y `tools/call` (`CallToolRequestSchema`).
- Despacha las invocaciones al registro unificado central en [index.ts](../../mcp-server/src/tools/index.ts).

## 2. Ciclo de Vida y Advertencias Operativas

Es fundamental distinguir tres fases independientes:
1. **Arranque del Proceso:** Ejecución del binario Node.js sobre `./mcp-server/build/server.js`.
2. **Conexión MCP:** Negociación y handshake de capacidades entre el cliente y el servidor.
3. **Ejecución de Herramientas:** Procesamiento individual de llamadas a cada herramienta.

En auditorías previas se documentaron cierres prematuros de conexión bajo ciertas condiciones de invocación. Dicho comportamiento no debe darse por resuelto sin una comprobación empírica dedicada. Esta fase documental no requiere arrancar el servidor MCP.

Para la estructura de retrocompatibilidad de rutas, véase [compatibilidad.md](compatibilidad.md).
