import Link from "next/link";
import { getGlossaryEntry } from "./glossary";

/**
 * Wraps a term with a short plain-language explanation shown on hover or keyboard focus,
 * linking to the full glossary entry. Falls back to the plain text for unknown ids.
 */
export default function GlossaryTerm({ id, children }: { id: string; children: React.ReactNode }) {
  const entry = getGlossaryEntry(id);
  if (!entry) return <>{children}</>;

  const tipId = `gloss-${id}`;
  return (
    <span className="group relative inline-block">
      <span
        tabIndex={0}
        aria-describedby={tipId}
        className="cursor-help border-b border-dotted border-slate-400 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-blue rounded-sm"
      >
        {children}
      </span>
      <span
        id={tipId}
        role="tooltip"
        className="invisible absolute start-0 top-full z-50 mt-1 w-64 rounded-lg border border-slate-200 bg-white p-2.5 text-start text-xs font-normal leading-relaxed text-slate-700 shadow-lg group-hover:visible group-focus-within:visible"
      >
        <span className="block">{entry.definition}</span>
        <span className="mt-1 block text-[10px] text-slate-500" dir="ltr">
          {entry.english} — {entry.englishGloss}
        </span>
        <Link href={`/glossary#${entry.id}`} className="mt-1 inline-block text-[11px] font-semibold text-indigo-700 underline">
          المزيد في قاموس المصطلحات
        </Link>
      </span>
    </span>
  );
}
