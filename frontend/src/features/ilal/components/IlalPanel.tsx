"use client";

import { useState } from "react";
import { ShieldAlert, ShieldCheck, GitFork, FileDiff } from "lucide-react";
import { IlalReportDto } from "@/types/api";
import { useIlalStore } from "../store/useIlalStore";
import { groupFindings, ILLAH_TYPE_LABELS, SEVERITY_STYLES } from "../utils/ilalLabels";
import MatnDiffView from "./MatnDiffView";
import IlalAiExplanation from "./IlalAiExplanation";

interface IlalPanelProps {
  report: IlalReportDto;
}

/**
 * Lists the findings of an Ilal report grouped by type. Selecting a finding highlights its
 * narrators on the canvas; textual findings can expand an aligned matn comparison.
 */
export default function IlalPanel({ report }: IlalPanelProps) {
  const { activeFindingIndex, selectFinding } = useIlalStore();
  const [openDiffIndex, setOpenDiffIndex] = useState<number | null>(null);
  const groups = groupFindings(report.findings);

  const tariqLabel = (hadithId: string) => {
    const t = report.turuq.find((x) => x.hadithId === hadithId);
    return t ? `${t.bookName} ${t.hadithNumber}` : null;
  };

  return (
    <div className="space-y-4" dir="rtl">
      {/* Summary */}
      <div
        className={`rounded-xl border p-3.5 ${report.hasQadihah ? "border-red-200 bg-red-50" : "border-emerald-200 bg-emerald-50"}`}
      >
        <h3 className={`mb-1 flex items-center gap-2 text-sm font-bold ${report.hasQadihah ? "text-red-900" : "text-emerald-900"}`}>
          {report.hasQadihah ? <ShieldAlert className="h-4 w-4" /> : <ShieldCheck className="h-4 w-4" />}
          نتيجة فحص العلل
        </h3>
        <p className={`text-xs leading-relaxed ${report.hasQadihah ? "text-red-800" : "text-emerald-800"}`}>
          {report.summaryAr}
        </p>
      </div>

      {/* Madars */}
      {report.madars.length > 0 && (
        <div className="rounded-xl border border-amber-200 bg-amber-50/50 p-3">
          <h4 className="mb-2 flex items-center gap-1.5 text-xs font-bold text-amber-900">
            <GitFork className="h-3.5 w-3.5" /> مدار الطرق
          </h4>
          <div className="flex flex-wrap gap-1.5">
            {report.madars.slice(0, 6).map((m) => (
              <span key={m.narratorId} className="rounded-full border border-amber-200 bg-white px-2 py-0.5 text-xs text-amber-900">
                {m.narratorName} <span className="opacity-60">({m.branchCount} فروع)</span>
              </span>
            ))}
          </div>
        </div>
      )}

      {/* Findings */}
      {groups.map((group) => (
        <section key={group.type} className="space-y-2">
          <h4 className="border-b border-slate-100 pb-1 text-sm font-bold text-slate-800">
            {ILLAH_TYPE_LABELS[group.type]}
            <span className="ms-2 rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-semibold text-slate-600">
              {group.items.length}
            </span>
          </h4>

          {group.items.map(({ finding, index }) => {
            const style = SEVERITY_STYLES[finding.severity];
            const isActive = activeFindingIndex === index;
            const labels = finding.hadithIds.map(tariqLabel).filter(Boolean);

            return (
              <div
                key={index}
                role="button"
                tabIndex={0}
                onClick={() => selectFinding(isActive ? null : index)}
                onKeyDown={(e) => e.key === "Enter" && selectFinding(isActive ? null : index)}
                className={`cursor-pointer rounded-xl border p-3 transition-shadow ${style.card} ${isActive ? "ring-2 ring-brand-blue shadow-md" : "hover:shadow-sm"}`}
              >
                <div className="mb-1.5 flex items-center justify-between gap-2">
                  <span className="text-sm font-bold text-slate-800">{finding.titleAr}</span>
                  <span className={`shrink-0 rounded-md border px-1.5 py-0.5 text-[10px] font-bold ${style.badge}`}>
                    {style.label}
                  </span>
                </div>
                <p className="text-xs leading-relaxed text-slate-700">{finding.evidenceAr}</p>

                <div className="mt-2 flex flex-wrap items-center gap-1.5 text-[10px] text-slate-500">
                  {labels.map((l) => (
                    <span key={l} className="rounded bg-white/80 px-1.5 py-0.5 border border-slate-200">{l}</span>
                  ))}
                  <span className="ms-auto" title="درجة الثقة في الاستنتاج الآلي">
                    الثقة: {Math.round(finding.confidence * 100)}%
                  </span>
                </div>

                {finding.matnComparison && (
                  <div onClick={(e) => e.stopPropagation()}>
                    <button
                      onClick={() => setOpenDiffIndex(openDiffIndex === index ? null : index)}
                      className="mt-2 flex items-center gap-1 text-[11px] font-semibold text-brand-blue hover:underline cursor-pointer"
                    >
                      <FileDiff className="h-3.5 w-3.5" />
                      {openDiffIndex === index ? "إخفاء مقارنة المتن" : "عرض مقارنة المتن"}
                    </button>
                    {openDiffIndex === index && (
                      <MatnDiffView comparison={finding.matnComparison} turuq={report.turuq} />
                    )}
                  </div>
                )}
              </div>
            );
          })}
        </section>
      ))}

      {report.findings.length > 0 && <IlalAiExplanation report={report} />}

      <p className="text-[10px] leading-relaxed text-slate-400">
        نتائج آلية مبنية على قواعد المحدثين في التدليس والاختلاط واللقاء والمخالفة، تُعين المحقق ولا تغني عن نظره في كتب العلل.
      </p>
    </div>
  );
}
