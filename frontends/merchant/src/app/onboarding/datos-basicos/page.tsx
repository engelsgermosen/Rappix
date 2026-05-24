"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Box, Pill, ShoppingBasket, UtensilsCrossed } from "lucide-react";

import { StepShell } from "@/components/onboarding/step-shell";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { getMyMerchant, updateMyMerchant } from "@/lib/api/merchants-me";
import type { UpdateMerchantProfileRequest, VerticalType } from "@/lib/api/types";
import { cn } from "@/lib/utils";
import { describeError } from "@/lib/errors";

type VerticalCard = {
  value: VerticalType;
  label: string;
  caption: string;
  Icon: typeof UtensilsCrossed;
};

const VERTICALS: VerticalCard[] = [
  { value: "Food", label: "Comida", caption: "Restaurantes, cafeterías, repostería", Icon: UtensilsCrossed },
  { value: "Pharmacy", label: "Farmacia", caption: "Medicamentos y cuidado personal", Icon: Pill },
  { value: "Grocery", label: "Súper", caption: "Frescos, abarrotes, lácteos", Icon: ShoppingBasket },
  { value: "Parcel", label: "Paquetería", caption: "Envíos y mensajería", Icon: Box },
];

function slugify(input: string) {
  return input
    .toLowerCase()
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 60);
}

export default function DatosBasicosPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const merchant = useQuery({ queryKey: ["merchant", "me"], queryFn: getMyMerchant });

  const [name, setName] = useState("");
  const [slug, setSlug] = useState("");
  const [vertical, setVertical] = useState<VerticalType>("Food");
  const [rnc, setRnc] = useState("");
  const [description, setDescription] = useState("");
  const [slugTouched, setSlugTouched] = useState(false);

  // Prefill from the backend once.
  useEffect(() => {
    if (!merchant.data) return;
    setName((prev) => prev || merchant.data.name);
    setSlug((prev) => prev || merchant.data.slug);
    setVertical((prev) => prev ?? merchant.data.verticalType);
    setRnc((prev) => prev || (merchant.data.rnc ?? ""));
    setDescription((prev) => prev || (merchant.data.description ?? ""));
  }, [merchant.data]);

  // Auto-derive slug from name unless the user has typed in it themselves.
  useEffect(() => {
    if (slugTouched) return;
    if (name.trim().length === 0) return;
    setSlug(slugify(name));
  }, [name, slugTouched]);

  const save = useMutation({
    mutationFn: (req: UpdateMerchantProfileRequest) => updateMyMerchant(req),
    onSuccess: (data) => {
      queryClient.setQueryData(["merchant", "me"], data);
      router.push("/onboarding/horarios");
    },
    onError: (err) => toast.error(describeError(err)),
  });

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    save.mutate({
      name: name.trim(),
      slug: slug.trim(),
      rnc: rnc.trim() || null,
      description: description.trim() || null,
      verticalType: vertical,
    });
  }

  const canContinue =
    name.trim().length > 0 &&
    slug.trim().length > 0 &&
    rnc.trim().length > 0 &&
    !save.isPending;

  return (
    <form onSubmit={onSubmit}>
      <StepShell
        stepNumber={1}
        title="Datos básicos"
        description="Cómo se llama tu comercio, su URL pública y qué vendes."
        onContinue={() => onSubmit(new Event("submit") as unknown as FormEvent)}
        continueDisabled={!canContinue}
        continueLabel={save.isPending ? "Guardando..." : "Continuar"}
      >
        <div className="grid gap-5 md:grid-cols-2">
          <div className="space-y-1.5">
            <Label htmlFor="name">
              Nombre del comercio <span className="text-destructive">*</span>
            </Label>
            <Input
              id="name"
              required
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Pizza Vesuvio"
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="slug">
              URL pública <span className="text-destructive">*</span>
            </Label>
            <Input
              id="slug"
              required
              value={slug}
              onChange={(e) => {
                setSlug(slugify(e.target.value));
                setSlugTouched(true);
              }}
              placeholder="pizza-vesuvio"
            />
            <p className="text-xs text-muted-foreground">rappix.com/c/{slug || "tu-comercio"}</p>
          </div>
        </div>

        <div className="mt-6 space-y-3">
          <Label>
            Vertical del comercio <span className="text-destructive">*</span>
          </Label>
          <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-3">
            {VERTICALS.map((v) => {
              const Icon = v.Icon;
              const active = vertical === v.value;
              return (
                <button
                  type="button"
                  key={v.value}
                  onClick={() => setVertical(v.value)}
                  className={cn(
                    "flex items-start gap-3 rounded-xl border-2 p-4 text-left transition-colors",
                    active
                      ? "border-brand bg-brand-50"
                      : "border-border bg-white hover:border-brand-300",
                  )}
                >
                  <div
                    className={cn(
                      "flex h-10 w-10 shrink-0 items-center justify-center rounded-lg",
                      active ? "bg-brand text-white" : "bg-muted text-foreground/70",
                    )}
                  >
                    <Icon className="h-5 w-5" />
                  </div>
                  <div className="min-w-0">
                    <p className="text-sm font-semibold text-foreground">{v.label}</p>
                    <p className="mt-0.5 text-xs text-muted-foreground">{v.caption}</p>
                  </div>
                </button>
              );
            })}
          </div>
        </div>

        <div className="mt-6 grid gap-5 md:grid-cols-2">
          <div className="space-y-1.5">
            <Label htmlFor="rnc">
              RNC <span className="text-destructive">*</span>
            </Label>
            <Input
              id="rnc"
              required
              value={rnc}
              onChange={(e) => setRnc(e.target.value)}
              placeholder="1-30-12345-6"
            />
            <p className="text-xs text-muted-foreground">Tu Registro Nacional del Contribuyente</p>
          </div>
        </div>

        <div className="mt-6 space-y-1.5">
          <Label htmlFor="description">Descripción</Label>
          <Textarea
            id="description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="Cuéntale al cliente qué hace especial a tu comercio (máx. 280 caracteres)."
            maxLength={280}
            rows={4}
          />
          <p className="text-xs text-muted-foreground">
            Cómo verá tu comercio el cliente. Máximo 280 caracteres.
          </p>
        </div>
      </StepShell>
    </form>
  );
}
