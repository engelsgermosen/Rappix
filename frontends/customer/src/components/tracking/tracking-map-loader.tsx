"use client";

/**
 * Dynamic-import boundary: the Google Maps component only runs on the
 * client (ssr:false) and its script is split out of the main bundle. This
 * wrapper keeps the parent file synchronous and shows a skeleton meanwhile.
 */

import dynamic from "next/dynamic";

import { Skeleton } from "@/components/ui/skeleton";

export const TrackingMap = dynamic(
  () => import("./tracking-map").then((m) => m.TrackingMap),
  {
    ssr: false,
    loading: () => <Skeleton className="h-[420px] md:h-[480px] w-full rounded-xl" />,
  },
);
