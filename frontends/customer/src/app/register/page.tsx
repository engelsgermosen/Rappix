"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useState } from "react";
import { toast } from "sonner";

import { AuthShell } from "@/components/auth/auth-shell";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/api/errors";
import { useAuthStore } from "@/lib/auth/store";

export default function RegisterPage() {
  return (
    <Suspense fallback={<div className="container py-16 text-center text-muted-foreground">Cargando…</div>}>
      <RegisterForm />
    </Suspense>
  );
}

function RegisterForm() {
  const router = useRouter();
  const params = useSearchParams();
  const redirectTo = params.get("redirect") ?? "/";
  const register = useAuthStore((s) => s.register);

  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState("");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [password, setPassword] = useState("");
  const [submitting, setSubmitting] = useState(false);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    try {
      await register({
        firstName,
        lastName,
        email,
        phoneNumber: phoneNumber.trim() || null,
        password,
      });
      toast.success("¡Cuenta creada! Te enviamos un correo de confirmación.");
      router.push(redirectTo);
    } catch (err) {
      const message = err instanceof ApiError ? err.display() : "No pudimos crear la cuenta";
      toast.error(message);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <AuthShell
      title="Crear cuenta"
      subtitle="Solo necesitamos unos datos para empezar."
      footer={
        <>
          ¿Ya tienes cuenta?{" "}
          <Link href={`/login${params.get("redirect") ? `?redirect=${params.get("redirect")}` : ""}`} className="font-medium text-brand hover:underline">
            Iniciar sesión
          </Link>
        </>
      }
    >
      <form className="space-y-4" onSubmit={onSubmit}>
        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1.5">
            <Label htmlFor="firstName">Nombre</Label>
            <Input id="firstName" required value={firstName} onChange={(e) => setFirstName(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="lastName">Apellido</Label>
            <Input id="lastName" required value={lastName} onChange={(e) => setLastName(e.target.value)} />
          </div>
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="email">Correo electrónico</Label>
          <Input id="email" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} placeholder="ana@ejemplo.com" />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="phone">Teléfono (opcional)</Label>
          <Input id="phone" type="tel" value={phoneNumber} onChange={(e) => setPhoneNumber(e.target.value)} placeholder="+18091234567" />
          <p className="text-xs text-muted-foreground">Formato internacional (E.164). Ejemplo: +18091234567</p>
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="password">Contraseña</Label>
          <Input id="password" type="password" required value={password} onChange={(e) => setPassword(e.target.value)} placeholder="Mínimo 8 caracteres, 1 letra y 1 número" />
        </div>
        <Button type="submit" size="lg" className="w-full" disabled={submitting}>
          {submitting ? "Creando…" : "Crear cuenta"}
        </Button>
      </form>
    </AuthShell>
  );
}
