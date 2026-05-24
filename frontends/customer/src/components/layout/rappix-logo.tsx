import { cn } from "@/lib/utils";

type Props = { className?: string; size?: number };

export function RappixLogo({ className, size = 32 }: Props) {
  return (
    <span className={cn("inline-flex items-center gap-2 select-none", className)}>
      <span
        className="inline-flex items-center justify-center rounded-lg bg-white text-brand font-extrabold leading-none shadow-sm"
        style={{ width: size, height: size, fontSize: size * 0.55 }}
      >
        R
      </span>
      <span className="font-bold tracking-tight text-white text-lg">Rappix</span>
    </span>
  );
}

export function RappixMark({ className, size = 32 }: Props) {
  return (
    <span
      className={cn(
        "inline-flex items-center justify-center rounded-lg bg-brand text-white font-extrabold leading-none shadow-sm",
        className,
      )}
      style={{ width: size, height: size, fontSize: size * 0.55 }}
    >
      R
    </span>
  );
}
