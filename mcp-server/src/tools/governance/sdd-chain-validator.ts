/**
 * sdd-chain-validator.ts — Herramienta de Gobernanza SDD
 * Validador dinámico de cadena documental SDD: escanea todos los archivos .md
 * con metadatos YAML frontmatter (doc, estado, depende-de, habilita, aprobado)
 * y construye el grafo de dependencias en tiempo de ejecución.
 */
import * as fs from "node:fs";
import * as path from "node:path";
import { CallToolResult } from "@modelcontextprotocol/sdk/types.js";
import { SddToolHandler } from "../types.js";
import { assertReadable, getProjectRoot } from "../../core/sandbox.js";
import { parseFrontmatter, extractHeadings } from "../../core/yaml-frontmatter.js";

interface SDDDocNode {
  filePath: string;
  relativePath: string;
  doc: string;
  estado: string;
  version: string;
  tipo: string;
  aprobado: boolean;
  dependeDe: string[];
  habilita: string[];
  headings: string[];
  issues: string[];
}

interface ValidationResult {
  doc: string;
  file: string;
  estado: string;
  version: string;
  aprobado: boolean;
  dependeDe: string[];
  habilita: string[];
  blockedBy: BlockingDep[];
  isBlocked: boolean;
  issues: string[];
  canProceed: boolean;
}

interface BlockingDep {
  doc: string;
  estado: string;
  reason: string;
}

/**
 * Encuentra recursivamente todos los archivos .md hasta 4 niveles de profundidad.
 */
function findMarkdownFiles(dir: string, depth: number = 0): string[] {
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
        !["node_modules", "dist", "build", "coverage", "bin", "obj", ".obsidian"].includes(
          entry.name
        )
      ) {
        results.push(...findMarkdownFiles(fullPath, depth + 1));
      }
    }
  } catch {
    // Omite directorios inaccesibles
  }

  return results;
}

/**
 * Escanea un directorio en busca de archivos .md con frontmatter SDD.
 */
function discoverSDDDocuments(projectPath: string): Map<string, SDDDocNode> {
  const docs = new Map<string, SDDDocNode>();
  const mdFiles = findMarkdownFiles(projectPath);

  for (const filePath of mdFiles) {
    try {
      const content = fs.readFileSync(filePath, "utf-8");
      const fm = parseFrontmatter(content);

      let docId = fm?.doc ? String(fm.doc).trim() : "";
      if (!docId && fm?.estado) {
        const base = path.basename(filePath, ".md").toLowerCase();
        if (base.includes("orquestador")) docId = "DOC-0";
        else if (base.includes("estado-actual") || base.includes("estado_actual"))
          docId = "ESTADO-ACTUAL";
      }
      if (!docId || !fm) continue;

      const issues: string[] = [];
      if (!fm.estado) issues.push("Falta 'estado' en frontmatter");

      const rawDepende = Array.isArray(fm["depende-de"])
        ? fm["depende-de"]
        : typeof fm["depende-de"] === "string"
        ? [fm["depende-de"]]
        : [];

      const dependeDe = rawDepende
        .map((d) => String(d).trim())
        .filter(
          (d) =>
            d &&
            !["ninguno", "ninguna", "none", "-", "null", "[]"].includes(
              d.toLowerCase()
            )
        );

      const rawHabilita = Array.isArray(fm.habilita)
        ? fm.habilita
        : typeof fm.habilita === "string"
        ? [fm.habilita]
        : [];

      const habilita = rawHabilita
        .map((h) => String(h).trim())
        .filter(
          (h) =>
            h &&
            !["ninguno", "ninguna", "none", "-", "null", "[]"].includes(
              h.toLowerCase()
            )
        );

      const rawAprobado = fm.aprobado as unknown;
      const aprobado =
        rawAprobado === true ||
        rawAprobado === "true" ||
        (typeof rawAprobado === "string" &&
          (rawAprobado.toLowerCase() === "sí" || rawAprobado.toLowerCase() === "si")) ||
        (typeof fm.estado === "string" &&
          (fm.estado.toLowerCase() === "aprobado" ||
            fm.estado.toLowerCase() === "completado"));

      docs.set(docId, {
        filePath,
        relativePath: path.relative(projectPath, filePath),
        doc: docId,
        estado: String(fm.estado ?? "desconocido"),
        version: String(fm.version ?? "0.0.0"),
        tipo: String(fm.tipo ?? "documento"),
        aprobado,
        dependeDe,
        habilita,
        headings: extractHeadings(content),
        issues,
      });
    } catch {
      // Ignora archivos ilegibles
    }
  }

  return docs;
}

/**
 * Ordenamiento topológico de documentos según sus dependencias.
 */
function topologicalSort(docs: Map<string, SDDDocNode>): string[] {
  const visited = new Set<string>();
  const visiting = new Set<string>();
  const sorted: string[] = [];

  function visit(id: string): void {
    if (visited.has(id)) return;
    if (visiting.has(id)) return; // Ciclo detectado, se maneja en validación

    visiting.add(id);
    const node = docs.get(id);
    if (node) {
      for (const dep of node.dependeDe) {
        if (docs.has(dep)) {
          visit(dep);
        }
      }
    }
    visiting.delete(id);
    visited.add(id);
    sorted.push(id);
  }

  for (const id of docs.keys()) {
    visit(id);
  }

  return sorted;
}

/**
 * Valida un nodo individual contra el mapa general de documentos.
 */
