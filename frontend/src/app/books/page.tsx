"use client";

import { getBooks } from "@/lib/api";
import { useQuery } from "@tanstack/react-query";
import { Book } from "lucide-react";
import Link from "next/link";

export default function BooksPage() {
  const { data: books, isLoading, isError } = useQuery({
    queryKey: ['books'],
    queryFn: getBooks,
  });

  return (
    <div className="max-w-4xl mx-auto p-6 pt-12">
      <h1 className="text-3xl font-bold text-brand-dark mb-8 text-center">
        تصفح كتب الحديث
      </h1>

      {isLoading && (
        <div className="text-center text-slate-500 py-10">جاري التحميل...</div>
      )}

      {isError && (
        <div className="text-center text-red-500 py-10">حدث خطأ أثناء جلب الكتب. تأكد من تشغيل الخادم.</div>
      )}

      {!isLoading && books && books.length === 0 && (
        <div className="text-center text-slate-500 py-10">لا توجد كتب متاحة.</div>
      )}

      {!isLoading && books && books.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {books.map((book) => (
            <Link 
              key={book}
              href={`/books/${encodeURIComponent(book)}`}
              className="bg-white p-5 rounded-xl shadow-sm border border-slate-100 hover:shadow-md hover:border-brand-teal transition-all flex items-center gap-4"
            >
              <div className="w-12 h-12 bg-brand-blue/10 rounded-xl flex items-center justify-center text-brand-blue">
                <Book className="w-6 h-6" />
              </div>
              <span className="text-lg font-semibold text-slate-800">{book}</span>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
