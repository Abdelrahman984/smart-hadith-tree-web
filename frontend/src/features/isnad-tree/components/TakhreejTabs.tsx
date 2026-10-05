"use client";

import Link from "next/link";
import { useState } from "react";
import { BookOpen, Check, Copy, ExternalLink } from "lucide-react";
import EvidenceBadge from "@/components/EvidenceBadge";
import IlalPanel from "@/features/ilal/components/IlalPanel";
import { getBookMeta } from "@/lib/bookTheme";
import type { ComparativeTreeResponseDto } from "@/types/api";

/** «المتون»: the automatic grade and the text of each compared narration. */
export function MatnTab({ data }: { data: ComparativeTreeResponseDto }) {
  const [copiedId, setCopiedId] = useState<string | null>(null);

  const handleCopy = (text: string, id: string) => {
    navigator.clipboard.writeText(text);
    setCopiedId(id);
    setTimeout(() => setCopiedId(null), 2000);
  };

  return (
    <>
      {data.calculatedGrade && (
        <div className="bg-surface-muted border border-line rounded-xl p-3.5">
          <h3 className="font-bold text-ink mb-1 flex items-center gap-2 text-sm">
            <span>التقدير الآلي للطرق:</span>
            <span className="text-ink bg-slate-200/70 px-2 py-0.5 rounded-md">{data.calculatedGrade}</span>
          </h3>
          <div className="mt-1.5 flex flex-wrap items-center gap-2">
            <EvidenceBadge status="verify" />
            <span className="text-[11px] text-ink">
              تقدير آلي من الأسانيد المسجلة وليس حكماً معتمداً؛ للحكم المعتمد راجع{" "}
              <a href="https://dorar.net/hadith" target="_blank" rel="noopener noreferrer" className="font-semibold underline">
                الدرر السنية
              </a>
              .
            </span>
          </div>
          {data.taqwiyahDetails && <p className="text-xs text-slate-700 leading-relaxed mt-1">{data.taqwiyahDetails}</p>}
        </div>
      )}

      <div className="flex items-center justify-between border-b border-slate-100 pb-2">
        <div className="flex items-center gap-2">
          <BookOpen className="w-5 h-5 text-brand-blue" />
          <h2 className="font-bold text-base text-ink">متون الروايات</h2>
        </div>
        <span className="text-xs bg-slate-100 text-ink-muted px-2 py-0.5 rounded-full font-semibold">
          {data.sources.length} {data.sources.length === 1 ? "رواية" : "روايات"}
        </span>
      </div>

      <div className="space-y-4">
        {data.sources.map((source) => {
          const meta = getBookMeta(source.bookName);
          const text = source.matnArabic || source.matnSnippet;
          return (
            <div
              id={`matn-card-${source.hadithId}`}
              key={source.hadithId}
              className="bg-surface-muted/70 p-3.5 rounded-xl border border-line/80 space-y-2.5 scroll-mt-2"
            >
              <div className="flex items-center justify-between gap-2">
                <div className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md border text-xs font-bold ${meta.badgeClass}`}>
                  <span
                    className="min-w-[18px] h-[18px] px-1 flex items-center justify-center text-[9px] text-white rounded-full font-bold shrink-0"
                    style={{ backgroundColor: meta.color }}
                  >
                    {meta.code}
                  </span>
                  <span>{source.bookName}</span>
                  <span className="opacity-50">•</span>
                  <span>حديث رقم {source.hadithNumber}</span>
                </div>
                <div className="flex items-center gap-1 shrink-0">
                  <Link
                    href={`/tree/${source.hadithId}`}
                    className="p-1 rounded-md text-slate-400 hover:text-brand-blue hover:bg-slate-200/60 transition-colors"
                    title="عرض شجرة هذه الرواية منفردة"
                  >
                    <ExternalLink className="w-4 h-4" />
                  </Link>
                  <button
                    onClick={() => handleCopy(text, source.hadithId)}
                    className="p-1 rounded-md text-slate-400 hover:text-slate-700 hover:bg-slate-200/60 transition-colors cursor-pointer"
                    title="نسخ نص المتن"
                  >
                    {copiedId === source.hadithId ? <Check className="w-4 h-4 text-emerald-600" /> : <Copy className="w-4 h-4" />}
                  </button>
                </div>
              </div>
              <p className="text-ink leading-loose font-arabic text-sm text-justify whitespace-pre-wrap select-text">{text}</p>
            </div>
          );
        })}
      </div>
    </>
  );
}

/** «العلل»: the findings the comparison carries. */
export function IlalTab({ data }: { data: ComparativeTreeResponseDto }) {
  return data.ilalReport ? (
    <IlalPanel report={data.ilalReport} />
  ) : (
    <p className="text-sm text-ink-subtle">لا يتوفر فحص للعلل لهذه الطرق.</p>
  );
}
