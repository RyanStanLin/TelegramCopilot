"use client";
import { PageLayout } from "@primer/react";
import { Failure } from "@/components/Status";

export default function ErrorPage({ reset }: { reset: () => void }) {
  return (
    <PageLayout containerWidth="medium">
      <PageLayout.Content>
        <Failure retry={reset} focus />
      </PageLayout.Content>
    </PageLayout>
  );
}
