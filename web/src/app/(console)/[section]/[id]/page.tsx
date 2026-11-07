import { notFound } from "next/navigation";
import { kinds, type Kind } from "@/lib/types";
import { ResourceForm } from "@/features/configuration/ResourceForm";
export default async function EditPage({
  params,
}: {
  params: Promise<{ section: string; id: string }>;
}) {
  const { section, id } = await params;
  if (!kinds.includes(section as Kind)) notFound();
  return <ResourceForm key={section + id} kind={section as Kind} id={id} />;
}
