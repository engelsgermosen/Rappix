import type { ReactNode } from "react";

import { RequireAuth } from "@/components/auth/require-auth";
import { RequireMerchantStatus } from "@/components/auth/require-merchant-status";
import { ConsoleSidebar } from "@/components/layout/console-sidebar";

/**
 * Operational console layout — only renders for merchants in Active/Paused.
 * Other statuses are redirected by RequireMerchantStatus.
 */
export default function ConsoleLayout({ children }: { children: ReactNode }) {
  return (
    <RequireAuth>
      <RequireMerchantStatus allow={["Active", "Paused"]}>
        <div className="flex min-h-screen bg-background">
          <ConsoleSidebar />
          <div className="flex-1">{children}</div>
        </div>
      </RequireMerchantStatus>
    </RequireAuth>
  );
}
