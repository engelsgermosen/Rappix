"use client";

import { useEffect, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { ImageIcon, Upload } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { uploadLogo } from "@/lib/api/merchants-me";
import { getLogoUrl } from "@/lib/api/merchants-public";
import { describeError } from "@/lib/errors";
import type { MerchantResponse } from "@/lib/api/types";

const MAX_BYTES = 2 * 1024 * 1024;

export function LogoCard({ merchant }: { merchant: MerchantResponse }) {
  const queryClient = useQueryClient();
  const logoUrl = useQuery({
    queryKey: ["merchant", "logo-url", merchant.id],
    queryFn: () => getLogoUrl(merchant.id),
    enabled: !!merchant.logoObjectKey,
    staleTime: 50 * 60_000,
  });

  const [preview, setPreview] = useState<string | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    return () => {
      if (preview) URL.revokeObjectURL(preview);
    };
  }, [preview]);

  const upload = useMutation({
    mutationFn: (file: File) => uploadLogo(file),
    onSuccess: (data) => {
      queryClient.setQueryData(["merchant", "me"], data);
      queryClient.invalidateQueries({ queryKey: ["merchant", "logo-url"] });
      toast.success("Logo actualizado.");
    },
    onError: (err) => toast.error(describeError(err)),
  });

  function onPick(file: File) {
    if (file.size > MAX_BYTES) {
      toast.error("El logo supera el tamaño máximo de 2 MB.");
      return;
    }
    if (!file.type.startsWith("image/")) {
      toast.error("Solo imágenes.");
      return;
    }
    const url = URL.createObjectURL(file);
    if (preview) URL.revokeObjectURL(preview);
    setPreview(url);
    upload.mutate(file);
  }

  const displayUrl = preview ?? logoUrl.data ?? null;

  return (
    <Card className="overflow-hidden">
      <div className="border-b border-border px-6 py-4">
        <h2 className="text-lg font-bold text-foreground">Logo</h2>
        <p className="text-xs text-muted-foreground">Imagen cuadrada, máx. 2 MB.</p>
      </div>
      <div className="flex items-center gap-6 px-6 py-5">
        <div className="flex h-28 w-28 items-center justify-center overflow-hidden rounded-2xl border-2 border-dashed border-border bg-muted/30">
          {displayUrl ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img src={displayUrl} alt="Logo" className="h-full w-full object-cover" />
          ) : (
            <ImageIcon className="h-8 w-8 text-muted-foreground" />
          )}
        </div>
        <div className="flex flex-col gap-2">
          <Button type="button" variant="outline" onClick={() => fileRef.current?.click()} disabled={upload.isPending}>
            <Upload className="h-4 w-4" />
            {upload.isPending ? "Subiendo..." : merchant.logoObjectKey ? "Cambiar logo" : "Subir logo"}
          </Button>
          <p className="text-xs text-muted-foreground">JPG, PNG o WebP. Mínimo 600×600 px.</p>
        </div>
        <input
          ref={fileRef}
          type="file"
          accept="image/jpeg,image/png,image/webp"
          className="hidden"
          onChange={(e) => {
            const file = e.target.files?.[0];
            if (file) onPick(file);
            e.target.value = "";
          }}
        />
      </div>
    </Card>
  );
}
