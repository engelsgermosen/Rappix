"use client";

import { Search, X } from "lucide-react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { Suspense, useEffect, useState } from "react";

/**
 * Search bar — debounced live-filter on the home page (rewrites the URL
 * `?q=`), navigate-to-home-with-q on any other page (Enter only).
 *
 * Reactive to URL changes so opening `/?q=foo` shows "foo" in the input,
 * and clicking "Comercios cercanos" (which clears `q`) empties it.
 *
 * Lives in the header on every page, so we wrap the inner component in
 * Suspense — `useSearchParams` requires a boundary for static rendering.
 */
export function SearchBar() {
  return (
    <Suspense fallback={<SearchSkeleton />}>
      <SearchBarInner />
    </Suspense>
  );
}

function SearchSkeleton() {
  return (
    <div className="relative flex-1 max-w-2xl">
      <Search className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
      <div className="h-10 w-full rounded-full border border-white/20 bg-white" />
    </div>
  );
}

function SearchBarInner() {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const isHome = pathname === "/";

  const urlQ = params.get("q") ?? "";
  const [q, setQ] = useState(urlQ);

  // Sync local state ← URL on navigation.
  useEffect(() => {
    setQ(urlQ);
  }, [urlQ]);

  // On home: debounce 200ms and rewrite the URL so the grid filters live.
  useEffect(() => {
    if (!isHome) return;
    const next = q.trim();
    if (next === urlQ) return;
    const id = window.setTimeout(() => {
      const sp = new URLSearchParams(params);
      if (next.length === 0) sp.delete("q");
      else sp.set("q", next);
      router.replace(sp.toString() ? `/?${sp.toString()}` : "/", { scroll: false });
    }, 200);
    return () => window.clearTimeout(id);
  }, [q, urlQ, isHome, router, params]);

  function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    // On non-home pages, Enter jumps to home with the term applied.
    if (!isHome) {
      const next = q.trim();
      router.push(next.length === 0 ? "/" : `/?q=${encodeURIComponent(next)}`);
    }
  }

  function onClear() {
    setQ("");
    if (isHome) {
      const sp = new URLSearchParams(params);
      sp.delete("q");
      router.replace(sp.toString() ? `/?${sp.toString()}` : "/", { scroll: false });
    }
  }

  return (
    <form className="relative flex-1 max-w-2xl" onSubmit={onSubmit} role="search">
      <Search className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
      <input
        value={q}
        onChange={(e) => setQ(e.target.value)}
        type="search"
        aria-label="Buscar comercios"
        placeholder="Busca comercios por nombre..."
        className="h-10 w-full rounded-full border border-white/20 bg-white pl-11 pr-10 text-sm text-foreground placeholder:text-muted-foreground/80 focus:outline-none focus:ring-2 focus:ring-white/40"
      />
      {q.length > 0 && (
        <button
          type="button"
          onClick={onClear}
          aria-label="Limpiar búsqueda"
          className="absolute right-3 top-1/2 -translate-y-1/2 h-6 w-6 inline-flex items-center justify-center rounded-full text-muted-foreground hover:bg-muted hover:text-foreground transition-colors"
        >
          <X className="h-3.5 w-3.5" />
        </button>
      )}
    </form>
  );
}
