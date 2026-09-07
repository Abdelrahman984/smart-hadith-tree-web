"use client";

import { useSearchParams } from "next/navigation";
import { Suspense, useMemo, useState } from "react";
import { useTakhreej } from "@/features/isnad-tree/hooks/useTakhreej";
import ComparativeTreeCanvas from "@/features/isnad-tree/components/ComparativeTreeCanvas";
import NarratorDrawer from "@/features/narrator-details/components/NarratorDrawer";
import Link from "next/link";
import { ArrowRight, ChevronLeft, ChevronRight } from "lucide-react";

function TakhreejContent() {
  const searchParams = useSearchParams();
  const idsParam = searchParams.get("ids");
  
  const hadithIds = useMemo(() => {
    return idsParam ? idsParam.split(',').filter(id => id.trim().length > 0) : [];
  }, [idsParam]);

  const { data, isLoading, isError } = useTakhreej(hadithIds);
  const [isSidebarOpen, setIsSidebarOpen] = useState(true);

  if (!idsParam || hadithIds.length < 2) {
    return (
      <div className="flex flex-col items-center justify-center h-screen space-y-4">
        <p className="text-xl text-slate-600">يرجى تحديد حديثين على الأقل للتخريج.</p>
        <Link href="/search" className="text-brand-blue hover:underline font-bold">
          العودة للبحث
        </Link>
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
        <Link href="/search" className="text-brand-blue hover:underline font-bold">
          العودة للبحث
        </Link>
      </div>
    );
  }

  const getBookColorClass = (book: string) => {
    switch(book) {
      case 'صحيح البخاري': return 'bg-blue-100 text-blue-800 border-blue-200';
      case 'صحيح مسلم': return 'bg-green-100 text-green-800 border-green-200';
      case 'سنن أبي داود': return 'bg-amber-100 text-amber-800 border-amber-200';
      case 'جامع الترمذي': return 'bg-purple-100 text-purple-800 border-purple-200';
      default: return 'bg-slate-100 text-slate-800 border-slate-200';
    }
  };

  return (
    <div className="h-screen w-full flex flex-col overflow-hidden bg-slate-50 relative" dir="rtl">
      {/* Header */}
      <header className="bg-white border-b border-slate-200 p-4 flex items-center justify-between z-10 shrink-0 shadow-sm">
        <div className="flex items-center gap-4">
          <Link href="/search" className="p-2 hover:bg-slate-100 rounded-full transition-colors" title="العودة للبحث">
            <ArrowRight className="w-6 h-6 text-slate-600" />
          </Link>
          <h1 className="text-xl font-bold text-slate-800">شجرة التخريج المقارنة</h1>
        </div>
        
        <div className="flex gap-2 items-center flex-wrap">
          {data.sources.map((source, idx) => (
            <div key={idx} className={`px-3 py-1 rounded-full border text-sm font-semibold flex items-center gap-1 ${getBookColorClass(source.bookName)}`}>
              <span>{source.bookName}</span>
              <span className="opacity-60">|</span>
              <span>{source.hadithNumber}</span>
            </div>
          ))}
        </div>
      </header>

      <div className="flex-1 flex overflow-hidden relative">
        {/* Main Canvas Area */}
        <div className="flex-1 relative h-full">
          <ComparativeTreeCanvas treeData={data} />
        </div>

        {/* Sidebar for Matn texts */}
        <div className={`transition-all duration-300 ease-in-out border-r border-slate-200 bg-white z-10 flex flex-col overflow-hidden ${isSidebarOpen ? 'w-80' : 'w-0 border-r-0'}`}>
          <div className="flex-1 overflow-y-auto p-4 space-y-6 min-w-[20rem]">
            <h2 className="font-bold text-lg text-slate-800 mb-4 whitespace-nowrap">المتون</h2>
            {data.sources.map((source, idx) => (
              <div key={idx} className="space-y-2">
                <div className={`inline-block px-2 py-1 rounded border text-xs font-bold ${getBookColorClass(source.bookName)}`}>
                  {source.bookName} - {source.hadithNumber}
                </div>
                <p className="text-slate-700 leading-relaxed font-arabic text-sm text-justify">
                  {source.matnSnippet}
                </p>
                <hr className="border-slate-100" />
              </div>
            ))}
          </div>
        </div>

        {/* Toggle Sidebar Button */}
        <button 
          onClick={() => setIsSidebarOpen(!isSidebarOpen)}
          className="absolute top-1/2 -translate-y-1/2 bg-white border border-l-0 border-slate-200 p-2 rounded-r-lg shadow-md z-20 text-slate-500 hover:text-brand-blue transition-all duration-300 ease-in-out flex items-center justify-center cursor-pointer"
          style={{ left: isSidebarOpen ? '20rem' : '0' }}
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
