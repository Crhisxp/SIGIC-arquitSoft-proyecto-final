/**
 * sandbox.ts — Aislamiento y validación de rutas para el servidor MCP de SIGIC v2.2.
 * Asegura que todas las operaciones de lectura y escritura permanezcan
 * estrictamente dentro de la raíz de 'SIGIC-arquitSoft-proyecto-final'.
 */
import * as path from "node:path";
import * as fs from "node:fs";

// Raíz canónica del proyecto SIGIC
const CANONICAL_SIGIC_ROOT = "/home/crhistian/Projects/SIGIC-arquitSoft-proyecto-final";

function determineProjectRoot(): string {
  if (process.env.SIGIC_PROJECT_ROOT && fs.existsSync(process.env.SIGIC_PROJECT_ROOT)) {
    return path.resolve(process.env.SIGIC_PROJECT_ROOT);
  }
  // Resolver relativo a este archivo compilado o fuente
  const candidateFromHere = path.resolve(
    path.dirname(new URL(import.meta.url).pathname),
    "../../.."
  );
  if (fs.existsSync(candidateFromHere)) {
    return candidateFromHere;
  }
  return CANONICAL_SIGIC_ROOT;
}

const PROJECT_ROOT = determineProjectRoot();

let allowedReadPaths: string[] = [PROJECT_ROOT];
let allowedWritePaths: string[] = [PROJECT_ROOT];

export function getProjectRoot(): string {
  return PROJECT_ROOT;
}

export function configureSandbox(
  readPaths?: string[],
  writePaths?: string[]
): void {
  if (readPaths && readPaths.length > 0) {
    allowedReadPaths = readPaths.map((p) => path.resolve(p));
  }
  if (writePaths && writePaths.length > 0) {
    allowedWritePaths = writePaths.map((p) => path.resolve(p));
  }
}

export function assertReadable(targetPath: string): string {
  const resolved = path.resolve(targetPath);
  const isAllowed = allowedReadPaths.some(
    (allowed) =>
      resolved === allowed || resolved.startsWith(allowed + path.sep)
  );
  if (!isAllowed) {
    throw new Error(
      `VIOLACIÓN DE SANDBOX: Acceso de lectura denegado para "${resolved}". ` +
        `Rutas permitidas: ${allowedReadPaths.join(", ")}`
    );
  }
  return resolved;
}

export function assertWritable(targetPath: string): string {
  const resolved = path.resolve(targetPath);
  const isAllowed = allowedWritePaths.some(
    (allowed) =>
      resolved === allowed || resolved.startsWith(allowed + path.sep)
  );
  if (!isAllowed) {
    throw new Error(
      `VIOLACIÓN DE SANDBOX: Acceso de escritura denegado para "${resolved}". ` +
        `Rutas permitidas: ${allowedWritePaths.join(", ")}`
    );
  }
  return resolved;
}
