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

export default function LoginPage() {
  return (
    <Suspense fallback={<div className="container py-16 text-center text-muted-foreground">Cargando…</div>}>
      <LoginForm />
    </Suspense>
  );
}

function LoginForm() {
  const router = useRouter();
  const params = useSearchParams();
  const redirectTo = params.get("redirect") ?? "/";
  const login = useAuthStore((s) => s.login);

  const [identifier, setIdentifier] = useState("");
  const [password, setPassword] = useState("");
  const [submitting, setSubmitting] = useState(false);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    try {
      await login({ identifier, password });
      toast.success("¡Bienvenido de vuelta!");
      router.push(redirectTo);
    } catch (err) {
      const message = err instanceof ApiError ? err.display() : "No pudimos iniciar sesión";
      toast.error(message);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <AuthShell
      title="Iniciar sesión"
      subtitle="Usa tu correo o teléfono para entrar a tu cuenta."
      footer={
        <>
          ¿Nuevo en Rappix?{" "}
          <Link href={`/register${params.get("redirect") ? `?redirect=${params.get("redirect")}` : ""}`} className="font-medium text-brand hover:underline">
            Crea tu cuenta
          </Link>
        </>
      }
    >
      <form className="space-y-4" onSubmit={onSubmit}>
        <div className="space-y-1.5">
          <Label htmlFor="identifier">Correo o teléfono</Label>
          <Input
            id="identifier"
            type="text"
            autoComplete="username"
            required
            value={identifier}
            onChange={(e) => setIdentifier(e.target.value)}
            placeholder="ana@ejemplo.com"
          />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="password">Contraseña</Label>
          <Input
            id="password"
            type="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            placeholder="••••••••"
          />
        </div>
        <Button type="submit" size="lg" className="w-full" disabled={submitting}>
          {submitting ? "Entrando…" : "Entrar"}
        </Button>
      </form>
      <p className="text-xs text-muted-foreground text-center">
        Al iniciar sesión aceptas los Términos y la Política de privacidad de Rappix.
      </p>
    </AuthShell>
  );
}
