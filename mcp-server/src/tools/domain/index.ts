import { inventorySddTools } from "./inventoryTools.js";
import { posSddTools } from "./posTools.js";
import { SddToolHandler } from "../types.js";

export const domainSddTools: SddToolHandler[] = [
  ...inventorySddTools,
  ...posSddTools,
];

export { inventorySddTools, posSddTools };
