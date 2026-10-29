"use client";

/**
 * Dynamic-import boundary: the Google Maps component only runs on the
 * client (ssr:false) and its script is split out of the main bundle. This
 * wrapper keeps the parent file synchronous and shows a skeleton meanwhile.
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
