/**
 * quality-gate-enforcer.ts — Herramienta de Gobernanza SDD
 * Verifica Restricciones No Negociables de Calidad (RNT):
 * - Cobertura de pruebas unitarias >= 95% en áreas críticas (Domain / Application / Core).
 * - Calidad estructural y contención de archivos (límite de líneas).
 * - Emisión de veredicto vinculante GO / NO-GO para despliegue.
 */
import * as fs from "node:fs";
import * as path from "node:path";
import { CallToolResult } from "@modelcontextprotocol/sdk/types.js";
import { SddToolHandler } from "../types.js";
import { assertReadable, getProjectRoot } from "../../core/sandbox.js";

interface CoverageEntry {
  statements?: { pct: number };
  branches?: { pct: number };
  functions?: { pct: number };
  lines?: { pct: number };
}

interface CoverageData {
  total: CoverageEntry;
  [filePath: string]: CoverageEntry;
}

interface DomainUnitCoverage {
  assembly: "SIGIC.Domain";
  suite: "Domain";
  covered: number;
  valid: number;
}

const DEFAULT_THRESHOLDS = {
  coverageMinimum: 95,
  criticalAreas: ["domain", "application", "core"],
  maxFileLines: 300,
};

function findCoverageReport(
  projectRoot: string
): { path: string; data: CoverageData; domain?: DomainUnitCoverage } | null {
  const currentPath = path.join(projectRoot, "TestResults", "coverage-summary.json");
  if (fs.existsSync(currentPath)) {
    const { gate } = JSON.parse(fs.readFileSync(currentPath, "utf-8"));
    if (gate?.assembly !== "SIGIC.Domain" || gate.suite !== "Domain" ||
        !Number.isInteger(gate.covered) || !Number.isInteger(gate.valid) ||
        gate.valid <= 0 || gate.covered < 0 || gate.covered > gate.valid) {
      throw new Error("TestResults/coverage-summary.json no contiene cobertura válida de la suite unitaria Domain.");
    }
    return {
      path: currentPath,
      data: { total: { lines: { pct: 100 * gate.covered / gate.valid } } },
      domain: gate,
    };
  }
  // An initialized evidence directory represents the current run, even before it completes.
  if (fs.existsSync(path.dirname(currentPath))) return null;

  const candidates = [
    path.join(projectRoot, "coverage", "coverage-summary.json"),
    path.join(projectRoot, "coverage", "coverage-final.json"),
    path.join(projectRoot, "test-results", "coverage-summary.json"),
  ];

  for (const candidate of candidates) {
    if (fs.existsSync(candidate)) {
      try {
        const raw = fs.readFileSync(candidate, "utf-8");
        const data = JSON.parse(raw);
        if (data && data.total) {
          return { path: candidate, data };
        }
      } catch {
        continue;
      }
    }
  }
  return null;
}

function walkSourceFiles(
  dir: string,
  projectRoot: string
): { file: string; lines: number; exceeds: boolean }[] {
  const results: { file: string; lines: number; exceeds: boolean }[] = [];
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
          /\.(ts|tsx|js|jsx|cs|java)$/i.test(entry.name) &&
          !/\.(test|spec|Tests)\./i.test(entry.name)
        ) {
          const content = fs.readFileSync(fullPath, "utf-8");
          const lineCount = content.split("\n").length;
          results.push({
            file: path.relative(projectRoot, fullPath),
            lines: lineCount,
            exceeds: lineCount > DEFAULT_THRESHOLDS.maxFileLines,
          });
        }
      }
    } catch {
      // Omite directorios ilegibles
    }
  }

  walk(dir);
  return results;
}

