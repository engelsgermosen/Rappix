"use client";

import { Toaster } from "sonner";

export function ToastProvider() {
  return (
    <Toaster
      position="top-center"
      richColors
      closeButton
      duration={3500}
      toastOptions={{
        style: { borderRadius: "12px" },
      }}
    />
  );
}
