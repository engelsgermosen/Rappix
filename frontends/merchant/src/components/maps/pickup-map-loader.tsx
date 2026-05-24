"use client";

import dynamic from "next/dynamic";

import { Skeleton } from "@/components/ui/skeleton";

export const PickupMap = dynamic(
  () => import("./pickup-map").then((m) => m.PickupMap),
  {
    ssr: false,
    loading: () => <Skeleton className="h-72 w-full rounded-lg" />,
  },
);
