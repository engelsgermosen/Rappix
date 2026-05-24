"use client";

import { useQuery } from "@tanstack/react-query";
import { AlertCircle } from "lucide-react";
import { use } from "react";

import { CartFab } from "@/components/catalog/cart-fab";
import { CatalogSection } from "@/components/catalog/catalog-section";
import { MerchantHero } from "@/components/merchants/merchant-hero";
import { Skeleton } from "@/components/ui/skeleton";
import { catalogApi } from "@/lib/api/catalog";
import { merchantsApi } from "@/lib/api/merchants";

export default function MerchantBySlugPage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = use(params);

  const merchantQ = useQuery({
    queryKey: ["merchants", "by-slug", slug],
    queryFn: () => merchantsApi.bySlug(slug),
  });

  const itemsQ = useQuery({
    enabled: !!merchantQ.data?.id,
    queryKey: ["catalog", "items", merchantQ.data?.id],
    queryFn: () =>
      catalogApi.search({
        merchantId: merchantQ.data!.id,
        pageSize: 100,
      }),
  });

  if (merchantQ.isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-56 w-full rounded-none rounded-b-2xl" />
        <div className="container space-y-3">
          <Skeleton className="h-8 w-1/3" />
          <Skeleton className="h-4 w-1/2" />
        </div>
      </div>
    );
  }

  if (merchantQ.isError || !merchantQ.data) {
    return (
      <div className="container py-16">
        <div className="flex flex-col items-center justify-center gap-3 rounded-xl border border-dashed border-border bg-white py-16 text-center text-muted-foreground">
          <AlertCircle className="h-8 w-8 text-amber-500" />
          <div className="font-semibold text-foreground">Comercio no encontrado</div>
          <div className="text-sm">Es posible que esté pausado o no exista.</div>
        </div>
      </div>
    );
  }

  const merchant = merchantQ.data;
  const items = itemsQ.data?.items ?? [];

  return (
    <div className="animate-fade-in pb-40">
      <MerchantHero merchant={merchant} />

      {itemsQ.isLoading ? (
        <div className="container mt-10 md:mt-12 space-y-4">
          <Skeleton className="h-10 w-1/2" />
          <Skeleton className="h-32 w-full" />
          <Skeleton className="h-32 w-full" />
        </div>
      ) : items.length === 0 ? (
        <div className="container mt-10 md:mt-12">
          <div className="rounded-xl border border-dashed border-border bg-white py-20 text-center text-muted-foreground">
            Este comercio aún no tiene productos publicados.
          </div>
        </div>
      ) : (
        <CatalogSection
          merchant={{ id: merchant.id, name: merchant.name, slug: merchant.slug, vertical: merchant.verticalType }}
          items={items}
        />
      )}

      <CartFab />
    </div>
  );
}
