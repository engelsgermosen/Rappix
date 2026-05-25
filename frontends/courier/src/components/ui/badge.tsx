import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";

import { cn } from "@/lib/utils";

const badgeVariants = cva(
  "inline-flex items-center gap-1.5 rounded-md px-2.5 py-0.5 text-xs font-semibold transition-colors",
  {
    variants: {
      variant: {
        default: "bg-brand-100 text-brand-700",
        accent: "bg-accent text-white",
        muted: "bg-muted text-foreground/70",
        outline: "border border-border bg-white text-foreground/70",
        dark: "bg-foreground/80 text-white backdrop-blur",
        success: "bg-success-100 text-success-700",
        warn: "bg-amber-100 text-amber-700",
        danger: "bg-red-100 text-red-700",
        info: "bg-blue-100 text-blue-700",
        draft: "bg-muted text-foreground/70",
        pending: "bg-amber-100 text-amber-700",
        active: "bg-success-100 text-success-700",
        paused: "bg-amber-100 text-amber-700",
        suspended: "bg-red-100 text-red-700",
        rejected: "bg-red-100 text-red-700",
      },
    },
    defaultVariants: { variant: "default" },
  },
);

export interface BadgeProps extends React.HTMLAttributes<HTMLSpanElement>, VariantProps<typeof badgeVariants> {}

export function Badge({ className, variant, ...props }: BadgeProps) {
  return <span className={cn(badgeVariants({ variant }), className)} {...props} />;
}
