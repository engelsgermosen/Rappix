"use client";

import { useEffect, useState, type FormEvent } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { updateMyMerchant } from "@/lib/api/merchants-me";
import { describeError } from "@/lib/errors";
import type { MerchantResponse, VerticalType } from "@/lib/api/types";

export function DatosCard({ merchant }: { merchant: MerchantResponse }) {
  const queryClient = useQueryClient();
  const [name, setName] = useState(merchant.name);
  const [slug, setSlug] = useState(merchant.slug);
  const [rnc, setRnc] = useState(merchant.rnc ?? "");
  const [description, setDescription] = useState(merchant.description ?? "");
  const [vertical, setVertical] = useState<VerticalType>(merchant.verticalType);

  // Re-sync if the parent invalidates the query.
  useEffect(() => {
    setName(merchant.name);
    setSlug(merchant.slug);
    setRnc(merchant.rnc ?? "");
    setDescription(merchant.description ?? "");
    setVertical(merchant.verticalType);
  }, [merchant]);

  const save = useMutation({
    mutationFn: () =>
      updateMyMerchant({
        name: name.trim(),
        slug: slug.trim(),
        rnc: rnc.trim() || null,
        description: description.trim() || null,
        verticalType: vertical,
      }),
    onSuccess: (data) => {
      queryClient.setQueryData(["merchant", "me"], data);
      toast.success("Datos del comercio guardados.");
    },
    onError: (err) => toast.error(describeError(err)),
  });

  const dirty =
    name !== merchant.name ||
    slug !== merchant.slug ||
    rnc !== (merchant.rnc ?? "") ||
    description !== (merchant.description ?? "") ||
    vertical !== merchant.verticalType;

  return (
    <Card className="overflow-hidden">
      <div className="border-b border-border px-6 py-4">
        <h2 className="text-lg font-bold text-foreground">Datos básicos</h2>
        <p className="text-xs text-muted-foreground">Cómo se llama tu comercio y qué vende.</p>
      </div>
      <form
        onSubmit={(e: FormEvent) => {
          e.preventDefault();
          save.mutate();
        }}
        className="space-y-4 px-6 py-5"
      >
        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-1.5">
            <Label htmlFor="cfg-name">Nombre</Label>
            <Input id="cfg-name" value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="cfg-slug">URL pública</Label>
            <Input id="cfg-slug" value={slug} onChange={(e) => setSlug(e.target.value)} />
          </div>
        </div>
        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-1.5">
            <Label htmlFor="cfg-vertical">Vertical</Label>
            <Select value={vertical} onValueChange={(v) => setVertical(v as VerticalType)}>
              <SelectTrigger id="cfg-vertical">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Food">Comida</SelectItem>
                <SelectItem value="Pharmacy">Farmacia</SelectItem>
                <SelectItem value="Grocery">Súper</SelectItem>
                <SelectItem value="Parcel">Paquetería</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="cfg-rnc">RNC</Label>
            <Input id="cfg-rnc" value={rnc} onChange={(e) => setRnc(e.target.value)} />
          </div>
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="cfg-desc">Descripción</Label>
          <Textarea
            id="cfg-desc"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            rows={3}
            maxLength={280}
          />
        </div>
        <div className="flex items-center justify-end gap-2 border-t border-border pt-4">
          <Button type="submit" disabled={!dirty || save.isPending}>
            {save.isPending ? "Guardando..." : "Guardar cambios"}
          </Button>
        </div>
      </form>
    </Card>
  );
}
