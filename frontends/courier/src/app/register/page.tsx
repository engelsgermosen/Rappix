"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { toast } from "sonner";

import { AuthShell } from "@/components/auth/auth-shell";
import { WrongPortalBanner } from "@/components/auth/wrong-portal-banner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useAuthStore, WrongPortalError } from "@/lib/auth/store";
import { describeError } from "@/lib/errors";

export default function RegisterPage() {
  const router = useRouter();
  const register = useAuthStore((s) => s.register);

  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [password, setPassword] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [wrongPortal, setWrongPortal] = useState<WrongPortalError | null>(null);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setWrongPortal(null);
    setSubmitting(true);
    try {
      await register({
        email: email.trim(),
        password,
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        phoneNumber: phone.trim() ? phone.trim() : null,
      });
      toast.success("Cuenta creada. Configura tu vehiculo para empezar.");
      router.push("/");
    } catch (err) {
      if (err instanceof WrongPortalError) {
        setWrongPortal(err);
      } else {
        toast.error(describeError(err));
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <AuthShell
      tagline="Entrega con Rappix."
      helper="Crea tu cuenta de repartidor en menos de un minuto. Despues configuras tu vehiculo y te conectas."
    >
      <div className="space-y-6">
        <div className="space-y-2">
          <h2 className="text-3xl font-bold text-foreground">Registrarme</h2>
          <p className="text-sm text-muted-foreground">
            Crearemos tu cuenta de repartidor. El siguiente paso es configurar tu vehiculo.
          </p>
        </div>

        {wrongPortal ? <WrongPortalBanner error={wrongPortal} /> : null}

        <form onSubmit={onSubmit} className="space-y-4" noValidate>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="firstName">Nombre</Label>
              <Input
                id="firstName"
                required
                autoComplete="given-name"
                value={firstName}
                onChange={(e) => setFirstName(e.target.value)}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="lastName">Apellido</Label>
              <Input
                id="lastName"
                required
                autoComplete="family-name"
                value={lastName}
                onChange={(e) => setLastName(e.target.value)}
              />
            </div>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="email">Correo electronico</Label>
            <Input
              id="email"
              type="email"
              required
              autoComplete="email"
              placeholder="tu@ejemplo.com"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="phone">Telefono (opcional)</Label>
            <Input
              id="phone"
              type="tel"
              autoComplete="tel"
              placeholder="+1 809 555 0000"
              value={phone}
              onChange={(e) => setPhone(e.target.value)}
            />
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="password">Contrasena</Label>
            <Input
              id="password"
              type="password"
              required
              minLength={8}
              autoComplete="new-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
            <p className="text-xs text-muted-foreground">Minimo 8 caracteres, con letra y numero.</p>
          </div>

          <Button type="submit" disabled={submitting} className="w-full" size="lg">
            {submitting ? "Creando cuenta..." : "Crear cuenta de repartidor"}
          </Button>
        </form>

        <p className="text-center text-sm text-muted-foreground">
          Ya tienes cuenta?{" "}
          <Link href="/login" className="font-semibold text-brand hover:underline">
            Inicia sesion
          </Link>
        </p>
      </div>
    </AuthShell>
  );
}
