import { handleSessionGet } from "@/lib/bffAnalitica";

export const dynamic = "force-dynamic";

export async function GET(req: Request) {
  return handleSessionGet(req);
}
