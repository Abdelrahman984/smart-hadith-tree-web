"use client";

import { useSearchParams } from "next/navigation";
import Link from "next/link";
import { Suspense, useEffect, useMemo, useState } from "react";
import { useTakhreej } from "@/features/isnad-tree/hooks/useTakhreej";
import ComparativeTreeCanvas from "@/features/isnad-tree/components/ComparativeTreeCanvas";
import NarratorDrawer from "@/features/narrator-details/components/NarratorDrawer";
import ReturnToSearchButton from "@/features/isnad-tree/components/ReturnToSearchButton";
import { ChevronLeft, ChevronRight, Copy, Check, BookOpen, ShieldAlert, ExternalLink } from "lucide-react";
import IlalPanel from "@/features/ilal/components/IlalPanel";
import { useIlalStore } from "@/features/ilal/store/useIlalStore";
import { getBookMeta } from "@/lib/bookTheme";

function TakhreejContent() {
  const searchParams = useSearchParams();
  const idsParam = searchParams.get("ids");
  
  const hadithIds = useMemo(() => {
    return idsParam ? idsParam.split(',').filter(id => id.trim().length > 0) : [];
  }, [idsParam]);

  const { data, isLoading, isError } = useTakhreej(hadithIds);
  const [isSidebarOpen, setIsSidebarOpen] = useState(true);
  const [copiedIdx, setCopiedIdx] = useState<number | null>(null);
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
      <div className="flex flex-col items-center justify-center h-screen space-y-4">
        <p className="text-xl text-slate-600">يرجى تحديد حديثين على الأقل للتخريج.</p>
        <ReturnToSearchButton variant="button" label="العودة للبحث" />
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-screen">
        <p className="text-2xl text-slate-600 animate-pulse">جاري بناء شجرة التخريج المقارنة...</p>
      </div>
    );
  }

  if (isError || !data) {
    return (
      <div className="flex flex-col items-center justify-center h-screen space-y-4">
        <p className="text-xl text-red-600">حدث خطأ أثناء جلب بيانات التخريج.</p>
        <ReturnToSearchButton variant="button" label="العودة للبحث" />
      </div>
    );
  }

  return (
    <div className="h-screen w-full flex flex-col overflow-hidden bg-slate-50 relative" dir="rtl">
      {/* Header */}
      <header className="bg-white border-b border-slate-200 p-4 flex items-center justify-between gap-4 z-10 shrink-0 shadow-sm">
        <div className="flex items-center gap-3 shrink-0">
          <ReturnToSearchButton variant="button" label="العودة للبحث" />
          <h1 className="text-xl font-bold text-slate-800">شجرة التخريج المقارنة</h1>
        </div>
        
        <div className="flex gap-2 items-center flex-wrap max-h-20 overflow-y-auto">
          {data.sources.map((source, idx) => {
            const meta = getBookMeta(source.bookName);
            return (
              <div
                key={idx}
                className={`px-2.5 py-1 rounded-full border text-xs sm:text-sm font-semibold flex items-center gap-1.5 transition-colors ${meta.badgeClass}`}
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
        </div>
      </header>

      <div className="flex-1 flex overflow-hidden relative">
        {/* Main Canvas Area */}
        <div className="flex-1 relative h-full">
          <ComparativeTreeCanvas treeData={data} />
        </div>

        {/* Sidebar for Matn texts */}
        <div className={`transition-all duration-300 ease-in-out border-r border-slate-200 bg-white z-10 flex flex-col overflow-hidden ${isSidebarOpen ? 'w-96' : 'w-0 border-r-0'}`}>
          <div className="flex-1 overflow-y-auto p-4 space-y-5 min-w-[24rem]">
            {data.calculatedGrade && (
              <div className="bg-emerald-50 border border-emerald-200 rounded-xl p-3.5">
                <h3 className="font-bold text-emerald-900 mb-1 flex items-center gap-2 text-sm">
                  <span>الحكم الكلي:</span>
                  <span className="text-emerald-700 bg-emerald-100/70 px-2 py-0.5 rounded-md">{data.calculatedGrade}</span>
                </h3>
                {data.taqwiyahDetails && (
                  <p className="text-xs text-emerald-800 leading-relaxed mt-1">
                    {data.taqwiyahDetails}
                  </p>
                )}
              </div>
            )}

            {/* Sidebar tabs */}
            <div className="flex rounded-lg bg-slate-100 p-1 text-sm font-semibold">
              <button
                onClick={() => setSidebarTab("mutun")}
                className={`flex flex-1 items-center justify-center gap-1.5 rounded-md py-1.5 transition-colors cursor-pointer ${sidebarTab === "mutun" ? "bg-white text-brand-blue shadow-sm" : "text-slate-500 hover:text-slate-700"}`}
              >
                <BookOpen className="w-4 h-4" /> المتون
              </button>
              <button
                onClick={() => setSidebarTab("ilal")}
                className={`flex flex-1 items-center justify-center gap-1.5 rounded-md py-1.5 transition-colors cursor-pointer ${sidebarTab === "ilal" ? "bg-white text-brand-blue shadow-sm" : "text-slate-500 hover:text-slate-700"}`}
              >
                <ShieldAlert className="w-4 h-4" /> العلل
                {data.ilalReport && data.ilalReport.findings.length > 0 && (
                  <span className={`rounded-full px-1.5 text-[11px] text-white ${data.ilalReport.hasQadihah ? "bg-red-500" : "bg-amber-500"}`}>
                    {data.ilalReport.findings.length}
                  </span>
                )}
              </button>
            </div>

            {sidebarTab === "ilal" && (
              data.ilalReport
                ? <IlalPanel report={data.ilalReport} />
                : <p className="text-sm text-slate-500">لا يتوفر فحص للعلل لهذه الطرق.</p>
            )}

            {sidebarTab === "mutun" && (<>
            <div className="flex items-center justify-between border-b border-slate-100 pb-2">
              <div className="flex items-center gap-2">
                <BookOpen className="w-5 h-5 text-brand-blue" />
                <h2 className="font-bold text-base text-slate-800">متون الروايات</h2>
              </div>
              <span className="text-xs bg-slate-100 text-slate-600 px-2 py-0.5 rounded-full font-semibold">
                {data.sources.length} {data.sources.length === 1 ? 'رواية' : 'روايات'}
              </span>
            </div>

            <div className="space-y-4">
              {data.sources.map((source, idx) => {
                const meta = getBookMeta(source.bookName);
                return (
                  <div key={idx} className="bg-slate-50/70 p-3.5 rounded-xl border border-slate-200/80 space-y-2.5">
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
                    <p className="text-slate-800 leading-loose font-arabic text-sm text-justify whitespace-pre-wrap select-text">
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
          className="absolute top-1/2 -translate-y-1/2 bg-white border border-l-0 border-slate-200 p-2 rounded-r-lg shadow-md z-20 text-slate-500 hover:text-brand-blue transition-all duration-300 ease-in-out flex items-center justify-center cursor-pointer"
          style={{ left: isSidebarOpen ? '24rem' : '0' }}
          title={isSidebarOpen ? "إخفاء المتون" : "إظهار المتون"}
          aria-label={isSidebarOpen ? "إخفاء المتون" : "إظهار المتون"}
        >
          {isSidebarOpen ? <ChevronLeft className="w-5 h-5" /> : <ChevronRight className="w-5 h-5" />}
        </button>
      </div>

      <NarratorDrawer />
    </div>
  );
}

export default function TakhreejPage() {
  return (
    <Suspense fallback={<div className="h-screen flex items-center justify-center text-xl">جاري التحميل...</div>}>
      <TakhreejContent />
    </Suspense>
  );
}