export const qualityGateEnforcerTool: SddToolHandler = {
  definition: {
    name: "quality_gate_enforcer",
    description:
      "Verifica las restricciones técnicas no negociables de SIGIC: cobertura de pruebas unitarias >= 95% en el Core/Dominio y límite de líneas por archivo, emitiendo un veredicto definitivo GO / NO-GO.",
    inputSchema: {
      type: "object",
      properties: {
        projectPath: {
          type: "string",
          description:
            "Ruta del proyecto. Si se omite, opera sobre la raíz de 'SIGIC-arquitSoft-proyecto-final'.",
        },
        checkType: {
          type: "string",
          enum: ["full", "coverage", "file-quality"],
          description:
            "Tipo de validación: 'full' (ambas), 'coverage' (cobertura) o 'file-quality' (calidad y longitud). Por defecto: 'full'.",
        },
        coverageThreshold: {
          type: "number",
          description:
            "Porcentaje mínimo de cobertura exigido (0-100). Por defecto: 95.",
        },
        blockOnFailure: {
          type: "boolean",
          description:
            "Si es verdadero, marca el veredicto como BLOQUEANTE si se incumple algún umbral. Por defecto: true.",
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

      const checkType =
        typeof args.checkType === "string" &&
        ["full", "coverage", "file-quality"].includes(args.checkType)
          ? args.checkType
          : "full";

      const threshold =
        typeof args.coverageThreshold === "number" &&
        args.coverageThreshold >= 0 &&
        args.coverageThreshold <= 100
          ? args.coverageThreshold
          : DEFAULT_THRESHOLDS.coverageMinimum;

      const blockOnFailure =
        typeof args.blockOnFailure === "boolean" ? args.blockOnFailure : true;

      const results: Record<string, unknown> = {
        projectPath: projectRoot,
        umbrales: { ...DEFAULT_THRESHOLDS, coverageMinimum: threshold },
        verificaciones: {} as Record<string, unknown>,
        veredictoFinal: "PENDIENTE",
      };

      let hasFailure = false;
      const verificaciones = results.verificaciones as Record<string, unknown>;

      // 1. Verificación de Cobertura
      if (checkType === "full" || checkType === "coverage") {
        const report = findCoverageReport(projectRoot);
        if (report) {
          const total = report.data.total;
          const linePct = total.lines?.pct ?? 0;
          const meetsThreshold = linePct >= threshold;
          const criticalFailures: Record<string, unknown> = {};

          for (const [filePath, fileData] of Object.entries(report.data)) {
            if (filePath === "total") continue;
            for (const area of DEFAULT_THRESHOLDS.criticalAreas) {
              if (filePath.toLowerCase().includes(`/${area}/`)) {
                const pct = fileData.lines?.pct ?? 0;
                if (pct < threshold) {
                  criticalFailures[filePath] = {
                    cobertura: pct,
                    exigida: threshold,
                    estado: "❌ DEBAJO DEL UMBRAL",
                  };
                  hasFailure = true;
                }
              }
            }
          }

          if (!meetsThreshold) hasFailure = true;

          verificaciones.cobertura = {
            reporteEncontrado: true,
            rutaReporte: path.relative(projectRoot, report.path),
            lineasTotal: linePct,
            ...(report.domain ? {
              ensamble: report.domain.assembly,
              suite: report.domain.suite,
              lineasCubiertas: report.domain.covered,
              lineasValidas: report.domain.valid,
            } : {
              sentenciasTotal: total.statements?.pct ?? 0,
              ramasTotal: total.branches?.pct ?? 0,
              funcionesTotal: total.functions?.pct ?? 0,
              fallosEnAreasCriticas: Object.keys(criticalFailures).length > 0
                ? criticalFailures
                : "✅ Todas las áreas críticas cumplen el umbral",
            }),
            cumpleUmbral: meetsThreshold,
          };
        } else {
          verificaciones.cobertura = {
            reporteEncontrado: false,
            estado: "⚠️ Reporte de cobertura no encontrado.",
            sugerencia:
              "Ejecutar pruebas con recolección de cobertura: dotnet test --collect:'XPlat Code Coverage'",
          };
          hasFailure = true;
        }
      }

      // 2. Verificación de Calidad de Archivos
      if (checkType === "full" || checkType === "file-quality") {
        const srcCandidates = ["src", "Source", "lib", "."];
        let srcDir = projectRoot;
        for (const candidate of srcCandidates) {
          const candPath = path.join(projectRoot, candidate);
          if (fs.existsSync(candPath) && fs.statSync(candPath).isDirectory()) {
            srcDir = candPath;
            break;
          }
        }

        const fileLengths = walkSourceFiles(srcDir, projectRoot);
        const oversized = fileLengths.filter((f) => f.exceeds);

        verificaciones.calidadArchivos = {
          totalArchivosAnalizados: fileLengths.length,
          archivosSobredimensionados: oversized.length,
          limiteMaximoLineas: DEFAULT_THRESHOLDS.maxFileLines,
          detalle:
            oversized.length > 0
              ? oversized.slice(0, 20)
              : "✅ Todos los archivos respetan el límite de tamaño",
        };

        if (oversized.length > 0) hasFailure = true;
      }

      results.veredictoFinal = hasFailure
        ? blockOnFailure
          ? "🚫 NO-GO (BLOQUEO DE DESPLIEGUE)"
          : "⚠️ NO-GO (NO BLOQUEANTE)"
        : "✅ GO — Quality Gate Aprobado";

      return {
        content: [
          {
            type: "text",
            text: JSON.stringify(results, null, 2),
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
              error: "Error ejecutando quality_gate_enforcer",
              detalle: error instanceof Error ? error.message : String(error),
            }),
          },
        ],
      };
    }
  },
};
