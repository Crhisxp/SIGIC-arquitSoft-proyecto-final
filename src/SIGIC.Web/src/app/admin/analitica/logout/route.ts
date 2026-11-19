import { handleLogoutPost } from "@/lib/bffAnalitica";

export const dynamic = "force-dynamic";

export async function POST(req: Request) {
  return handleLogoutPost(req);
}
