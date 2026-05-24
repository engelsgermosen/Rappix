import { RappixMark } from "@/components/layout/rappix-logo";

export function AuthShell({ title, subtitle, children, footer }: {
  title: string;
  subtitle: string;
  children: React.ReactNode;
  footer: React.ReactNode;
}) {
  return (
    <div className="container max-w-md py-10 md:py-16 animate-fade-in">
      <div className="rounded-2xl border border-border bg-white p-6 md:p-8 shadow-sm">
        <div className="flex items-center gap-3 mb-6">
          <RappixMark size={40} />
          <div>
            <div className="font-bold text-xl tracking-tight">Rappix</div>
            <div className="text-xs text-muted-foreground">Cliente</div>
          </div>
        </div>
        <h1 className="text-2xl md:text-3xl font-bold tracking-tight">{title}</h1>
        <p className="text-sm text-muted-foreground mt-1">{subtitle}</p>
        <div className="mt-6 space-y-4">{children}</div>
      </div>
      <div className="mt-4 text-center text-sm text-muted-foreground">{footer}</div>
    </div>
  );
}
