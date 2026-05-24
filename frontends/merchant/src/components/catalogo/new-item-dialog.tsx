"use client";

import { useEffect, useRef, useState, type FormEvent } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { ImageIcon, Upload } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { createItem, setItemAvailability, uploadItemPhoto } from "@/lib/api/catalog-me";
import { describeError } from "@/lib/errors";
import type { CategoryResponse } from "@/lib/api/types";

const MAX_PHOTO_BYTES = 4 * 1024 * 1024;

export function NewItemDialog({
  open,
  onClose,
  categories,
  defaultCategoryId,
}: {
  open: boolean;
  onClose: () => void;
  categories: CategoryResponse[];
  defaultCategoryId?: string | null;
}) {
  const queryClient = useQueryClient();

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [categoryId, setCategoryId] = useState<string>("");
  const [available, setAvailable] = useState(true);
  const [price, setPrice] = useState<number>(0);
  const [initialStock, setInitialStock] = useState<number>(0);
  const [photoFile, setPhotoFile] = useState<File | null>(null);
  const [photoPreview, setPhotoPreview] = useState<string | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  // Set default category whenever the dialog opens or props change.
  useEffect(() => {
    if (!open) return;
    setCategoryId(defaultCategoryId ?? categories[0]?.id ?? "");
  }, [open, defaultCategoryId, categories]);

  // Reset other fields when the dialog closes.
  useEffect(() => {
    if (open) return;
    setName("");
    setDescription("");
    setAvailable(true);
    setPrice(0);
    setInitialStock(0);
    if (photoPreview) URL.revokeObjectURL(photoPreview);
    setPhotoPreview(null);
    setPhotoFile(null);
  }, [open]); // eslint-disable-line react-hooks/exhaustive-deps

  const create = useMutation({
    mutationFn: async () => {
      const item = await createItem({
        categoryId: categoryId || null,
        name: name.trim(),
        description: description.trim() || null,
        priceAmount: price,
        tracksInventory: true,
        initialStock,
        currency: "DOP",
      });
      // Flip availability if user defaulted off.
      if (!available) {
        await setItemAvailability(item.id, { available: false });
      }
      // Upload photo after item exists.
      if (photoFile) {
        await uploadItemPhoto(item.id, photoFile);
      }
      return item;
    },
    onSuccess: () => {
      toast.success("Producto creado.");
      queryClient.invalidateQueries({ queryKey: ["catalog", "me", "items"] });
      onClose();
    },
    onError: (err) => toast.error(describeError(err)),
  });

  function onPickFile(file: File) {
    if (file.size > MAX_PHOTO_BYTES) {
      toast.error("La foto supera el tamaño máximo de 4 MB.");
      return;
    }
    if (!file.type.startsWith("image/")) {
      toast.error("Solo imágenes (JPG, PNG o WebP).");
      return;
    }
    const url = URL.createObjectURL(file);
    if (photoPreview) URL.revokeObjectURL(photoPreview);
    setPhotoPreview(url);
    setPhotoFile(file);
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (!name.trim()) {
      toast.error("El nombre es obligatorio.");
      return;
    }
    if (price <= 0) {
      toast.error("El precio debe ser mayor que cero.");
      return;
    }
    create.mutate();
  }

  return (
    <Dialog open={open} onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="max-w-xl">
        <DialogHeader>
          <DialogTitle>Nuevo producto</DialogTitle>
          <DialogDescription>Agrega un item a tu catálogo. Lo verán los clientes en cuanto lo publiques.</DialogDescription>
        </DialogHeader>

        <form onSubmit={onSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="item-name">
              Nombre <span className="text-destructive">*</span>
            </Label>
            <Input
              id="item-name"
              required
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Ej. Pizza Hawaiana"
            />
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="item-desc">Descripción</Label>
            <Textarea
              id="item-desc"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Describe ingredientes o características."
              rows={3}
              maxLength={500}
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="item-cat">Categoría</Label>
              <Select value={categoryId} onValueChange={setCategoryId}>
                <SelectTrigger id="item-cat">
                  <SelectValue placeholder="Selecciona" />
                </SelectTrigger>
                <SelectContent>
                  {categories.map((c) => (
                    <SelectItem key={c.id} value={c.id}>
                      {c.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label>Disponibilidad</Label>
              <div className="flex h-11 items-center gap-3 rounded-lg border border-input bg-white px-3">
                <Switch checked={available} onCheckedChange={setAvailable} />
                <span className="text-sm">{available ? "Publicado" : "Oculto"}</span>
              </div>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="item-price">
                Precio (RD$) <span className="text-destructive">*</span>
              </Label>
              <Input
                id="item-price"
                type="number"
                min={0}
                step={0.01}
                value={Number.isNaN(price) ? "" : price}
                onChange={(e) => setPrice(Number(e.target.value))}
                required
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="item-stock">Stock inicial</Label>
              <Input
                id="item-stock"
                type="number"
                min={0}
                value={Number.isNaN(initialStock) ? "" : initialStock}
                onChange={(e) => setInitialStock(Number(e.target.value))}
              />
            </div>
          </div>

          <div className="space-y-1.5">
            <Label>Foto del producto</Label>
            <button
              type="button"
              onClick={() => fileRef.current?.click()}
              className="flex w-full items-center justify-center gap-2 rounded-lg border-2 border-dashed border-border bg-muted/30 px-4 py-8 text-sm text-muted-foreground hover:bg-muted/50"
            >
              {photoPreview ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img src={photoPreview} alt="Preview" className="h-24 w-24 rounded-md object-cover" />
              ) : (
                <>
                  <Upload className="h-5 w-5" />
                  Arrastra una imagen o haz click
                </>
              )}
            </button>
            <input
              ref={fileRef}
              type="file"
              accept="image/jpeg,image/png,image/webp"
              className="hidden"
              onChange={(e) => {
                const file = e.target.files?.[0];
                if (file) onPickFile(file);
                e.target.value = "";
              }}
            />
            <p className="text-xs text-muted-foreground">Cuadrada, mínimo 600×600 px. Máx. 4 MB.</p>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              Cancelar
            </Button>
            <Button type="submit" disabled={create.isPending}>
              {create.isPending ? "Creando..." : "Crear producto"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
