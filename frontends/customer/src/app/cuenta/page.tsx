"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { CheckCircle2, Mail, Phone, ShieldCheck, User as UserIcon } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

import { RequireAuth } from "@/components/auth/require-auth";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { apiFetch } from "@/lib/api/client";
import { ApiError } from "@/lib/api/errors";
import type { UserResponse } from "@/lib/api/types";
import { useAuthStore } from "@/lib/auth/store";
import { initials } from "@/lib/utils";

export default function AccountPage() {
  return (
    <RequireAuth fallback={<div className="container py-16 text-center text-muted-foreground">Cargando…</div>}>
      <AccountContent />
    </RequireAuth>
  );
}

function AccountContent() {
  const updateProfile = useAuthStore((s) => s.updateProfile);

  const meQ = useQuery({
    queryKey: ["auth", "me"],
    queryFn: () => apiFetch<UserResponse>("/api/v1/auth/me"),
  });

  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [bootstrapped, setBootstrapped] = useState(false);

  if (meQ.data && !bootstrapped) {
    setFirstName(meQ.data.firstName);
    setLastName(meQ.data.lastName);
    setPhoneNumber(meQ.data.phoneNumber ?? "");
    setBootstrapped(true);
  }

  const saveMut = useMutation({
    mutationFn: () =>
      updateProfile({
        firstName,
        lastName,
        phoneNumber: phoneNumber.trim() || null,
      }),
    onSuccess: () => toast.success("Perfil actualizado"),
    onError: (err) =>
      toast.error(err instanceof ApiError ? err.display() : "No pudimos actualizar el perfil"),
  });

  if (meQ.isLoading || !meQ.data) {
    return (
      <div className="container py-6 md:py-8 max-w-2xl space-y-4">
        <Skeleton className="h-24 w-full" />
        <Skeleton className="h-48 w-full" />
      </div>
    );
  }

  const user = meQ.data;

  return (
    <div className="container py-6 md:py-8 max-w-2xl space-y-5 animate-fade-in">
      {/* Header card */}
      <section className="rounded-2xl border border-border bg-white p-5 flex items-center gap-4">
        <span className="inline-flex h-14 w-14 items-center justify-center rounded-full bg-accent text-white font-bold text-lg">
          {initials(`${user.firstName} ${user.lastName}`)}
        </span>
        <div className="min-w-0 flex-1">
          <div className="font-bold text-lg truncate">
            {user.firstName} {user.lastName}
          </div>
          <div className="text-sm text-muted-foreground truncate flex items-center gap-1.5">
            <Mail className="h-3.5 w-3.5" />
            {user.email}
            {user.emailConfirmed && <CheckCircle2 className="h-3.5 w-3.5 text-emerald-600" />}
          </div>
        </div>
        <div className="hidden sm:flex flex-col items-end text-xs text-muted-foreground">
          <span className="inline-flex items-center gap-1">
            <ShieldCheck className="h-3.5 w-3.5" />
            {user.userType}
          </span>
          <span>Miembro desde {new Date(user.createdAtUtc).toLocaleDateString("es-DO")}</span>
        </div>
      </section>

      {/* Profile form */}
      <section className="rounded-2xl border border-border bg-white p-5">
        <h2 className="font-bold text-lg">Información personal</h2>
        <p className="text-sm text-muted-foreground">Esta es la información que usamos para tus pedidos.</p>
        <Separator className="my-4" />
        <form
          className="space-y-4"
          onSubmit={(e) => {
            e.preventDefault();
            saveMut.mutate();
          }}
        >
          <div className="grid sm:grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="firstName">Nombre</Label>
              <Input id="firstName" value={firstName} onChange={(e) => setFirstName(e.target.value)} required />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="lastName">Apellido</Label>
              <Input id="lastName" value={lastName} onChange={(e) => setLastName(e.target.value)} required />
            </div>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="email">Correo</Label>
            <div className="relative">
              <Mail className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
              <Input id="email" type="email" value={user.email} disabled className="pl-9" />
            </div>
            <p className="text-xs text-muted-foreground">Para cambiar el correo escribe a soporte.</p>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="phone">Teléfono</Label>
            <div className="relative">
              <Phone className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
              <Input
                id="phone"
                type="tel"
                value={phoneNumber}
                onChange={(e) => setPhoneNumber(e.target.value)}
                placeholder="+18091234567"
                className="pl-9"
              />
            </div>
          </div>
          <Button type="submit" disabled={saveMut.isPending} size="lg" className="w-full sm:w-auto">
            {saveMut.isPending ? "Guardando…" : "Guardar cambios"}
          </Button>
        </form>
      </section>

      {/* Security card placeholder */}
      <section className="rounded-2xl border border-border bg-white p-5">
        <div className="flex items-start gap-3">
          <span className="inline-flex h-10 w-10 items-center justify-center rounded-lg bg-brand-100 text-brand-700">
            <UserIcon className="h-5 w-5" />
          </span>
          <div className="flex-1">
            <h2 className="font-bold">Seguridad</h2>
            <p className="text-sm text-muted-foreground">Cambia tu contraseña periódicamente para mantener tu cuenta segura.</p>
          </div>
          <Button variant="outline" size="sm">Cambiar contraseña</Button>
        </div>
      </section>
    </div>
  );
}
