"use client";

import { useHadithSearch } from "@/features/search/hooks/useHadithSearch";
import { Search, Book, GitCompareArrows, CheckSquare, Square, Loader2 } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { getRelatedHadiths } from "@/lib/api";

export default function SearchPage() {
  const { query, setQuery, data: results, isLoading, isError } = useHadithSearch();
  const router = useRouter();

  const [takhreejMode, setTakhreejMode] = useState(false);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [isAutoTakhreejLoading, setIsAutoTakhreejLoading] = useState<string | null>(null);

  const toggleSelection = (id: string) => {
    const newSet = new Set(selectedIds);
    if (newSet.has(id)) {
      newSet.delete(id);
    } else {
      newSet.add(id);
    }
    setSelectedIds(newSet);
  };

  const handleCardClick = (hadithId: string) => {
    if (takhreejMode) {
      toggleSelection(hadithId);
    } else {
      router.push(`/tree/${hadithId}`);
    }
  };

  const handleAutoTakhreej = async (e: React.MouseEvent, hadithId: string) => {
    e.stopPropagation();
    setIsAutoTakhreejLoading(hadithId);
    try {
      const related = await getRelatedHadiths(hadithId);
      const allIds = [hadithId, ...related.map(r => r.id)];
      router.push(`/takhreej?ids=${allIds.join(',')}`);
    } catch (err) {
      console.error(err);
      alert('فشل في جلب الأحاديث المتعلقة.');
    } finally {
      setIsAutoTakhreejLoading(null);
    }
  };

  const handleTakhreejModeToggle = () => {
    setTakhreejMode(!takhreejMode);
    if (takhreejMode) {
      setSelectedIds(new Set());
    }
  };

  return (
    <div className="max-w-4xl mx-auto p-6 pt-12 relative pb-24">
      <div className="flex justify-between items-center mb-8">
        <h1 className="text-3xl font-bold text-brand-dark flex-1 text-center">
          ابحث في الأحاديث والأسانيد
        </h1>
        <button
          onClick={handleTakhreejModeToggle}
          className={`flex items-center gap-2 px-4 py-2 rounded-lg font-bold transition-all ${takhreejMode ? 'bg-brand-blue text-white shadow-md' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'}`}
        >
          <GitCompareArrows className="w-5 h-5" />
          تخريج
        </button>
      </div>

      <div className="relative mb-10">
        <div className="absolute inset-y-0 start-0 flex items-center ps-4 pointer-events-none">
          <Search className="w-6 h-6 text-slate-400" />
        </div>
        <input
          type="search"
          className="block w-full p-4 ps-12 text-lg text-slate-900 border-2 border-slate-200 rounded-xl bg-white focus:ring-brand-teal focus:border-brand-teal shadow-sm outline-none transition-all"
          placeholder="ابحث بمتن الحديث، اسم الراوي، أو المصدر..."
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />
      </div>

      <div>
        {isLoading && query.length > 2 && (
          <div className="text-center text-slate-500 py-10">جاري البحث...</div>
        )}

        {isError && (
          <div className="text-center text-red-500 py-10">حدث خطأ أثناء البحث. تأكد من تشغيل الخادم.</div>
        )}

        {!isLoading && results && results.length === 0 && query.length > 2 && (
          <div className="text-center text-slate-500 py-10">لا توجد نتائج مطابقة لبحثك.</div>
        )}

        {!isLoading && results && results.length > 0 && (
          <div className="space-y-4">
            <h2 className="text-lg font-semibold text-slate-700 mb-4">
              نتائج البحث ({results.length})
            </h2>
            {results.map((hadith) => {
              const isSelected = selectedIds.has(hadith.id);
              return (
                <div 
                  key={hadith.id}
                  className={`bg-white p-5 rounded-xl shadow-sm border transition-all cursor-pointer relative group ${isSelected ? 'border-brand-blue ring-1 ring-brand-blue bg-blue-50/30' : 'border-slate-100 hover:shadow-md hover:border-brand-teal'}`}
                  onClick={() => handleCardClick(hadith.id)}
                >
                  <div className="flex justify-between items-start mb-2">
                    <div className="flex items-center gap-2 text-brand-blue font-semibold">
                      {takhreejMode && (
                        <div className="ml-2 text-slate-400">
                          {isSelected ? <CheckSquare className="w-5 h-5 text-brand-blue" /> : <Square className="w-5 h-5" />}
                        </div>
                      )}
                      <Book className="w-4 h-4" />
                      <span>{hadith.bookName}</span>
                      <span className="text-slate-400 font-normal">|</span>
                      <span>حديث رقم {hadith.hadithNumber}</span>
                    </div>
                    <div className="flex items-center gap-2">
                      {hadith.chapter && (
                        <span className="text-xs bg-slate-100 text-slate-600 px-2 py-1 rounded">
                          {hadith.chapter}
                        </span>
                      )}
                      {!takhreejMode && (
                        <button
                          onClick={(e) => handleAutoTakhreej(e, hadith.id)}
                          disabled={isAutoTakhreejLoading === hadith.id}
                          className="flex items-center gap-1 text-xs bg-brand-blue/10 text-brand-blue px-2 py-1 rounded hover:bg-brand-blue/20 transition-colors"
                          title="البحث عن أحاديث متعلقة وعرض التخريج"
                        >
                          {isAutoTakhreejLoading === hadith.id ? (
                            <Loader2 className="w-4 h-4 animate-spin" />
                          ) : (
                            <GitCompareArrows className="w-4 h-4" />
                          )}
                          تخريج
                        </button>
                      )}
                    </div>
                  </div>
                  <p className="text-slate-700 text-lg leading-relaxed font-arabic mt-3">
                    {hadith.matnSnippet}
                  </p>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* Floating Action Bar for Takhreej Mode */}
      {takhreejMode && selectedIds.size > 0 && (
        <div className="fixed bottom-6 left-1/2 -translate-x-1/2 bg-white px-6 py-4 rounded-full shadow-2xl border border-slate-200 flex items-center gap-6 z-50 animate-in slide-in-from-bottom-5">
          <div className="text-slate-700 font-bold">
            تم تحديد <span className="text-brand-blue">{selectedIds.size}</span> أحاديث
          </div>
          <button
            onClick={() => router.push(`/takhreej?ids=${Array.from(selectedIds).join(',')}`)}
            disabled={selectedIds.size < 2}
            className={`px-6 py-2 rounded-full font-bold text-white transition-all ${selectedIds.size >= 2 ? 'bg-brand-blue hover:bg-blue-700 shadow-md' : 'bg-slate-300 cursor-not-allowed'}`}
          >
            عرض شجرة التخريج
          </button>
        </div>
      )}
    </div>
  );
}
