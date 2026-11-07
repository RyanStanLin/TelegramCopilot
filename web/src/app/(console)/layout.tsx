import { redirect } from "next/navigation";
import { authenticated } from "@/lib/server/session";
import { Shell } from "@/components/Shell";
export const dynamic = "force-dynamic";
export default async function ConsoleLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  if (!(await authenticated())) redirect("/login");
  return <Shell>{children}</Shell>;
}
