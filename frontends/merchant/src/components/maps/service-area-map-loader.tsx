"use client";

import dynamic from "next/dynamic";

import { Skeleton } from "@/components/ui/skeleton";

export const ServiceAreaMap = dynamic(
  () => import("./service-area-map").then((m) => m.ServiceAreaMap),
  {
    ssr: false,
    loading: () => <Skeleton className="h-80 w-full rounded-lg" />,
  },
);
