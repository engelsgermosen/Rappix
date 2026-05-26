"use client";

import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Plus } from "lucide-react";

import { CategoriesRail } from "@/components/catalogo/categories-rail";
import { ItemsTable } from "@/components/catalogo/items-table";
import { NewItemDialog } from "@/components/catalogo/new-item-dialog";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { getMyCatalog, listMyItems } from "@/lib/api/catalog-me";
import { ApiError } from "@/lib/api/errors";

export default function CatalogoPage() {
  const [selectedCategoryId, setSelectedCategoryId] = useState<string | "all">("all");
  const [newOpen, setNewOpen] = useState(false);

  const catalog = useQuery({
    queryKey: ["catalog", "me"],
    queryFn: getMyCatalog,
    staleTime: 60_000,
  });

  // Fetch ALL items (pageSize big enough for early-stage merchants) and
  // filter client-side by selected category. Avoids a server round trip per click.
  const items = useQuery({
    queryKey: ["catalog", "me", "items", { all: true }],
    queryFn: () => listMyItems({ pageSize: 200 }),
    staleTime: 30_000,
  });

  // El backend devuelve PagedResult<ItemResponse> = { items, page, pageSize, ... } —
  // SIEMPRE desempacar `.items` antes de pasar a componentes que esperan array.
  // Antes este caller hacia `items.data ?? []` y pasaba el wrapper como prop,
  // crasheando `<CategoriesRail>` con "items.filter is not a function".
  // useMemo para estabilizar la referencia y no recalcular `filtered` en cada render.
  const itemsList = useMemo(() => items.data?.items ?? [], [items.data]);

  const filtered = useMemo(() => {
    if (selectedCategoryId === "all") return itemsList;
    if (selectedCategoryId === "") return itemsList.filter((i) => !i.categoryId);
    return itemsList.filter((i) => i.categoryId === selectedCategoryId);
  }, [itemsList, selectedCategoryId]);

  // The catalog row only exists after the merchant is Active. If we get a 404,
  // show a friendly empty-state instead of an error banner.
  const isCatalogMissing =
    catalog.isError &&
    catalog.error instanceof ApiError &&
    catalog.error.status === 404;

  if (isCatalogMissing) {
    return (
      <section className="px-8 py-10">
        <Card className="px-6 py-12 text-center">
          <h1 className="text-xl font-semibold text-foreground">Tu catálogo aún no está activo</h1>
          <p className="mx-auto mt-2 max-w-md text-sm text-muted-foreground">
            El catálogo se crea automáticamente cuando tu comercio sea aprobado. Vuelve aquí en cuanto recibas el
            correo de aprobación.
          </p>
        </Card>
      </section>
    );
  }

  // Estado completamente vacio (comercio recien aprobado): banner amable arriba del grid
  // guiando al merchant a crear su primera categoria + producto. Las columnas debajo
  // siguen funcionales (CategoriesRail tiene el form para agregar categoria, ItemsTable
  // muestra "No hay productos") — el banner solo agrega contexto, no reemplaza el UI.
  const isEmptyCatalog = !!catalog.data
    && catalog.data.categories.length === 0
    && !items.isPending
    && itemsList.length === 0;

  return (
    <section className="px-8 py-8">
      <header className="mb-6 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-3xl font-bold text-foreground">Catálogo</h1>
          <p className="mt-1 text-sm text-muted-foreground">Gestiona tus productos y categorías.</p>
        </div>
      </header>

      {isEmptyCatalog ? (
        <Card className="mb-6 border-brand-100 bg-brand-50/40 px-6 py-5">
          <h2 className="text-base font-semibold text-brand-700">Empieza tu catálogo</h2>
          <p className="mt-1 text-sm text-foreground/80">
            Aún no tienes categorías ni productos. Crea una categoría a la izquierda
            (por ejemplo, "Pizzas") y luego usa <span className="font-semibold">Añadir producto</span> para tu primer item.
          </p>
        </Card>
      ) : null}

      <div className="grid gap-6 lg:grid-cols-[280px,1fr]">
        {catalog.data ? (
          <CategoriesRail
            catalog={catalog.data}
            items={itemsList}
            selectedCategoryId={selectedCategoryId}
            onSelectCategory={setSelectedCategoryId}
          />
        ) : (
          <Card className="h-64 animate-pulse" />
        )}

        <div className="space-y-4">
          <div className="flex flex-wrap items-end justify-between gap-3">
            <div>
              <h2 className="text-xl font-bold text-foreground">
                {selectedCategoryId === "all"
                  ? "Todos los productos"
                  : selectedCategoryId === ""
                    ? "Sin categoría"
                    : catalog.data?.categories.find((c) => c.id === selectedCategoryId)?.name ?? ""}
              </h2>
              <p className="text-xs text-muted-foreground">
                {filtered.length} {filtered.length === 1 ? "producto" : "productos"}
              </p>
            </div>
            <Button type="button" onClick={() => setNewOpen(true)} disabled={!catalog.data}>
              <Plus className="h-4 w-4" />
              Añadir producto
            </Button>
          </div>

          {items.isPending ? (
            <Card className="h-64 animate-pulse" />
          ) : (
            <ItemsTable items={filtered} />
          )}
        </div>
      </div>

      {catalog.data ? (
        <NewItemDialog
          open={newOpen}
          onClose={() => setNewOpen(false)}
          categories={catalog.data.categories}
          defaultCategoryId={
            selectedCategoryId !== "all" && selectedCategoryId !== "" ? selectedCategoryId : null
          }
        />
      ) : null}
    </section>
  );
}
