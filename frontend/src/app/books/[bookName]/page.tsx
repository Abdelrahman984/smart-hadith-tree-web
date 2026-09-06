"use client";

import { getChapters } from "@/lib/api";
import { useQuery } from "@tanstack/react-query";
import { ListFilter } from "lucide-react";
import Link from "next/link";
import { use } from "react";

export default function BookChaptersPage({ params }: { params: Promise<{ bookName: string }> }) {
  const resolvedParams = use(params);
  const bookName = decodeURIComponent(resolvedParams.bookName);

  const { data: chapters, isLoading, isError } = useQuery({
    queryKey: ['chapters', bookName],
    queryFn: () => getChapters(bookName),
  });

  return (
    <div className="max-w-4xl mx-auto p-6 pt-12">
      <div className="mb-8">
        <Link href="/books" className="text-brand-blue hover:underline mb-2 inline-block">
          &rarr; العودة إلى الكتب
        </Link>
        <h1 className="text-3xl font-bold text-brand-dark text-center">
          أبواب {bookName}
        </h1>
      </div>

      {isLoading && (
        <div className="text-center text-slate-500 py-10">جاري التحميل...</div>
      )}

      {isError && (
        <div className="text-center text-red-500 py-10">حدث خطأ أثناء جلب الأبواب.</div>
      )}

      {!isLoading && chapters && chapters.length === 0 && (
        <div className="text-center text-slate-500 py-10">لا توجد أبواب متاحة لهذا الكتاب.</div>
      )}

      {!isLoading && chapters && chapters.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {chapters.map((chapter) => (
            <Link 
              key={chapter}
              href={`/books/${encodeURIComponent(bookName)}/chapters/${encodeURIComponent(chapter)}`}
              className="bg-white p-5 rounded-xl shadow-sm border border-slate-100 hover:shadow-md hover:border-brand-teal transition-all flex items-center gap-4"
            >
              <div className="w-10 h-10 bg-brand-teal/10 rounded-xl flex items-center justify-center text-brand-teal">
                <ListFilter className="w-5 h-5" />
              </div>
              <span className="text-lg font-semibold text-slate-800">{chapter}</span>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
