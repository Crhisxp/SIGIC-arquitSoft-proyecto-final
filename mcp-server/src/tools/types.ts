import { Tool, CallToolResult } from "@modelcontextprotocol/sdk/types.js";

export interface SddToolHandler {
  definition: Tool;
  execute: (args: Record<string, unknown>) => Promise<CallToolResult>;
}

export interface SddAuditCheck {
  requirementId: string;
  title: string;
  passed: boolean;
  severity: "BLOCKER" | "CRITICAL" | "INFO";
  evidence: string;
  remediation?: string;
}
