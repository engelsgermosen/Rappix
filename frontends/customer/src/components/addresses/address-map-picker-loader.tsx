"use client";

/**
 * Dynamic-import boundary for the map picker. Leaflet touches `window`
 * at module load, so the real component must be ssr:false. This wrapper
 * keeps the dialog file synchronous.
 */

import dynamic from "next/dynamic";

import { Skeleton } from "@/components/ui/skeleton";

export const AddressMapPicker = dynamic(
  () => import("./address-map-picker").then((m) => m.AddressMapPicker),
  {
    ssr: false,
    loading: () => <Skeleton className="h-64 sm:h-72 w-full rounded-lg" />,
  },
);
