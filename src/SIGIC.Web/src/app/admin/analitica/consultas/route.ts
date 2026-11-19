import { handleConsultasPost } from "@/lib/bffAnalitica";

export const dynamic = "force-dynamic";

export async function POST(req: Request) {
  return handleConsultasPost(req);
}
