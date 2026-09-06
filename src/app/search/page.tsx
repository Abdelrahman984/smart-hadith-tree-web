"use client";

import { useHadithSearch } from "@/features/search/hooks/useHadithSearch";
import { Search, Book } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";

export default function SearchPage() {
  const { query, setQuery, data: results, isLoading, isError } = useHadithSearch();
  const router = useRouter();

  return (
    <div className="max-w-4xl mx-auto p-6 pt-12">
      <h1 className="text-3xl font-bold text-brand-dark mb-8 text-center">
        ابحث في الأحاديث والأسانيد
      </h1>

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
            {results.map((hadith) => (
              <div 
                key={hadith.id}
                className="bg-white p-5 rounded-xl shadow-sm border border-slate-100 hover:shadow-md hover:border-brand-teal transition-all cursor-pointer"
                onClick={() => router.push(`/tree/${hadith.id}`)}
              >
                <div className="flex justify-between items-start mb-2">
                  <div className="flex items-center gap-2 text-brand-blue font-semibold">
                    <Book className="w-4 h-4" />
                    <span>{hadith.bookName}</span>
                    <span className="text-slate-400 font-normal">|</span>
                    <span>حديث رقم {hadith.hadithNumber}</span>
                  </div>
                  {hadith.chapter && (
                    <span className="text-xs bg-slate-100 text-slate-600 px-2 py-1 rounded">
                      {hadith.chapter}
                    </span>
                  )}
                </div>
                <p className="text-slate-700 text-lg leading-relaxed font-arabic mt-3">
                  {hadith.matnSnippet}
                </p>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