function validateDoc(
  node: SDDDocNode,
  allDocs: Map<string, SDDDocNode>,
  strictMode: boolean
): ValidationResult {
  const blockedBy: BlockingDep[] = [];
  const issues = [...node.issues];

  for (const depId of node.dependeDe) {
    const depNode = allDocs.get(depId);
    if (!depNode) {
      issues.push(`Dependencia huérfana: '${depId}' no existe en el repositorio.`);
      if (strictMode) {
        blockedBy.push({
          doc: depId,
          estado: "NO_ENCONTRADO",
          reason: `Documento de prerrequisito '${depId}' no encontrado`,
        });
      }
    } else if (!depNode.aprobado && strictMode) {
      blockedBy.push({
        doc: depId,
        estado: depNode.estado,
        reason: `Prerrequisito '${depId}' aún en estado '${depNode.estado}' (no aprobado)`,
      });
    }
  }

  // Verifica referencias inversas en habilita
  for (const enabledId of node.habilita) {
    const targetNode = allDocs.get(enabledId);
    if (targetNode && !targetNode.dependeDe.includes(node.doc)) {
      issues.push(
        `Inconsistencia: Este doc habilita '${enabledId}', pero '${enabledId}' no lista a '${node.doc}' en depende-de`
      );
    }
  }

  const isBlocked = blockedBy.length > 0;
  const canProceed = !isBlocked && issues.length === 0;

  return {
    doc: node.doc,
    file: node.relativePath,
    estado: node.estado,
    version: node.version,
    aprobado: node.aprobado,
    dependeDe: node.dependeDe,
    habilita: node.habilita,
    blockedBy,
    isBlocked,
    issues,
    canProceed,
  };
}

export const sddChainValidatorTool: SddToolHandler = {
  definition: {
    name: "sdd_chain_validator",
    description:
      "Escanea dinámicamente todos los archivos .md con metadatos SDD frontmatter (doc, estado, depende-de, habilita, aprobado) en el proyecto SIGIC y valida la integridad de la cadena de dependencias, reportando bloqueos, documentos huérfanos o inconsistencias.",
    inputSchema: {
      type: "object",
      properties: {
        projectPath: {
          type: "string",
          description:
            "Ruta del directorio del proyecto. Si se omite, opera sobre la raíz de 'SIGIC-arquitSoft-proyecto-final'.",
        },
        documentId: {
          type: "string",
          description:
            "Identificador del documento específico a validar (ej: 'DOC-1', 'PLAN-MIGRACION-SIGIC'). Si se omite, valida toda la cadena.",
        },
        strictMode: {
          type: "boolean",
          description:
            "Si es verdadero, bloquea documentos cuyos prerrequisitos no estén en estado aprobado. Por defecto: true.",
        },
      },
    },
  },
  execute: async (args: Record<string, unknown>): Promise<CallToolResult> => {
    try {
      const targetDir = typeof args.projectPath === "string" && args.projectPath.trim() !== ""
        ? assertReadable(args.projectPath)
        : getProjectRoot();

      if (!fs.existsSync(targetDir)) {
        return {
          isError: true,
          content: [
            {
              type: "text",
              text: JSON.stringify({
                error: `La ruta del proyecto no existe: ${targetDir}`,
              }),
            },
          ],
        };
      }

      const allDocs = discoverSDDDocuments(targetDir);

      if (allDocs.size === 0) {
        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(
                {
                  projectPath: targetDir,
                  warning:
                    "No se encontraron archivos .md con frontmatter SDD (campo doc:) en el directorio.",
                  sugerencia:
                    "Asegúrese de que los documentos incluyan frontmatter YAML con doc, estado, depende-de, habilita, aprobado.",
                },
                null,
                2
              ),
            },
          ],
        };
      }

      const documentId = typeof args.documentId === "string" ? args.documentId.trim() : undefined;
      const strictMode = typeof args.strictMode === "boolean" ? args.strictMode : true;

      const docsToValidate = documentId
        ? new Map([...allDocs.entries()].filter(([id]) => id === documentId))
        : allDocs;

      if (documentId && docsToValidate.size === 0) {
        return {
          isError: true,
          content: [
            {
              type: "text",
              text: JSON.stringify({
                error: `Documento '${documentId}' no encontrado en el proyecto.`,
                documentosDisponibles: Array.from(allDocs.keys()),
              }),
            },
          ],
        };
      }

      const sortedIds = topologicalSort(allDocs);
      const results: ValidationResult[] = [];

      for (const docId of sortedIds) {
        if (!docsToValidate.has(docId)) continue;
        const node = allDocs.get(docId)!;
        results.push(validateDoc(node, allDocs, strictMode));
      }

      const summary = {
        projectPath: targetDir,
        totalDocumentos: allDocs.size,
        validados: results.length,
        aprobados: results.filter((r) => r.aprobado).length,
        bloqueados: results.filter((r) => r.isBlocked).length,
        conObservaciones: results.filter((r) => r.issues.length > 0).length,
        estadoCadena:
          results.every((r) => r.canProceed)
            ? "✅ CADENA SDD SALUDABLE Y SIN BLOQUEOS"
            : `⚠️ ${results.filter((r) => !r.canProceed).length} documento(s) con bloqueos o problemas`,
      };

      return {
        content: [
          {
            type: "text",
            text: JSON.stringify({ summary, documentos: results }, null, 2),
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
              error: "Error ejecutando sdd_chain_validator",
              detalle: error instanceof Error ? error.message : String(error),
            }),
          },
        ],
      };
    }
  },
};
