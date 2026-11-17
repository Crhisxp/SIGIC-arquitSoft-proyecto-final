/**
 * obsidian-vault-sync.ts — Herramienta de Gobernanza SDD
 * Sincroniza el estado de la cadena documental SDD y los subgrafos arquitectónicos
 * hacia notas Markdown enriquecidas para Obsidian y GitHub.
 *
 * REGLA ESTRICTA DE COMPATIBILIDAD (AGENTS.md / GitHub):
 * Genera enlaces Markdown relativos estándar '[Texto](ruta.md)' en lugar de '[[wikilinks]]'.
 */
import * as fs from "node:fs";
import * as path from "node:path";
import { CallToolResult } from "@modelcontextprotocol/sdk/types.js";
import { SddToolHandler } from "../types.js";
import { assertReadable, assertWritable, getProjectRoot } from "../../core/sandbox.js";
import { parseFrontmatter } from "../../core/yaml-frontmatter.js";

interface GraphNode {
  id: string;
  type: string;
  name: string;
  file?: string;
  [key: string]: unknown;
}

interface GraphEdge {
  source: string;
  target: string;
  type: string;
  [key: string]: unknown;
}

interface GraphData {
  nodes: GraphNode[];
  edges: GraphEdge[];
  metadata?: Record<string, unknown>;
}

