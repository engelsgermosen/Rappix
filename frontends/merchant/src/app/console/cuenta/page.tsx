"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState, type FormEvent } from "react";
import { useMutation } from "@tanstack/react-query";
import { toast } from "sonner";
import { LogOut } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { changePassword } from "@/lib/api/identity";
import { useAuthStore } from "@/lib/auth/store";
import { describeError } from "@/lib/errors";
import { initials } from "@/lib/utils";

export default function CuentaPage() {
  const router = useRouter();
  const user = useAuthStore((s) => s.user);
  const updateProfile = useAuthStore((s) => s.updateProfile);
  const logout = useAuthStore((s) => s.logout);

  // ---- Datos personales -------------------------------------------------
  const [firstName, setFirstName] = useState(user?.firstName ?? "");
  const [lastName, setLastName] = useState(user?.lastName ?? "");
  const [phone, setPhone] = useState(user?.phoneNumber ?? "");

  useEffect(() => {
    if (!user) return;
    setFirstName(user.firstName);
    setLastName(user.lastName);
    setPhone(user.phoneNumber ?? "");
  }, [user]);

  const profileDirty =
    !!user &&
    (firstName !== user.firstName ||
      lastName !== user.lastName ||
      (phone || null) !== (user.phoneNumber ?? null));

  const saveProfile = useMutation({
    mutationFn: () =>
      updateProfile({
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        phoneNumber: phone.trim() || null,
      }),
    onSuccess: () => toast.success("Datos personales guardados."),
    onError: (err) => toast.error(describeError(err)),
  });

  // ---- Seguridad --------------------------------------------------------
  const [currentPwd, setCurrentPwd] = useState("");
  const [newPwd, setNewPwd] = useState("");
  const [confirmPwd, setConfirmPwd] = useState("");

  const changePwd = useMutation({
    mutationFn: () => changePassword({ currentPassword: currentPwd, newPassword: newPwd }),
    onSuccess: () => {
      toast.success("Contraseña actualizada. Vuelve a entrar con la nueva.");
      setCurrentPwd("");
      setNewPwd("");
      setConfirmPwd("");
      // The backend revokes all refresh tokens; the next 401 will boot to /login.
      logout().then(() => router.replace("/login"));
    },
    onError: (err) => toast.error(describeError(err)),
  });

  function onChangePwd(e: FormEvent) {
    e.preventDefault();
    if (newPwd.length < 8) {
      toast.error("La nueva contraseña debe tener al menos 8 caracteres.");
      return;
    }
    if (newPwd !== confirmPwd) {
      toast.error("Las contraseñas no coinciden.");
      return;
    }
    changePwd.mutate();
  }

  if (!user) {
    return <section className="px-8 py-10"><Card className="h-64 animate-pulse" /></section>;
  }

  return (
    <section className="space-y-6 px-8 py-8">
      <header>
        <h1 className="text-3xl font-bold text-foreground">Mi cuenta</h1>
        <p className="mt-1 text-sm text-muted-foreground">Datos personales y seguridad.</p>
      </header>

      {/* Datos personales */}
      <Card className="overflow-hidden">
        <div className="border-b border-border px-6 py-4">
          <h2 className="text-lg font-bold text-foreground">Datos personales</h2>
          <p className="text-xs text-muted-foreground">Solo tú ves esta información.</p>
        </div>
        <form
          onSubmit={(e: FormEvent) => {
            e.preventDefault();
            saveProfile.mutate();
          }}
          className="flex flex-wrap items-start gap-6 px-6 py-5"
        >
          <div className="flex h-20 w-20 shrink-0 items-center justify-center rounded-full bg-brand text-2xl font-bold text-white">
            {initials(`${firstName} ${lastName}`)}
          </div>
          <div className="grid flex-1 gap-4 md:grid-cols-2">
            <div className="space-y-1.5">
              <Label htmlFor="acc-first">Nombre</Label>
              <Input id="acc-first" value={firstName} onChange={(e) => setFirstName(e.target.value)} required />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="acc-last">Apellido</Label>
              <Input id="acc-last" value={lastName} onChange={(e) => setLastName(e.target.value)} required />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="acc-email">Correo electrónico</Label>
              <Input id="acc-email" type="email" value={user.email} disabled />
              <p className="text-xs text-muted-foreground">El correo no es editable desde aquí.</p>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="acc-phone">Teléfono</Label>
              <Input
                id="acc-phone"
                type="tel"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                placeholder="+1 809 555 0000"
              />
            </div>
            <div className="md:col-span-2 flex justify-end">
              <Button type="submit" disabled={!profileDirty || saveProfile.isPending}>
                {saveProfile.isPending ? "Guardando..." : "Guardar datos personales"}
              </Button>
            </div>
          </div>
        </form>
      </Card>

      {/* Seguridad */}
      <Card className="overflow-hidden">
        <div className="border-b border-border px-6 py-4">
          <h2 className="text-lg font-bold text-foreground">Seguridad</h2>
          <p className="text-xs text-muted-foreground">Cambia tu contraseña periódicamente.</p>
        </div>
        <form onSubmit={onChangePwd} className="space-y-4 px-6 py-5">
          <div className="grid gap-4 md:grid-cols-3">
            <div className="space-y-1.5">
              <Label htmlFor="pwd-current">Contraseña actual</Label>
              <Input
                id="pwd-current"
                type="password"
                value={currentPwd}
                onChange={(e) => setCurrentPwd(e.target.value)}
                required
                autoComplete="current-password"
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="pwd-new">Nueva contraseña</Label>
              <Input
                id="pwd-new"
                type="password"
                value={newPwd}
                onChange={(e) => setNewPwd(e.target.value)}
                required
                minLength={8}
                autoComplete="new-password"
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="pwd-confirm">Confirmar nueva</Label>
              <Input
                id="pwd-confirm"
                type="password"
                value={confirmPwd}
                onChange={(e) => setConfirmPwd(e.target.value)}
                required
                minLength={8}
                autoComplete="new-password"
              />
            </div>
          </div>
          <div className="flex justify-end">
            <Button type="submit" disabled={changePwd.isPending}>
              {changePwd.isPending ? "Actualizando..." : "Actualizar contraseña"}
            </Button>
          </div>
        </form>
      </Card>

      {/* Cerrar sesión */}
      <Card className="overflow-hidden">
        <div className="flex flex-wrap items-center justify-between gap-4 px-6 py-5">
          <div>
            <h2 className="text-lg font-bold text-foreground">Cerrar sesión</h2>
            <p className="text-xs text-muted-foreground">
              Cerrarás sesión en este navegador. Los pedidos seguirán entrando, los verás al volver.
            </p>
          </div>
          <Button
            type="button"
            variant="outline"
            className="border-reject/30 text-reject hover:bg-reject-50"
            onClick={async () => {
              await logout();
              router.replace("/login");
            }}
          >
            <LogOut className="h-4 w-4" />
            Cerrar sesión
          </Button>
        </div>
      </Card>
    </section>
  );
}
