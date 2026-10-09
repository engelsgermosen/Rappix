"use client";

import { useMutation } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useEffect, useState, type FormEvent } from "react";
import { toast } from "sonner";
import { LogOut } from "lucide-react";

import { AppHeader } from "@/components/courier/app-header";
import { RequireAuth } from "@/components/auth/require-auth";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useAuthStore } from "@/lib/auth/store";
import { useCourier } from "@/lib/courier/use-courier";
import { describeError } from "@/lib/errors";

function CuentaContent() {
  const router = useRouter();
  const courier = useCourier();
  const user = useAuthStore((s) => s.user);
  const updateProfile = useAuthStore((s) => s.updateProfile);
  const changePassword = useAuthStore((s) => s.changePassword);
  const logout = useAuthStore((s) => s.logout);

  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [phone, setPhone] = useState("");

  useEffect(() => {
    if (!user) return;
    setFirstName((prev) => prev || user.firstName);
    setLastName((prev) => prev || user.lastName);
    setPhone((prev) => prev || user.phoneNumber || "");
  }, [user]);

  const [currentPwd, setCurrentPwd] = useState("");
  const [newPwd, setNewPwd] = useState("");
  const [confirmPwd, setConfirmPwd] = useState("");

  const saveProfile = useMutation({
    mutationFn: () =>
      updateProfile({
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        phoneNumber: phone.trim() || null,
      }),
    onSuccess: () => toast.success("Perfil actualizado."),
    onError: (err) => toast.error(describeError(err)),
  });

  const changePwd = useMutation({
    mutationFn: () => changePassword({ currentPassword: currentPwd, newPassword: newPwd }),
    onSuccess: () => {
      toast.success("Contrasena actualizada. Vuelve a entrar con la nueva.");
      setCurrentPwd("");
      setNewPwd("");
      setConfirmPwd("");
      // El backend revoco todos los refresh tokens — el siguiente request 401
      // booteria al login, pero forzamos el logout para no esperar.
      logout().then(() => router.replace("/login"));
    },
    onError: (err) => toast.error(describeError(err)),
  });

  async function onLogout() {
    await logout();
    router.replace("/login");
  }

  function onSubmitProfile(e: FormEvent) {
    e.preventDefault();
    saveProfile.mutate();
  }

  function onSubmitPassword(e: FormEvent) {
    e.preventDefault();
    if (newPwd.length < 8) {
      toast.error("La nueva contrasena debe tener al menos 8 caracteres.");
      return;
    }
    if (newPwd !== confirmPwd) {
      toast.error("La nueva contrasena y su confirmacion no coinciden.");
      return;
    }
    changePwd.mutate();
  }

  return (
    <>
      <AppHeader status={courier.data?.status} />
      <main className="flex-1 px-4 py-8 md:px-6">
        <div className="mx-auto w-full max-w-[600px] space-y-6">
          <div className="space-y-1.5">
            <h1 className="text-2xl font-bold">Mi cuenta</h1>
            <p className="text-sm text-muted-foreground">Datos personales y seguridad.</p>
          </div>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Perfil</CardTitle>
            </CardHeader>
            <CardContent>
              <form onSubmit={onSubmitProfile} className="space-y-4" noValidate>
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
                  <Label htmlFor="email">Email</Label>
                  <Input id="email" value={user?.email ?? ""} disabled readOnly />
                  <p className="text-xs text-muted-foreground">Para cambiar el email contacta a soporte.</p>
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
                <Button type="submit" disabled={saveProfile.isPending}>
                  {saveProfile.isPending ? "Guardando..." : "Guardar cambios"}
                </Button>
              </form>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Cambiar contrasena</CardTitle>
            </CardHeader>
            <CardContent>
              <form onSubmit={onSubmitPassword} className="space-y-4" noValidate>
                <div className="space-y-1.5">
                  <Label htmlFor="currentPwd">Contrasena actual</Label>
                  <Input
                    id="currentPwd"
                    type="password"
                    required
                    autoComplete="current-password"
                    value={currentPwd}
                    onChange={(e) => setCurrentPwd(e.target.value)}
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="newPwd">Nueva contrasena</Label>
                  <Input
                    id="newPwd"
                    type="password"
                    required
                    minLength={8}
                    autoComplete="new-password"
                    value={newPwd}
                    onChange={(e) => setNewPwd(e.target.value)}
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="confirmPwd">Confirmar nueva contrasena</Label>
                  <Input
                    id="confirmPwd"
                    type="password"
                    required
                    minLength={8}
                    autoComplete="new-password"
                    value={confirmPwd}
                    onChange={(e) => setConfirmPwd(e.target.value)}
                  />
                </div>
                <p className="text-xs text-muted-foreground">
                  Al cambiar la contrasena cerraremos tu sesion en todos los dispositivos.
                </p>
                <Button type="submit" disabled={changePwd.isPending}>
                  {changePwd.isPending ? "Cambiando..." : "Cambiar contrasena"}
                </Button>
              </form>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="flex items-center justify-between p-5">
              <div>
                <p className="font-semibold">Cerrar sesion en este dispositivo</p>
                <p className="text-xs text-muted-foreground">
                  No te desconecta del trabajo — solo te saca del portal.
                </p>
              </div>
              <Button type="button" variant="outline" onClick={onLogout}>
                <LogOut className="mr-1.5 h-4 w-4" /> Cerrar sesion
              </Button>
            </CardContent>
          </Card>
        </div>
      </main>
    </>
  );
}

export default function CuentaPage() {
  return (
    <RequireAuth>
      <CuentaContent />
    </RequireAuth>
  );
}
