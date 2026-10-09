"use client";

import { Toaster } from "sonner";

export function ToastProvider() {
  return (
    <Toaster
      position="top-right"
      richColors
      closeButton
      duration={3500}
      offset={16}
      toastOptions={{
        style: { borderRadius: "12px" },
      }}
    />
  );
}
