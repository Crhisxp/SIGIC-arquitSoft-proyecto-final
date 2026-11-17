import { SddToolHandler } from "../types.js";
import { sddChainValidatorTool } from "./sdd-chain-validator.js";
import { architectureDriftAuditorTool } from "./architecture-drift-auditor.js";
import { qualityGateEnforcerTool } from "./quality-gate-enforcer.js";
import { obsidianVaultSyncTool } from "./obsidian-vault-sync.js";

export const governanceSddTools: SddToolHandler[] = [
  sddChainValidatorTool,
  architectureDriftAuditorTool,
  qualityGateEnforcerTool,
  obsidianVaultSyncTool,
];

export {
  sddChainValidatorTool,
  architectureDriftAuditorTool,
  qualityGateEnforcerTool,
  obsidianVaultSyncTool,
};
