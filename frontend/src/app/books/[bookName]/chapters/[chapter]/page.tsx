"use client";

import { getBookHadiths } from "@/lib/api";
import { useQuery } from "@tanstack/react-query";
import { Book } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { use } from "react";

export default function ChapterHadithsPage({ params }: { params: Promise<{ bookName: string, chapter: string }> }) {
  const resolvedParams = use(params);
  const bookName = decodeURIComponent(resolvedParams.bookName);
  const chapter = decodeURIComponent(resolvedParams.chapter);
  const router = useRouter();

  const { data: hadiths, isLoading, isError } = useQuery({
    queryKey: ['hadiths', bookName, chapter],
    queryFn: () => getBookHadiths(bookName, chapter),
  });

  return (
    <div className="max-w-4xl mx-auto p-6 pt-12">
      <div className="mb-8">
        <Link href={`/books/${encodeURIComponent(bookName)}`} className="text-brand-blue hover:underline mb-2 inline-block">
          &rarr; العودة إلى أبواب {bookName}
        </Link>
        <h1 className="text-3xl font-bold text-brand-dark text-center">
          {chapter}
        </h1>
      </div>

      {isLoading && (
        <div className="text-center text-slate-500 py-10">جاري التحميل...</div>
      )}

      {isError && (
        <div className="text-center text-red-500 py-10">حدث خطأ أثناء جلب الأحاديث.</div>
      )}

      {!isLoading && hadiths && hadiths.length === 0 && (
        <div className="text-center text-slate-500 py-10">لا توجد أحاديث في هذا الباب.</div>
      )}

      {!isLoading && hadiths && hadiths.length > 0 && (
        <div className="space-y-4">
          <h2 className="text-lg font-semibold text-slate-700 mb-4">
            الأحاديث ({hadiths.length})
          </h2>
          {hadiths.map((hadith) => (
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
              </div>
              <p className="text-slate-800 text-base md:text-lg leading-loose font-arabic mt-3 text-justify select-text whitespace-pre-wrap">
                {hadith.matnArabic || hadith.matnSnippet}
              </p>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
