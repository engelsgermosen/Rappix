"use client";

import { useState, type FormEvent } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Trash2 } from "lucide-react";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { addCategory, removeCategory } from "@/lib/api/catalog-me";
import type { CatalogResponse, CategoryResponse, ItemResponse } from "@/lib/api/types";
import { describeError } from "@/lib/errors";
import { cn } from "@/lib/utils";

export function CategoriesRail({
  catalog,
  items,
  selectedCategoryId,
  onSelectCategory,
}: {
  catalog: CatalogResponse;
  items: ItemResponse[];
  selectedCategoryId: string | "all";
  onSelectCategory: (id: string | "all") => void;
}) {
  const queryClient = useQueryClient();
  const [newName, setNewName] = useState("");

  const add = useMutation({
    mutationFn: () => addCategory({ name: newName.trim(), sortOrder: (catalog.categories.length + 1) * 10 }),
    onSuccess: (data) => {
      queryClient.setQueryData(["catalog", "me"], data);
      setNewName("");
      toast.success("Categoría creada.");
    },
    onError: (err) => toast.error(describeError(err)),
  });

  const remove = useMutation({
    mutationFn: (categoryId: string) => removeCategory(categoryId),
    onSuccess: (data) => {
      queryClient.setQueryData(["catalog", "me"], data);
      toast.success("Categoría eliminada.");
    },
    onError: (err) => toast.error(describeError(err)),
  });

  function countFor(c: CategoryResponse) {
    return items.filter((it) => it.categoryId === c.id).length;
  }

  function totalCount() {
    return items.length;
  }

  function uncategorizedCount() {
    return items.filter((it) => !it.categoryId).length;
  }

  return (
    <aside className="space-y-3">
      <div className="space-y-1">
        <h2 className="text-lg font-bold text-foreground">Categorías</h2>
        <p className="text-xs text-muted-foreground">{catalog.categories.length} en total</p>
      </div>

      <div className="rounded-xl border border-border bg-card">
        <button
          type="button"
          onClick={() => onSelectCategory("all")}
          className={cn(
            "flex w-full items-center justify-between gap-2 px-4 py-2.5 text-sm font-semibold transition-colors",
            selectedCategoryId === "all" ? "bg-brand-50 text-brand" : "text-foreground/80 hover:bg-muted/40",
          )}
        >
          <span>Todos</span>
          <span className="text-xs font-mono text-muted-foreground">{totalCount()}</span>
        </button>

        {catalog.categories.map((c) => {
          const active = selectedCategoryId === c.id;
          return (
            <div
              key={c.id}
              className={cn(
                "group flex items-center justify-between gap-2 border-t border-border px-4 py-2.5 text-sm transition-colors",
                active ? "bg-brand-50" : "hover:bg-muted/40",
              )}
            >
              <button
                type="button"
                onClick={() => onSelectCategory(c.id)}
                className={cn(
                  "flex-1 text-left font-semibold",
                  active ? "text-brand" : "text-foreground/80",
                )}
              >
                {c.name}
              </button>
              <span className="text-xs font-mono text-muted-foreground">{countFor(c)}</span>
              <button
                type="button"
                onClick={() => {
                  if (confirm(`¿Eliminar la categoría "${c.name}"?`)) remove.mutate(c.id);
                }}
                className="text-muted-foreground opacity-0 transition-opacity hover:text-destructive group-hover:opacity-100"
                aria-label={`Eliminar ${c.name}`}
              >
                <Trash2 className="h-3.5 w-3.5" />
              </button>
            </div>
          );
        })}

        {uncategorizedCount() > 0 ? (
          <button
            type="button"
            onClick={() => onSelectCategory("")}
            className={cn(
              "flex w-full items-center justify-between gap-2 border-t border-border px-4 py-2.5 text-sm transition-colors",
              selectedCategoryId === "" ? "bg-brand-50 text-brand" : "text-foreground/60 hover:bg-muted/40",
            )}
          >
            <span>Sin categoría</span>
            <span className="text-xs font-mono">{uncategorizedCount()}</span>
          </button>
        ) : null}
      </div>

      <form
        onSubmit={(e: FormEvent) => {
          e.preventDefault();
          if (newName.trim()) add.mutate();
        }}
        className="flex gap-2"
      >
        <Input
          value={newName}
          onChange={(e) => setNewName(e.target.value)}
          placeholder="Nombre"
          maxLength={60}
        />
        <Button type="submit" disabled={!newName.trim() || add.isPending} size="default">
          OK
        </Button>
      </form>
    </aside>
  );
}
