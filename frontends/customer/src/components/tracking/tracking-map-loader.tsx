"use client";

/**
 * Leaflet uses `window` at import time — we have to dynamic-import the real
 * map only on the client. This tiny wrapper isolates the dynamic boundary so
 * the rest of the tracking page can stay synchronous.
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
