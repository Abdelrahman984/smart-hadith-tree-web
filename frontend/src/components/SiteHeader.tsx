import Link from "next/link";
import { BookOpen, Network, ScrollText, Search, ShieldCheck } from "lucide-react";
import NavLink from "./NavLink";

const NAV_ITEMS = [
  { href: "/search", short: "البحث", long: "البحث والتخريج", Icon: Search },
  { href: "/books", short: "الدواوين", long: "فهرس الدواوين", Icon: BookOpen },
  { href: "/verify", short: "التحقق", long: "تحقق من حديث", Icon: ShieldCheck },
  { href: "/glossary", short: "المصطلحات", long: "المصطلحات", Icon: ScrollText },
];

interface SiteHeaderProps {
  /**
   * "default": sticky bar for the content pages.
   * "compact": a thin, non-sticky bar for the full-height graph screens (tree, takhreej).
   */
  variant?: "default" | "compact";
  /** Page-specific status shown before the navigation (e.g. the takhreej selection count on search). */
  children?: React.ReactNode;
}

/** The one site header: logo and the four main sections, on every page. */
export default function SiteHeader({ variant = "default", children }: SiteHeaderProps) {
  const isCompact = variant === "compact";

  return (
    <header
      className={
        isCompact
          ? "shrink-0 z-30 bg-white border-b border-slate-200"
          : "sticky top-0 z-30 bg-white/90 backdrop-blur-md border-b border-slate-200/80"
      }
    >
      <div
        className={`mx-auto flex items-center justify-between gap-2 px-3 sm:px-6 ${
          isCompact ? "h-12" : "max-w-7xl h-14 sm:h-16"
        }`}
      >
        <Link href="/" className="group flex min-w-0 items-center gap-2.5" aria-label="شجرة الأسانيد الذكية: الرئيسية">
          <span
            className={`flex shrink-0 items-center justify-center rounded-xl bg-brand-blue text-brand-teal shadow-xs transition-transform group-hover:scale-105 ${
              isCompact ? "h-8 w-8" : "h-9 w-9"
            }`}
          >
            <Network className={isCompact ? "h-4 w-4" : "h-5 w-5"} aria-hidden />
          </span>
          <span className={`truncate font-bold text-brand-dark ${isCompact ? "hidden md:inline text-sm" : "hidden sm:inline text-base"}`}>
            شجرة الأسانيد <span className="text-brand-teal">الذكية</span>
          </span>
        </Link>

        <div className="flex min-w-0 items-center gap-2">
          {children}
          <nav aria-label="التنقل الرئيسي" className="flex items-center gap-0.5 sm:gap-1">
            {NAV_ITEMS.map(({ href, short, long, Icon }) => (
              <NavLink
                key={href}
                href={href}
                className="flex flex-col items-center gap-0.5 rounded-lg px-2 py-1 text-[11px] font-semibold text-slate-600 transition-colors hover:bg-slate-100 hover:text-brand-dark sm:flex-row sm:gap-1.5 sm:px-3 sm:py-1.5 sm:text-sm"
                activeClassName="bg-brand-blue/10 text-brand-blue"
              >
                <Icon className="h-4 w-4 shrink-0 text-brand-teal" aria-hidden />
                <span className="whitespace-nowrap lg:hidden">{short}</span>
                <span className="hidden whitespace-nowrap lg:inline">{long}</span>
              </NavLink>
            ))}
          </nav>
        </div>
      </div>
    </header>
  );
}
