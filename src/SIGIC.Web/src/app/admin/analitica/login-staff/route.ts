import { handleLoginStaffPost } from "@/lib/bffAnalitica";

export const dynamic = "force-dynamic";

export async function POST(req: Request) {
  return handleLoginStaffPost(req);
}
