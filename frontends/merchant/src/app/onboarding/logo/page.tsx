"use client";

import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { ImageIcon, Send, Upload } from "lucide-react";

import { StepShell } from "@/components/onboarding/step-shell";
import { Button } from "@/components/ui/button";
import { getMyMerchant, submitForApproval, uploadLogo } from "@/lib/api/merchants-me";
import { getLogoUrl } from "@/lib/api/merchants-public";
import { ApiError, isApiCode } from "@/lib/api/errors";
import { describeError } from "@/lib/errors";

const MAX_BYTES = 2 * 1024 * 1024;

export default function LogoPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const merchant = useQuery({ queryKey: ["merchant", "me"], queryFn: getMyMerchant });
  const logoUrl = useQuery({
    queryKey: ["merchant", "logo-url", merchant.data?.id],
    queryFn: () => getLogoUrl(merchant.data!.id),
    enabled: !!merchant.data?.id && !!merchant.data?.logoObjectKey,
    staleTime: 50 * 60_000,
  });

  const [preview, setPreview] = useState<string | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  // Clean up the Blob URL when the preview changes or unmounts.
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
      toast.success("Logo cargado.");
    },
    onError: (err) => toast.error(describeError(err)),
  });

  const submit = useMutation({
    mutationFn: submitForApproval,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["merchant", "me"] });
      toast.success("Tu comercio fue enviado a revisión.");
      router.push("/en-revision");
    },
    onError: (err) => {
      if (err instanceof ApiError && isApiCode(err, "Merchants.Merchant.IncompleteForSubmission")) {
        toast.error(describeError(err));
      } else if (err instanceof ApiError && err.status === 403) {
        toast.error("Debes confirmar tu email antes de enviar a revisión.");
      } else {
        toast.error(describeError(err));
      }
    },
  });

  function onPick(file: File) {
    if (file.size > MAX_BYTES) {
      toast.error("El logo supera el tamaño máximo de 2 MB.");
      return;
    }
    if (!file.type.startsWith("image/")) {
      toast.error("El archivo debe ser una imagen (JPG, PNG o WebP).");
      return;
    }
    const url = URL.createObjectURL(file);
    setPreview(url);
    upload.mutate(file);
  }

  const hasLogo = !!merchant.data?.logoObjectKey;
  const displayUrl = preview ?? logoUrl.data ?? null;

  return (
    <StepShell
      stepNumber={5}
      title="Logo del comercio"
      description="Sube una imagen cuadrada (mínimo 600×600 px, hasta 2 MB). Es lo primero que verá el cliente."
      onBack={() => router.push("/onboarding/ubicacion")}
      onContinue={() => submit.mutate()}
      continueDisabled={submit.isPending}
      continueLabel={submit.isPending ? "Enviando..." : "Enviar a revisión"}
    >
      <div className="grid gap-6 lg:grid-cols-[200px,1fr]">
        <div className="flex flex-col items-center gap-3">
          <div className="flex h-44 w-44 items-center justify-center overflow-hidden rounded-2xl border-2 border-dashed border-border bg-muted/30">
            {displayUrl ? (
              // eslint-disable-next-line @next/next/no-img-element
              <img src={displayUrl} alt="Logo" className="h-full w-full object-cover" />
            ) : (
              <ImageIcon className="h-10 w-10 text-muted-foreground" />
            )}
          </div>
          <Button
            type="button"
            variant="outline"
            onClick={() => fileRef.current?.click()}
            disabled={upload.isPending}
          >
            <Upload className="h-4 w-4" />
            {upload.isPending ? "Subiendo..." : hasLogo ? "Cambiar logo" : "Subir logo"}
          </Button>
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

        <div className="space-y-3 rounded-xl border border-brand-100 bg-brand-50/40 p-5">
          <p className="text-sm font-semibold text-brand-700">Antes de enviar a revisión</p>
          <ul className="space-y-2 text-sm text-foreground/80">
            <li className="flex items-start gap-2">
              <Send className="mt-0.5 h-4 w-4 shrink-0 text-brand" />
              <span>
                Completa los 5 pasos del wizard. Si falta algo, el botón te devolverá un error con el campo
                pendiente.
              </span>
            </li>
            <li className="flex items-start gap-2">
              <Send className="mt-0.5 h-4 w-4 shrink-0 text-brand" />
              <span>
                Confirma tu correo electrónico. Si no confirmas, no podrás enviar a revisión.
              </span>
            </li>
            <li className="flex items-start gap-2">
              <Send className="mt-0.5 h-4 w-4 shrink-0 text-brand" />
              <span>
                Nuestro equipo revisa tu cuenta en 4-24 horas hábiles. Recibirás un correo al aprobarse.
              </span>
            </li>
          </ul>
        </div>
      </div>
    </StepShell>
  );
}
