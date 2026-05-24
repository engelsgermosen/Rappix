"use client";

import { useQuery } from "@tanstack/react-query";
import { ImageOff } from "lucide-react";

import { getItemPhotoUrl } from "@/lib/api/catalog-public";
import { cn } from "@/lib/utils";

/**
 * Renders an item's photo by resolving its presigned MinIO URL. Falls back to
 * a neutral placeholder square if the item has no photoObjectKey or the URL
 * fetch failed.
 */
export function ItemPhoto({
  itemId,
  hasPhoto,
  className,
}: {
  itemId: string;
  hasPhoto: boolean;
  className?: string;
}) {
  const url = useQuery({
    queryKey: ["catalog", "item", "photo-url", itemId],
    queryFn: () => getItemPhotoUrl(itemId),
    enabled: hasPhoto,
    staleTime: 50 * 60_000,
    retry: false,
  });

  if (!hasPhoto || url.isError) {
    return (
      <div className={cn("flex h-12 w-12 items-center justify-center rounded-lg bg-accent-100 text-accent-600", className)}>
        <ImageOff className="h-5 w-5" />
      </div>
    );
  }

  if (!url.data) {
    return <div className={cn("h-12 w-12 animate-pulse rounded-lg bg-muted", className)} />;
  }

  return (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={url.data} alt="" className={cn("h-12 w-12 rounded-lg object-cover", className)} />
  );
}
