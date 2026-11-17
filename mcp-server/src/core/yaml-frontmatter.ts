/**
 * yaml-frontmatter.ts — Parser robusto de metadatos YAML frontmatter para documentos SDD.
 * Extrae: version, estado, doc, tipo, depende-de, habilita, aprobado, etc.
 * No requiere dependencias externas de YAML.
 */

export interface SDDFrontmatter {
  version?: string;
  estado?: string;
  doc?: string;
  tipo?: string;
  "depende-de"?: string[];
  habilita?: string[];
  aprobado?: boolean;
  "última-revisión"?: string;
  "sprint-activo"?: string;
  [key: string]: unknown;
}

export function parseFrontmatter(content: string): SDDFrontmatter | null {
  // 1. Frontmatter estándar al inicio del archivo
  let match = content.match(/^---\r?\n([\s\S]*?)\r?\n---/);

  // 2. Fallback para bloques de metadatos incrustados o con indentación
  if (!match) {
    match = content.match(
      /(?:^|\n)[ \t]*---[ \t]*\r?\n([ \t]*(?:version|estado|doc|titulo|autor|tipo)[\s\S]*?)\r?\n[ \t]*---/i
    );
  }

  if (!match) return null;

  const block = match[1];
  const result: Record<string, unknown> = {};
  const lines = block.split(/\r?\n/);
  let currentKey: string | null = null;
  let collectingList: string[] | null = null;

  for (const rawLine of lines) {
    const line = rawLine.trimStart();
    // Elemento de lista (- item o - "item")
    const dashItem = line.match(/^-\s+"?([^"]*)"?\s*$/);
    if (dashItem && currentKey && collectingList) {
      collectingList.push(dashItem[1].trim());
      result[currentKey] = collectingList;
      continue;
    }

    if (collectingList) {
      collectingList = null;
    }

    // Línea clave: valor
    const kvMatch = line.match(/^([A-Za-zÀ-ÿ_-]+)\s*:\s*(.*?)\s*$/);
    if (!kvMatch) continue;

    const key = kvMatch[1];
    const rawValue = kvMatch[2];
    currentKey = key;

    // Valor vacío -> posible inicio de lista con guiones
    if (rawValue === "" || rawValue === undefined) {
      collectingList = [];
      result[key] = collectingList;
      continue;
    }

    // Arreglo en línea: [a, b, c]
    if (rawValue.startsWith("[") && rawValue.endsWith("]")) {
      const inner = rawValue.slice(1, -1).trim();
      if (inner === "") {
        result[key] = [];
      } else {
        result[key] = inner
          .split(",")
          .map((v) => v.trim().replace(/^["']|["']$/g, ""));
      }
      continue;
    }

    // Booleanos
    if (rawValue === "true") {
      result[key] = true;
      continue;
    }
    if (rawValue === "false") {
      result[key] = false;
      continue;
    }

    // Números
    if (/^\d+(\.\d+)?$/.test(rawValue)) {
      result[key] = parseFloat(rawValue);
      continue;
    }

    // Cadenas entrecomilladas
    const quoted = rawValue.match(/^["'](.*?)["']$/);
    if (quoted) {
      result[key] = quoted[1];
      continue;
    }

    // Cadenas simples
    result[key] = rawValue;
  }

  return result as SDDFrontmatter;
}

/**
 * Extrae los títulos de encabezado Markdown (## Título) del documento.
 */
export function extractHeadings(content: string): string[] {
  const headings: string[] = [];
  const regex = /^#{1,6}\s+(.+)$/gm;
  let match: RegExpExecArray | null;
  while ((match = regex.exec(content)) !== null) {
    headings.push(match[1].trim());
  }
  return headings;
}
