import { Suspense } from "react";
import { notFound } from "next/navigation";
import { kinds, type Kind } from "@/lib/types";
import { ResourceList } from "@/features/configuration/ResourceList";
import { Presence } from "@/features/presence/Presence";
import { History } from "@/features/History";
export default async function SectionPage({
  params,
}: {
  params: Promise<{ section: string }>;
}) {
  const { section } = await params;
  const content =
    section === "presence" ? (
      <Presence />
    ) : section === "events" || section === "deliveries" ? (
      <History key={section} section={section} />
    ) : kinds.includes(section as Kind) ? (
      <ResourceList key={section} kind={section as Kind} />
    ) : null;
  if (!content) notFound();
  return <Suspense>{content}</Suspense>;
}
