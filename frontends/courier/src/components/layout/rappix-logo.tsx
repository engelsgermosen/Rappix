import { cn } from "@/lib/utils";

/**
 * Wordmark + sub-label del portal. La "R" en cuadro blanco sobre hero; sobre
 * fondo claro el cuadro va en brand color.
 */
export function RappixLogo({
  variant = "light",
  subtitle = "PORTAL REPARTIDOR",
  className,
}: {
  variant?: "light" | "dark";
  subtitle?: string;
  className?: string;
}) {
  const onDark = variant === "light"; // "light" = logo sobre fondo oscuro
  return (
    <div className={cn("flex items-center gap-3", className)}>
      <div
        className={cn(
          "flex h-12 w-12 items-center justify-center rounded-xl text-2xl font-bold",
          onDark ? "bg-white text-brand" : "bg-brand text-white",
        )}
      >
        R
      </div>
      <div className="flex flex-col leading-tight">
        <span className={cn("text-xl font-bold", onDark ? "text-white" : "text-brand")}>
          Rappix
        </span>
        <span
          className={cn(
            "text-[10px] font-semibold uppercase tracking-[0.18em]",
            onDark ? "text-white/70" : "text-brand-700/70",
          )}
        >
          {subtitle}
        </span>
      </div>
    </div>
  );
}
