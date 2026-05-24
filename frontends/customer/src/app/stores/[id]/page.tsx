"use client";

import { useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { use, useEffect } from "react";

import { merchantsApi } from "@/lib/api/merchants";

/**
 * Alias route — accepts a merchant id and redirects to /comercios/<slug>
 * so the canonical URL is always slug-based.
 */
export default function MerchantByIdPage({ params }: { params: Promise<{ id: string }> }) {
  const router = useRouter();
  const { id } = use(params);
  const q = useQuery({
    queryKey: ["merchants", "by-id", id],
    queryFn: () => merchantsApi.byId(id),
  });

  useEffect(() => {
    if (q.data?.slug) router.replace(`/comercios/${q.data.slug}`);
  }, [q.data, router]);

  return (
    <div className="container py-16 text-center text-muted-foreground">Cargando comercio…</div>
  );
}
