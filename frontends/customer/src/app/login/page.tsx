"use client";

import { AlertTriangle } from "lucide-react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useState } from "react";
import { toast } from "sonner";

import { AuthShell } from "@/components/auth/auth-shell";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/api/errors";
import { useAuthStore, WrongPortalError } from "@/lib/auth/store";

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
  // Persistent banner shown when the entered credentials are for the wrong
  // portal. We do NOT redirect anywhere — each portal is its own app.
  const [wrongPortal, setWrongPortal] = useState<WrongPortalError | null>(null);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    setWrongPortal(null);
    try {
      await login({ identifier, password });
      toast.success("¡Bienvenido de vuelta!");
      router.push(redirectTo);
    } catch (err) {
      if (err instanceof WrongPortalError) {
        // Show the persistent banner; no toast (it's the primary feedback).
        setWrongPortal(err);
      } else if (err instanceof ApiError) {
        toast.error(err.display());
      } else {
        toast.error("No pudimos iniciar sesión");
      }
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
          <Link
            href={`/register${params.get("redirect") ? `?redirect=${params.get("redirect")}` : ""}`}
            className="font-medium text-brand hover:underline"
          >
            Crea tu cuenta
          </Link>
        </>
      }
    >
      {wrongPortal && (
        <div
          role="alert"
          className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900 flex gap-3"
        >
          <AlertTriangle className="h-5 w-5 flex-shrink-0 mt-0.5 text-amber-600" />
          <div>
            <div className="font-semibold">Esta cuenta no es de cliente</div>
            <p className="mt-0.5 leading-relaxed">{wrongPortal.message}</p>
            <p className="mt-1 text-xs text-amber-700/80">
              Este portal es solo para clientes. Tu sesión no se inició y no guardamos credenciales.
            </p>
          </div>
        </div>
      )}

      <form className="space-y-4" onSubmit={onSubmit}>
        <div className="space-y-1.5">
          <Label htmlFor="identifier">Correo o teléfono</Label>
          <Input
            id="identifier"
            type="text"
            autoComplete="username"
            required
            value={identifier}
            onChange={(e) => {
              setIdentifier(e.target.value);
              setWrongPortal(null);
            }}
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
            onChange={(e) => {
              setPassword(e.target.value);
              setWrongPortal(null);
            }}
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
