// Placeholder de la home autenticada — se reemplaza en el commit 4 con el
// home status-aware (Offline | Online idle | Busy). Este placeholder existe
// solo para que `next build` arranque limpio en el commit 1.
export default function HomePage() {
  return (
    <main className="flex flex-1 items-center justify-center p-6">
      <p className="text-sm text-muted-foreground">Cargando...</p>
    </main>
  );
}
