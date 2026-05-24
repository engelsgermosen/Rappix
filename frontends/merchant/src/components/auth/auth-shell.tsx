import type { ReactNode } from "react";

import { RappixLogo } from "@/components/layout/rappix-logo";

/**
 * Two-column auth layout: brand hero on the left (purple gradient + tagline),
 * the form on the right. On <lg screens the hero stacks above the form.
 */
export function AuthShell({
  title,
  tagline,
  helper,
  children,
}: {
  title?: string;
  tagline: string;
  helper: string;
  children: ReactNode;
}) {
  return (
    <div className="grid min-h-screen grid-cols-1 lg:grid-cols-2">
      {/* Left: hero */}
      <aside className="hero-merchant relative flex flex-col justify-between p-10 text-white lg:p-12">
        {/* Decorative blurred circles */}
        <div className="pointer-events-none absolute -right-24 -top-24 h-72 w-72 rounded-full bg-white/10 blur-2xl" />
        <div className="pointer-events-none absolute -bottom-16 -left-16 h-72 w-72 rounded-full bg-white/10 blur-2xl" />

        <RappixLogo variant="light" />

        <div className="relative z-10 max-w-md space-y-4">
          {title ? (
            <p className="text-sm font-semibold uppercase tracking-[0.18em] text-white/70">{title}</p>
          ) : null}
          <h1 className="text-4xl font-bold leading-tight lg:text-5xl">{tagline}</h1>
          <p className="text-base text-white/80">{helper}</p>
        </div>

        <div className="relative z-10 flex flex-wrap gap-x-6 gap-y-2 text-sm text-white/80">
          <span className="font-semibold text-white">Comida</span>
          <span className="opacity-50">·</span>
          <span className="font-semibold text-white">Farmacia</span>
          <span className="opacity-50">·</span>
          <span className="font-semibold text-white">Súper</span>
          <span className="opacity-50">·</span>
          <span className="font-semibold text-white">Paquetería</span>
        </div>
      </aside>

      {/* Right: form */}
      <main className="flex items-center justify-center bg-background px-6 py-12 lg:px-12">
        <div className="w-full max-w-md">{children}</div>
      </main>
    </div>
  );
}
