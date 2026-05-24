"use client";

import { Bell } from "lucide-react";

export function NotificationsButton() {
  return (
    <button
      className="relative inline-flex h-10 w-10 items-center justify-center rounded-full bg-white/10 text-white hover:bg-white/20 transition-colors"
      aria-label="Notificaciones"
      type="button"
    >
      <Bell className="h-5 w-5" />
      <span className="absolute top-2 right-2 h-2 w-2 rounded-full bg-accent" />
    </button>
  );
}