function sanitizeFilename(name: string): string {
  return name.replace(/[<>:"/\\|?*]/g, "_").replace(/\s+/g, "-");
}

function findMdFiles(dir: string, depth: number = 0): string[] {
  if (depth > 4) return [];
  const results: string[] = [];

  try {
    const entries = fs.readdirSync(dir, { withFileTypes: true });
    for (const entry of entries) {
      const fullPath = path.join(dir, entry.name);
      if (entry.isFile() && entry.name.endsWith(".md")) {
        results.push(fullPath);
      } else if (
        entry.isDirectory() &&
        !entry.name.startsWith(".") &&
        ![
          "node_modules",
          "dist",
          "build",
          "coverage",
          "bin",
          "obj",
          "obsidian-sync",
          ".obsidian",
        ].includes(entry.name)
      ) {
        results.push(...findMdFiles(fullPath, depth + 1));
      }
    }
  } catch {
    // Omite directorios inaccesibles
  }

  return results;
}

function generateSDDNote(
  docId: string,
  fm: Record<string, unknown>,
  relativePath: string,
  projectId: string
): string {
  const estado = String(fm.estado ?? "desconocido");
  const version = String(fm.version ?? "0.0.0");
  const tipo = String(fm.tipo ?? "documento");
  const aprobado = fm.aprobado === true || fm.aprobado === "true";
  const dependeDe = Array.isArray(fm["depende-de"]) ? fm["depende-de"] : [];
  const habilita = Array.isArray(fm.habilita) ? fm.habilita : [];

  // Enlaces Markdown estándar [Texto](ruta.md) sin wikilinks
  const depLinks = dependeDe
    .map((d: string) => `  - [${d}](./${projectId}-${sanitizeFilename(d)}.md)`)
    .join("\n");
  const enableLinks = habilita
    .map((h: string) => `  - [${h}](./${projectId}-${sanitizeFilename(h)}.md)`)
    .join("\n");

  const statusEmoji =
    estado === "aprobado"
      ? "✅"
      : estado.includes("progreso")
      ? "🔄"
      : estado.includes("revisión") || estado.includes("revision")
      ? "🔍"
      : estado === "bloqueado"
      ? "🚫"
      : "❓";

  return `---
doc: "${docId}"
proyecto: "${projectId}"
estado: "${estado}"
version: "${version}"
tipo: "${tipo}"
aprobado: ${aprobado}
tags:
  - sdd
  - ${projectId}
  - ${estado.replace(/\s+/g, "-")}
source-file: "${relativePath}"
synced-at: "${new Date().toISOString()}"
---

# ${statusEmoji} ${docId} — ${tipo}

**Proyecto:** [${projectId}](./${projectId}-SDD-Index.md)  
**Estado:** \`${estado}\` ${aprobado ? "✅ Aprobado" : "⏳ Pendiente"}  
**Versión:** ${version}  
**Archivo fuente original:** [\`${relativePath}\`](../${relativePath})  

## Dependencias (depende-de)
${depLinks || "  - _Ninguna (documento raíz)_"}

## Habilita
${enableLinks || "  - _Ninguno_"}
`;
}

function generateGraphNote(
  node: GraphNode,
  edges: GraphEdge[],
  projectId: string
): string {
  const incoming = edges
    .filter((e) => e.target === node.id)
    .map((e) => `  - [${sanitizeFilename(e.source)}](./${sanitizeFilename(e.source)}.md) ← (${e.type})`)
    .join("\n");
  const outgoing = edges
    .filter((e) => e.source === node.id)
    .map((e) => `  - [${sanitizeFilename(e.target)}](./${sanitizeFilename(e.target)}.md) → (${e.type})`)
    .join("\n");

  return `---
node-id: "${node.id}"
node-type: "${node.type}"
proyecto: "${projectId}"
tags:
  - graph-node
  - ${projectId}
  - ${node.type}
synced-at: "${new Date().toISOString()}"
---

# 🔗 ${node.name}

**Tipo:** \`${node.type}\`  
**Archivo:** \`${node.file ?? "N/A"}\`  

## Dependencias Entrantes
${incoming || "  - _Ninguna_"}

## Dependencias Salientes
${outgoing || "  - _Ninguna_"}
`;
}

export const obsidianVaultSyncTool: SddToolHandler = {
  definition: {
    name: "obsidian_vault_sync",
    description:
      "Sincroniza el estado documental SDD y los nodos del grafo en notas Markdown compatibles con Obsidian y GitHub, utilizando metadatos YAML y enlaces relativos estándar [Texto](ruta.md) sin sintaxis de wikilinks.",
    inputSchema: {
      type: "object",
      properties: {
        projectPath: {
          type: "string",
          description:
            "Ruta del proyecto. Si se omite, opera sobre la raíz de 'SIGIC-arquitSoft-proyecto-final'.",
        },
        vaultPath: {
          type: "string",
          description:
            "Directorio destino para las notas sincronizadas. Si se omite, utiliza 'obsidian-sync/' dentro de la raíz del proyecto.",
        },
        syncMode: {
          type: "string",
          enum: ["sdd-only", "graph-only", "full"],
          description:
            "'sdd-only': sincroniza solo documentos SDD. 'graph-only': sincroniza nodos del grafo. 'full': sincroniza ambos. Por defecto: 'full'.",
        },
        maxGraphNodes: {
          type: "number",
          description:
            "Límite máximo de nodos de grafo a procesar. Por defecto: 100.",
        },
      },
    },
  },
  execute: async (args: Record<string, unknown>): Promise<CallToolResult> => {
    try {
      const projectRoot =
        typeof args.projectPath === "string" && args.projectPath.trim() !== ""
          ? assertReadable(args.projectPath)
          : getProjectRoot();

      const activeProjectId = path.basename(projectRoot);

      const targetVault =
        typeof args.vaultPath === "string" && args.vaultPath.trim() !== ""
          ? args.vaultPath.trim()
          : path.join(projectRoot, "obsidian-sync");

      const vaultDir = path.isAbsolute(targetVault)
        ? assertWritable(targetVault)
        : assertWritable(path.join(projectRoot, targetVault));

      const syncMode =
        typeof args.syncMode === "string" &&
        ["sdd-only", "graph-only", "full"].includes(args.syncMode)
          ? args.syncMode
          : "full";

      const maxGraphNodes =
        typeof args.maxGraphNodes === "number" && args.maxGraphNodes > 0
          ? args.maxGraphNodes
          : 100;

      const stats = {
        projectId: activeProjectId,
        projectRoot,
        vaultDestination: vaultDir,
        sddNotesCreated: 0,
        graphNotesCreated: 0,
        errors: [] as string[],
      };

      // 1. Sincronización de Documentos SDD
      if (syncMode === "sdd-only" || syncMode === "full") {
        const sddDir = path.join(vaultDir, "SDD-Chain");
        assertWritable(sddDir);
        fs.mkdirSync(sddDir, { recursive: true });

        const mdFiles = findMdFiles(projectRoot);

        for (const filePath of mdFiles) {
          try {
            const content = fs.readFileSync(filePath, "utf-8");
            const fm = parseFrontmatter(content);
            if (!fm || !fm.doc) continue;

            const docId = String(fm.doc);
            const relativePath = path.relative(projectRoot, filePath);
            const noteContent = generateSDDNote(
              docId,
              fm as Record<string, unknown>,
              relativePath,
              activeProjectId
            );

            const noteFilename = `${activeProjectId}-${sanitizeFilename(docId)}.md`;
            const notePath = path.join(sddDir, noteFilename);
            assertWritable(notePath);
            fs.writeFileSync(notePath, noteContent, "utf-8");
            stats.sddNotesCreated++;
          } catch (err) {
            stats.errors.push(
              `SDD: ${path.relative(projectRoot, filePath)}: ${
                err instanceof Error ? err.message : String(err)
              }`
            );
          }
        }

        // Generar Índice General SDD
        const indexLines: string[] = [];
        for (const f of mdFiles) {
          try {
            const content = fs.readFileSync(f, "utf-8");
            const fm = parseFrontmatter(content);
            if (fm && fm.doc) {
              const docId = String(fm.doc);
              indexLines.push(
                `- [${docId}](./${activeProjectId}-${sanitizeFilename(docId)}.md) — \`${fm.estado ?? "?"}\``
              );
            }
          } catch {
            // Ignora
          }
        }

        const indexContent = `---
proyecto: "${activeProjectId}"
tipo: "indice-sdd"
tags:
  - sdd
  - ${activeProjectId}
  - index
synced-at: "${new Date().toISOString()}"
---

# 📋 Índice SDD — ${activeProjectId}

${indexLines.length > 0 ? indexLines.join("\n") : "_No se encontraron documentos con frontmatter SDD en el proyecto._"}
`;
        const indexPath = path.join(sddDir, `${activeProjectId}-SDD-Index.md`);
        assertWritable(indexPath);
        fs.writeFileSync(indexPath, indexContent, "utf-8");
      }

      // 2. Sincronización de Nodos del Grafo (si existe graphify-out/graph.json)
      if (syncMode === "graph-only" || syncMode === "full") {
        const graphPath = path.join(projectRoot, "graphify-out", "graph.json");

        if (fs.existsSync(graphPath)) {
          const graphDir = path.join(vaultDir, "Graph-Nodes");
          assertWritable(graphDir);
          fs.mkdirSync(graphDir, { recursive: true });

          const rawGraph = fs.readFileSync(graphPath, "utf-8");
          const graph: GraphData = JSON.parse(rawGraph);
          const nodesToSync = (graph.nodes || []).slice(0, maxGraphNodes);

          for (const node of nodesToSync) {
            try {
              const relatedEdges = (graph.edges || []).filter(
                (e) => e.source === node.id || e.target === node.id
              );
              const noteContent = generateGraphNote(
                node,
                relatedEdges,
                activeProjectId
              );
              const noteFilename = `${sanitizeFilename(node.id)}.md`;
              const notePath = path.join(graphDir, noteFilename);
              assertWritable(notePath);
              fs.writeFileSync(notePath, noteContent, "utf-8");
              stats.graphNotesCreated++;
            } catch (err) {
              stats.errors.push(
                `Grafo: ${node.id}: ${
                  err instanceof Error ? err.message : String(err)
                }`
              );
            }
          }
        }
      }

      return {
        content: [
          {
            type: "text",
            text: JSON.stringify(stats, null, 2),
          },
        ],
      };
    } catch (error) {
      return {
        isError: true,
        content: [
          {
            type: "text",
            text: JSON.stringify({
              error: "Error en sincronización hacia vault Obsidian",
              detalle: error instanceof Error ? error.message : String(error),
            }),
          },
        ],
      };
    }
  },
};
