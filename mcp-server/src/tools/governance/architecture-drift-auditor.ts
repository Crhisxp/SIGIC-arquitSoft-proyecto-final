/**
 * architecture-drift-auditor.ts — Herramienta de Gobernanza SDD
 * Audita la coherencia arquitectónica y el cumplimiento estricto de Clean Architecture.
 * Detecta violaciones de capas e importaciones prohibidas (ej: Dominio importando Infraestructura).
 */
import * as fs from "node:fs";
import * as path from "node:path";
import { CallToolResult } from "@modelcontextprotocol/sdk/types.js";
import { SddToolHandler } from "../types.js";
import { assertReadable, getProjectRoot } from "../../core/sandbox.js";

interface LayerRules {
  allowedImports: string[];
  forbiddenImports: string[];
}

const DEFAULT_LAYERS: Record<string, LayerRules> = {
  domain: {
    allowedImports: [],
    forbiddenImports: [
      "infrastructure",
      "presentation",
      "api",
      "controllers",
      "database",
      "prisma",
      "express",
      "fastify",
      "sequelize",
      "typeorm",
      "mongoose",
      "knex",
      "efcore",
      "entityframeworkcore",
      "sigic.infrastructure",
      "sigic.webapi",
      "sigic.pos",
      "sigic.ecommerce",
    ],
  },
  application: {
    allowedImports: ["domain"],
    forbiddenImports: [
      "infrastructure",
      "presentation",
      "database",
      "prisma",
      "express",
      "fastify",
      "sigic.infrastructure",
      "sigic.webapi",
    ],
  },
  infrastructure: {
    allowedImports: ["domain", "application"],
    forbiddenImports: [],
  },
  presentation: {
    allowedImports: ["domain", "application"],
    forbiddenImports: ["infrastructure", "database", "sigic.infrastructure"],
  },
};

interface DriftViolation {
  file: string;
  layer: string;
  line: number;
  importStatement: string;
  violationType: string;
  description: string;
}

function detectLayer(filePath: string): string | null {
  const normalized = filePath.replace(/\\/g, "/").toLowerCase();
  for (const layer of Object.keys(DEFAULT_LAYERS)) {
    if (
      normalized.includes(`/${layer}/`) ||
      normalized.includes(`/src/${layer}/`) ||
      normalized.includes(`.${layer}/`) ||
      normalized.includes(`sigic.${layer}/`) ||
      normalized.includes(`sigic.${layer}.`)
    ) {
      return layer;
    }
  }
  return null;
}

function walkSourceFiles(
  dir: string,
  includeTests: boolean
): string[] {
  const results: string[] = [];
  if (!fs.existsSync(dir)) return results;

  function walk(current: string): void {
    try {
      const entries = fs.readdirSync(current, { withFileTypes: true });
      for (const entry of entries) {
        const fullPath = path.join(current, entry.name);
        if (
          entry.isDirectory() &&
          !entry.name.startsWith(".") &&
          !["node_modules", "dist", "build", "coverage", "bin", "obj", ".git"].includes(
            entry.name
          )
        ) {
          walk(fullPath);
        } else if (
          entry.isFile() &&
          /\.(cs|ts|tsx|js|jsx|java)$/i.test(entry.name)
        ) {
          if (!includeTests && /\.(test|spec|Tests)\./i.test(entry.name)) {
            continue;
          }
          results.push(fullPath);
        }
      }
    } catch {
      // Omite directorios inaccesibles
    }
  }

  walk(dir);
  return results;
}

function analyzeImports(
  filePath: string,
  layer: string,
  projectRoot: string
): DriftViolation[] {
  const violations: DriftViolation[] = [];
  try {
    const content = fs.readFileSync(filePath, "utf-8");
    const lines = content.split("\n");

    const importRegex =
      /(?:import\s+.*?from\s+['"](.+?)['"]|require\s*\(\s*['"](.+?)['"]\s*\)|using\s+([A-Za-z0-9._]+);)/;

    for (let i = 0; i < lines.length; i++) {
      const match = lines[i].match(importRegex);
      if (!match) continue;

      const importPath = (match[1] || match[2] || match[3] || "").toLowerCase();
      const rules = DEFAULT_LAYERS[layer];
      if (!rules) continue;

      for (const forbidden of rules.forbiddenImports) {
        if (importPath.includes(forbidden)) {
          violations.push({
            file: path.relative(projectRoot, filePath),
            layer,
            line: i + 1,
            importStatement: lines[i].trim(),
            violationType: "forbidden_import",
            description: `Capa '${layer}' importa desde '${forbidden}' — Violación de Clean Architecture`,
          });
        }
      }
    }
  } catch {
    // Omite archivos ilegibles
  }

  return violations;
}

export const architectureDriftAuditorTool: SddToolHandler = {
  definition: {
    name: "architecture_drift_auditor",
    description:
      "Audita la coherencia arquitectónica y Clean Architecture de SIGIC. Detecta violaciones de límites de capas (ej: Domain importando Infrastructure o WebApi), importaciones prohibidas y emite un informe de salud arquitectónica.",
    inputSchema: {
      type: "object",
      properties: {
        projectPath: {
          type: "string",
          description:
            "Ruta del proyecto a auditar. Si se omite, opera sobre la raíz de 'SIGIC-arquitSoft-proyecto-final'.",
        },
        targetPath: {
          type: "string",
          description:
            "Subdirectorio específico a escanear (relativo a la raíz, ej: 'src'). Por defecto: 'src'.",
        },
        includeTests: {
          type: "boolean",
          description:
            "Si es verdadero, también audita archivos de prueba. Por defecto: false.",
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

      const targetSub =
        typeof args.targetPath === "string" && args.targetPath.trim() !== ""
          ? args.targetPath.trim()
          : "src";

      const scanDir = path.isAbsolute(targetSub)
        ? assertReadable(targetSub)
        : assertReadable(path.join(projectRoot, targetSub));

      const includeTests =
        typeof args.includeTests === "boolean" ? args.includeTests : false;

      if (!fs.existsSync(scanDir)) {
        return {
          isError: true,
          content: [
            {
              type: "text",
              text: JSON.stringify({
                error: `Directorio de escaneo no encontrado: ${scanDir}`,
              }),
            },
          ],
        };
      }

      const files = walkSourceFiles(scanDir, includeTests);
      const allViolations: DriftViolation[] = [];
      const layerStats: Record<string, number> = {};

      for (const file of files) {
        const layer = detectLayer(file);
        if (layer) {
          layerStats[layer] = (layerStats[layer] || 0) + 1;
          const violations = analyzeImports(file, layer, projectRoot);
          allViolations.push(...violations);
        }
      }

      const summary = {
        projectPath: projectRoot,
        scanDirectory: path.relative(projectRoot, scanDir) || ".",
        totalArchivosEscaneados: files.length,
        archivosConCapaIdentificada: Object.values(layerStats).reduce(
          (a, b) => a + b,
          0
        ),
        distribucionPorCapas: layerStats,
        totalViolaciones: allViolations.length,
        saludArquitectonica:
          allViolations.length === 0
            ? "✅ ARQUITECTURA LIMPIA — Sin violaciones de capas detectadas"
            : `⚠️ ${allViolations.length} VIOLACIÓN(ES) DE CAPA DETECTADA(S)`,
      };

      return {
        content: [
          {
            type: "text",
            text: JSON.stringify(
              { summary, violaciones: allViolations.slice(0, 50) },
              null,
              2
            ),
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
              error: "Error ejecutando architecture_drift_auditor",
              detalle: error instanceof Error ? error.message : String(error),
            }),
          },
        ],
      };
    }
  },
};
