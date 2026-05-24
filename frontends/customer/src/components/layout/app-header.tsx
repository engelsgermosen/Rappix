"use client";

import Link from "next/link";

import { AddressPill } from "./address-pill";
import { CartButton } from "./cart-button";
import { NotificationsButton } from "./notifications-button";
import { RappixLogo } from "./rappix-logo";
import { SearchBar } from "./search-bar";
import { UserMenu } from "./user-menu";

export function AppHeader() {
  return (
    <header className="sticky top-0 z-40 bg-brand text-white shadow-sm">
      <div className="container flex h-16 items-center gap-3 md:gap-4">
        <Link href="/" className="flex items-center">
          <RappixLogo />
        </Link>
        <div className="hidden md:block">
          <AddressPill />
        </div>
        <div className="hidden lg:flex flex-1 justify-center">
          <SearchBar />
        </div>
        <div className="ml-auto flex items-center gap-2">
          <NotificationsButton />
          <CartButton />
          <UserMenu />
        </div>
      </div>
      {/* Mobile-only second row with address + search */}
      <div className="container pb-3 lg:hidden flex flex-col gap-2">
        <div className="md:hidden">
          <AddressPill />
        </div>
        <SearchBar />
      </div>
    </header>
  );
}
