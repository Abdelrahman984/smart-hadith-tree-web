"use client";

import EvidenceBadge from "@/components/EvidenceBadge";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import { Suspense, useEffect, useMemo, useState } from "react";
import { useTakhreej } from "@/features/isnad-tree/hooks/useTakhreej";
import ComparativeTreeCanvas from "@/features/isnad-tree/components/ComparativeTreeCanvas";
import NarratorDrawer from "@/features/narrator-details/components/NarratorDrawer";
import ReturnToSearchButton from "@/features/isnad-tree/components/ReturnToSearchButton";
import { ChevronLeft, ChevronRight, Copy, Check, BookOpen, ShieldAlert, ExternalLink, X } from "lucide-react";
import SiteHeader from "@/components/SiteHeader";
import { EmptyState, ErrorState, LoadingState } from "@/components/StateViews";
import IlalPanel from "@/features/ilal/components/IlalPanel";
import { useIlalStore } from "@/features/ilal/store/useIlalStore";
import { countVisibleFindings } from "@/features/ilal/utils/ilalLabels";
import { getBookMeta } from "@/lib/bookTheme";

const MAX_HEADER_SOURCES = 4;

function TakhreejContent() {
  const searchParams = useSearchParams();
  const idsParam = searchParams.get("ids");
  
  const hadithIds = useMemo(() => {
    return idsParam ? idsParam.split(',').filter(id => id.trim().length > 0) : [];
  }, [idsParam]);

  const { data, isLoading, isError, refetch } = useTakhreej(hadithIds);
  // On phones the sidebar covers the canvas, so it starts closed there.
  const [isSidebarOpen, setIsSidebarOpen] = useState(
    () => typeof window === "undefined" || !window.matchMedia("(max-width: 767px)").matches
  );
  const [copiedIdx, setCopiedIdx] = useState<number | null>(null);
  const [showAllSources, setShowAllSources] = useState(false);
  const [sidebarTab, setSidebarTab] = useState<"mutun" | "ilal">("mutun");
  const { setReport, reset } = useIlalStore();

  // Share the ilal report with the canvas so it can decorate edges and highlight narrators.
  useEffect(() => {
    setReport(data?.ilalReport ?? null);
  }, [data, setReport]);

  useEffect(() => reset, [reset]);

  const handleCopy = (text: string, idx: number) => {
    navigator.clipboard.writeText(text);
    setCopiedIdx(idx);
    setTimeout(() => setCopiedIdx(null), 2000);
  };

  if (!idsParam || hadithIds.length < 2) {
    return (
      <StatusScreen>
        <EmptyState
          title="يرجى تحديد حديثين على الأقل للتخريج"
          message="حدد الروايات من صفحة البحث، أو استخدم «تخريج فوري» على أي حديث لجمع رواياته تلقائياً."
        >
          <ReturnToSearchButton variant="button" label="العودة للبحث" />
        </EmptyState>
      </StatusScreen>
    );
  }

  if (isLoading) {
    return (
      <StatusScreen>
        <LoadingState message="جارٍ بناء شجرة التخريج المقارنة..." />
      </StatusScreen>
    );
  }

  if (isError || !data) {
    return (
      <StatusScreen>
        <ErrorState title="تعذر بناء شجرة التخريج" message="حدث خطأ أثناء جلب بيانات التخريج. تأكد من تشغيل الخادم ثم أعد المحاولة." onRetry={() => refetch()}>
          <ReturnToSearchButton variant="button" label="العودة للبحث" />
        </ErrorState>
      </StatusScreen>
    );
  }

  return (
    <div className="h-screen w-full flex flex-col overflow-hidden bg-surface-muted relative" dir="rtl">
      <SiteHeader variant="compact" />

      {/* Header */}
      <header className="bg-surface border-b border-line px-3 py-2.5 sm:p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-2 sm:gap-4 z-10 shrink-0 shadow-sm">
        <div className="flex items-center gap-3 shrink-0">
          <ReturnToSearchButton variant="button" label="العودة للبحث" />
          <h1 className="text-lg sm:text-xl font-bold text-ink">شجرة التخريج المقارنة</h1>
        </div>
        
        <div className="flex gap-2 items-center flex-nowrap overflow-x-auto sm:flex-wrap min-w-0 pb-0.5">
          {(showAllSources ? data.sources : data.sources.slice(0, MAX_HEADER_SOURCES)).map((source, idx) => {
            const meta = getBookMeta(source.bookName);
            return (
              <div
                key={idx}
                className={`shrink-0 whitespace-nowrap px-2.5 py-1 rounded-full border text-xs sm:text-sm font-semibold flex items-center gap-1.5 transition-colors ${meta.badgeClass}`}
              >
                <span
                  className="min-w-[18px] h-[18px] px-1 flex items-center justify-center text-[9px] text-white rounded-full font-bold shrink-0"
                  style={{ backgroundColor: meta.color }}
                >
                  {meta.code}
                </span>
                <span>{source.bookName}</span>
                <span className="opacity-50">|</span>
                <span>{source.hadithNumber}</span>
              </div>
            );
          })}
          {data.sources.length > MAX_HEADER_SOURCES && (
            <button
              type="button"
              onClick={() => setShowAllSources((v) => !v)}
              aria-expanded={showAllSources}
              title={showAllSources ? "إخفاء" : data.sources.slice(MAX_HEADER_SOURCES).map((s) => `${s.bookName} ${s.hadithNumber}`).join("، ")}
              className="shrink-0 px-2.5 py-1 rounded-full border border-slate-300 bg-slate-100 text-xs sm:text-sm font-bold text-slate-700 hover:bg-slate-200 transition-colors cursor-pointer"
            >
              {showAllSources ? "إخفاء" : `+${data.sources.length - MAX_HEADER_SOURCES}`}
            </button>
          )}
        </div>
      </header>

      <div className="flex-1 flex overflow-hidden relative">
        {/* Main Canvas Area */}
        <main id="main-content" className="flex-1 relative h-full">
          <ComparativeTreeCanvas treeData={data} />
        </main>

        {/* Sidebar for Matn texts */}
        <div
          id="takhreej-sidebar"
          className={`absolute inset-y-0 left-0 z-20 md:relative md:z-10 transition-all duration-300 ease-in-out border-r border-line bg-surface flex flex-col overflow-hidden shadow-xl md:shadow-none ${isSidebarOpen ? 'w-full sm:w-96' : 'w-0 border-r-0'}`}
        >
          <div className="flex-1 overflow-y-auto p-4 space-y-5 min-w-[100vw] sm:min-w-[24rem]">
            <div className="flex items-center justify-between md:hidden">
              <span className="text-sm font-bold text-ink">المتون والعلل</span>
              <button
                type="button"
                onClick={() => setIsSidebarOpen(false)}
                className="flex items-center gap-1 rounded-lg px-2 py-1 text-xs font-semibold text-ink-muted hover:bg-slate-100 cursor-pointer"
              >
                <X className="w-4 h-4" aria-hidden />
                <span>إغلاق والعودة للشجرة</span>
              </button>
            </div>
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
                {data.taqwiyahDetails && (
                  <p className="text-xs text-slate-700 leading-relaxed mt-1">
                    {data.taqwiyahDetails}
                  </p>
                )}
              </div>
            )}

            {/* Sidebar tabs */}
            <div className="flex rounded-lg bg-slate-100 p-1 text-sm font-semibold">
              <button
                onClick={() => setSidebarTab("mutun")}
                className={`flex flex-1 items-center justify-center gap-1.5 rounded-md py-1.5 transition-colors cursor-pointer ${sidebarTab === "mutun" ? "bg-surface text-brand-blue shadow-sm" : "text-ink-subtle hover:text-slate-700"}`}
              >
                <BookOpen className="w-4 h-4" /> المتون
              </button>
              <button
                onClick={() => setSidebarTab("ilal")}
                className={`flex flex-1 items-center justify-center gap-1.5 rounded-md py-1.5 transition-colors cursor-pointer ${sidebarTab === "ilal" ? "bg-surface text-brand-blue shadow-sm" : "text-ink-subtle hover:text-slate-700"}`}
              >
                <ShieldAlert className="w-4 h-4" /> العلل
                {data.ilalReport && countVisibleFindings(data.ilalReport.findings) > 0 && (
                  <span className={`rounded-full px-1.5 text-[11px] text-white ${data.ilalReport.hasQadihah ? "bg-red-500" : "bg-amber-500"}`}>
                    {countVisibleFindings(data.ilalReport.findings)}
                  </span>
                )}
              </button>
            </div>

            {sidebarTab === "ilal" && (
              data.ilalReport
                ? <IlalPanel report={data.ilalReport} />
                : <p className="text-sm text-ink-subtle">لا يتوفر فحص للعلل لهذه الطرق.</p>
            )}

            {sidebarTab === "mutun" && (<>
            <div className="flex items-center justify-between border-b border-slate-100 pb-2">
              <div className="flex items-center gap-2">
                <BookOpen className="w-5 h-5 text-brand-blue" />
                <h2 className="font-bold text-base text-ink">متون الروايات</h2>
              </div>
              <span className="text-xs bg-slate-100 text-ink-muted px-2 py-0.5 rounded-full font-semibold">
                {data.sources.length} {data.sources.length === 1 ? 'رواية' : 'روايات'}
              </span>
            </div>

            <div className="space-y-4">
              {data.sources.map((source, idx) => {
                const meta = getBookMeta(source.bookName);
                return (
                  <div key={idx} className="bg-surface-muted/70 p-3.5 rounded-xl border border-line/80 space-y-2.5">
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
                          onClick={() => handleCopy(source.matnArabic || source.matnSnippet, idx)}
                          className="p-1 rounded-md text-slate-400 hover:text-slate-700 hover:bg-slate-200/60 transition-colors cursor-pointer"
                          title="نسخ نص المتن"
                        >
                          {copiedIdx === idx ? (
                            <Check className="w-4 h-4 text-emerald-600" />
                          ) : (
                            <Copy className="w-4 h-4" />
                          )}
                        </button>
                      </div>
                    </div>
                    <p className="text-ink leading-loose font-arabic text-sm text-justify whitespace-pre-wrap select-text">
                      {source.matnArabic || source.matnSnippet}
                    </p>
                  </div>
                );
              })}
            </div>
            </>)}
          </div>
        </div>

        {/* Toggle Sidebar Button */}
        <button 
          onClick={() => setIsSidebarOpen(!isSidebarOpen)}
          aria-controls="takhreej-sidebar"
          aria-expanded={isSidebarOpen}
          className={`absolute top-1/2 -translate-y-1/2 bg-surface border border-l-0 border-line p-2 rounded-r-lg shadow-md z-20 text-ink-subtle hover:text-brand-blue transition-all duration-300 ease-in-out items-center justify-center gap-1 cursor-pointer ${
            isSidebarOpen ? "hidden md:flex md:left-96" : "flex left-0"
          }`}
          title={isSidebarOpen ? "إخفاء المتون" : "إظهار المتون"}
          aria-label={isSidebarOpen ? "إخفاء المتون" : "إظهار المتون"}
        >
          {isSidebarOpen ? <ChevronLeft className="w-5 h-5" /> : <ChevronRight className="w-5 h-5" />}
          {!isSidebarOpen && <span className="text-xs font-semibold md:hidden">المتون</span>}
        </button>
      </div>

      <NarratorDrawer />
    </div>
  );
}

/** Full-height page with the site header, for the empty / loading / error states. */
function StatusScreen({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex h-screen flex-col">
      <SiteHeader variant="compact" />
      <main id="main-content" className="flex flex-1 items-center justify-center p-4">
        {children}
      </main>
    </div>
  );
}

export default function TakhreejPageClient() {
  return (
    <Suspense fallback={<StatusScreen><LoadingState /></StatusScreen>}>
      <TakhreejContent />
    </Suspense>
  );
}
