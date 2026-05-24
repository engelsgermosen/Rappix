"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useState, Suspense, type FormEvent } from "react";
import { toast } from "sonner";
import { Eye, EyeOff, Lock, Mail } from "lucide-react";

import { AuthShell } from "@/components/auth/auth-shell";
import { WrongPortalBanner } from "@/components/auth/wrong-portal-banner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";
import { ApiError } from "@/lib/api/errors";
import { useAuthStore, WrongPortalError } from "@/lib/auth/store";
import { describeError } from "@/lib/errors";

function LoginForm() {
  const router = useRouter();
  const search = useSearchParams();
  const redirectTo = search.get("next") ?? "/";

  const login = useAuthStore((s) => s.login);

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [remember, setRemember] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [wrongPortal, setWrongPortal] = useState<WrongPortalError | null>(null);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setWrongPortal(null);
    setSubmitting(true);
    try {
      await login({ identifier: email.trim(), password });
      toast.success("Bienvenido al panel.");
      router.push(redirectTo);
    } catch (err) {
      if (err instanceof WrongPortalError) {
        setWrongPortal(err);
      } else if (err instanceof ApiError) {
        toast.error(describeError(err));
      } else {
        toast.error(describeError(err));
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <h2 className="text-3xl font-bold text-foreground">Inicia sesión</h2>
        <p className="text-sm text-muted-foreground">Accede al panel de gestión de tu comercio.</p>
      </div>

      {wrongPortal ? <WrongPortalBanner error={wrongPortal} /> : null}

      <form onSubmit={onSubmit} className="space-y-4" noValidate>
        <div className="space-y-1.5">
          <Label htmlFor="email">Correo electrónico</Label>
          <div className="relative">
            <Mail className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              id="email"
              type="email"
              autoComplete="email"
              required
              placeholder="comercio@rappix.test"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="pl-9"
            />
          </div>
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="password">Contraseña</Label>
          <div className="relative">
            <Lock className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              id="password"
              type={showPassword ? "text" : "password"}
              autoComplete="current-password"
              required
              minLength={1}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="pl-9 pr-9"
            />
            <button
              type="button"
              onClick={() => setShowPassword((s) => !s)}
              className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
              aria-label={showPassword ? "Ocultar contraseña" : "Mostrar contraseña"}
            >
              {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
            </button>
          </div>
        </div>

        <div className="flex items-center justify-between text-sm">
          <label className="flex cursor-pointer items-center gap-2 text-foreground/80">
            <input
              type="checkbox"
              checked={remember}
              onChange={(e) => setRemember(e.target.checked)}
              className="h-4 w-4 rounded border-input text-brand focus:ring-brand"
            />
            Recuérdame
          </label>
          <span className="text-brand opacity-50">¿Olvidaste tu contraseña?</span>
        </div>

        <Button type="submit" disabled={submitting} className="w-full" size="lg">
          {submitting ? "Entrando..." : "Entrar al panel"}
        </Button>
      </form>

      <div className="flex items-center gap-4 text-xs uppercase text-muted-foreground">
        <Separator className="flex-1" />
        <span>o</span>
        <Separator className="flex-1" />
      </div>

      <Button asChild variant="outline" size="lg" className="w-full">
        <Link href="/register">Registrar nuevo comercio</Link>
      </Button>

      <p className="text-center text-sm text-muted-foreground">
        ¿Eres cliente y quieres pedir? Visita <span className="font-semibold text-brand">rappix.com</span>.
      </p>
    </div>
  );
}

export default function LoginPage() {
  return (
    <AuthShell
      tagline="Tu comercio, conectado a miles de clientes."
      helper="Recibe pedidos, gestiona tu catálogo y mira en tiempo real qué se está vendiendo. Sin instalaciones."
    >
      <Suspense fallback={null}>
        <LoginForm />
      </Suspense>
    </AuthShell>
  );
}
