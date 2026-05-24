"use client";

import { Search } from "lucide-react";
import { useRouter } from "next/navigation";
import { useState } from "react";

export function SearchBar() {
  const router = useRouter();
  const [q, setQ] = useState("");

  return (
    <form
      className="relative flex-1 max-w-2xl"
      onSubmit={(e) => {
        e.preventDefault();
        if (q.trim().length > 0) {
          router.push(`/?q=${encodeURIComponent(q.trim())}`);
        }
      }}
    >
      <Search className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
      <input
        value={q}
        onChange={(e) => setQ(e.target.value)}
        type="search"
        placeholder="Busca productos, comercios..."
        className="h-10 w-full rounded-full border border-white/20 bg-white pl-11 pr-4 text-sm text-foreground placeholder:text-muted-foreground/80 focus:outline-none focus:ring-2 focus:ring-white/40"
      />
    </form>
  );
}
