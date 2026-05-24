"use client";

import { useMemo } from "react";

import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import type { PublicItem, VerticalType } from "@/lib/api/types";

import { ItemCard } from "./item-card";

type Merchant = { id: string; name: string; slug: string; vertical: VerticalType };

const LABEL_BY_VERTICAL: Record<VerticalType, { popular: string; sections: [string, string, string] }> = {
  Food:     { popular: "Populares", sections: ["Platos fuertes", "Acompañantes", "Bebidas"] },
  Pharmacy: { popular: "Populares", sections: ["Sin receta", "Cuidado personal", "Vitaminas"] },
  Grocery:  { popular: "Más pedidos", sections: ["Frescos", "Despensa", "Bebidas"] },
  Parcel:   { popular: "Servicios", sections: ["Express", "Mismo día", "Programado"] },
};

/**
 * Catalog has no public "categories" endpoint. We group items by `categoryId`
 * client-side; when no name is available we synthesize section labels per
 * vertical to keep the page readable. Listed as backend follow-up.
 */
function bucketize(items: PublicItem[], vertical: VerticalType) {
  const popularLabel = LABEL_BY_VERTICAL[vertical].popular;
  const [s1, s2, s3] = LABEL_BY_VERTICAL[vertical].sections;

  // Bucket by hashing the categoryId to one of three labels for a stable layout.
  const buckets: Record<string, PublicItem[]> = {
    [popularLabel]: items.slice(0, Math.min(items.length, 4)),
    [s1]: [],
    [s2]: [],
    [s3]: [],
  };

  for (const item of items) {
    const key = item.categoryId ?? "uncategorized";
    let h = 0;
    for (let i = 0; i < key.length; i++) h = (h * 31 + key.charCodeAt(i)) >>> 0;
    const bucket = [s1, s2, s3][h % 3]!;
    buckets[bucket]!.push(item);
  }

  return buckets;
}

export function CatalogSection({
  merchant,
  items,
}: {
  merchant: Merchant;
  items: PublicItem[];
}) {
  const labels = LABEL_BY_VERTICAL[merchant.vertical];
  const popularLabel = labels.popular;
  const buckets = useMemo(() => bucketize(items, merchant.vertical), [items, merchant.vertical]);
  const tabs = [popularLabel, ...labels.sections];

  return (
    <Tabs defaultValue={popularLabel} className="container mt-6">
      <TabsList className="overflow-x-auto no-scrollbar">
        {tabs.map((t) => (
          <TabsTrigger key={t} value={t}>
            {t}
          </TabsTrigger>
        ))}
      </TabsList>
      {tabs.map((t) => {
        const bucket = buckets[t] ?? [];
        return (
          <TabsContent key={t} value={t}>
            {bucket.length === 0 ? (
              <div className="rounded-xl border border-dashed border-border bg-white py-16 text-center text-muted-foreground">
                Aún no hay productos en esta sección.
              </div>
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3 md:gap-4">
                {bucket.map((item, idx) => (
                  <ItemCard
                    key={item.id}
                    item={item}
                    merchant={merchant}
                    popular={t === popularLabel && idx === 0}
                  />
                ))}
              </div>
            )}
          </TabsContent>
        );
      })}
    </Tabs>
  );
}
