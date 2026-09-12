"use client";

import { useState, useRef, useEffect, useMemo, Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { useHadithSearch } from "@/features/search/hooks/useHadithSearch";
import { getRelatedHadiths } from "@/lib/api";
import HadithCard from "@/features/search/components/HadithCard";
import SearchSkeleton from "@/features/search/components/SearchSkeleton";
import SearchEmptyState from "@/features/search/components/SearchEmptyState";
import SearchBookFilters from "@/features/search/components/SearchBookFilters";
import TakhreejFloatingBar from "@/features/search/components/TakhreejFloatingBar";
import {
  Search,
  X,
  Loader2,
  GitCompareArrows,
  Network,
  BookOpen,
  Home,
  AlertCircle,
  RotateCcw,
  FilterX,
} from "lucide-react";

function SearchContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const initialQueryFromUrl = searchParams.get("q") || "";

  const {
    query,
    setQuery,
    debouncedQuery,
    data: results,
    isLoading,
    isFetching,
    isError,
    refetch,
  } = useHadithSearch(initialQueryFromUrl);

  const inputRef = useRef<HTMLInputElement>(null);
  const [takhreejMode, setTakhreejMode] = useState(false);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [isAutoTakhreejLoading, setIsAutoTakhreejLoading] = useState<string | null>(null);
  const [selectedBook, setSelectedBook] = useState<string | null>(null);

  // Sync URL when debounced query updates
  useEffect(() => {
    if (typeof window === "undefined") return;
    const url = new URL(window.location.href);
    if (debouncedQuery.trim().length > 2) {
      url.searchParams.set("q", debouncedQuery.trim());
    } else {
      url.searchParams.delete("q");
    }
    window.history.replaceState(null, "", url.toString());
  }, [debouncedQuery]);

  // Global shortcut "/" or "Ctrl+K" to focus search bar
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      const isInput = ["INPUT", "TEXTAREA"].includes((e.target as HTMLElement)?.tagName);
      if ((e.key === "/" && !isInput) || ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k")) {
        e.preventDefault();
        inputRef.current?.focus();
        inputRef.current?.select();
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, []);

  // Compute unique books and counts from the current search results
  const bookStats = useMemo(() => {
    if (!results || results.length === 0) return [];
    const map = new Map<string, number>();
    for (const item of results) {
      map.set(item.bookName, (map.get(item.bookName) || 0) + 1);
    }
    return Array.from(map.entries()).map(([name, count]) => ({ name, count }));
  }, [results]);

  // Filter results by selected book
  const filteredResults = useMemo(() => {
    if (!results) return [];
    if (!selectedBook) return results;
    return results.filter((h) => h.bookName === selectedBook);
  }, [results, selectedBook]);

  // Takhreej item selection
  const toggleSelection = (id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  const selectAllFiltered = () => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      filteredResults.forEach((h) => next.add(h.id));
      return next;
    });
  };

  const clearSelection = () => {
    setSelectedIds(new Set());
  };

  const handleTakhreejModeToggle = () => {
    const nextMode = !takhreejMode;
    setTakhreejMode(nextMode);
    if (!nextMode) {
      setSelectedIds(new Set());
    }
  };

  const handleOpenTree = (hadithId: string) => {
    router.push(`/tree/${hadithId}`);
  };

  const handleAutoTakhreej = async (hadithId: string) => {
    setIsAutoTakhreejLoading(hadithId);
    try {
      const related = await getRelatedHadiths(hadithId);
      const allIds = [hadithId, ...related.map((r) => r.id)];
      router.push(`/takhreej?ids=${allIds.join(",")}`);
    } catch (err) {
      console.error("Auto takhreej failed:", err);
      alert("تعذر جلب الأحاديث المتعلقة. تأكد من تشغيل الخادم.");
    } finally {
      setIsAutoTakhreejLoading(null);
    }
  };

  const handleViewComparativeTree = () => {
    if (selectedIds.size < 2) return;
    router.push(`/takhreej?ids=${Array.from(selectedIds).join(",")}`);
  };

  const isSearchActive = debouncedQuery.trim().length > 2;

  return (
    <div className="min-h-screen bg-slate-50/60 flex flex-col selection:bg-brand-teal/20 selection:text-brand-dark">
      {/* Top Application Navbar */}
      <header className="sticky top-0 z-30 bg-white/90 backdrop-blur-md border-b border-slate-200/80 shadow-2xs">
        <div className="max-w-5xl mx-auto px-4 sm:px-6 h-16 flex items-center justify-between gap-4">
          <div className="flex items-center gap-6">
            <Link
              href="/"
              className="flex items-center gap-2.5 font-bold text-slate-800 hover:text-brand-blue transition-colors group"
            >
              <div className="w-9 h-9 rounded-xl bg-brand-blue text-brand-teal flex items-center justify-center shadow-xs group-hover:scale-105 transition-transform">
                <Network className="w-5 h-5" />
              </div>
              <div className="flex flex-col">
                <span className="text-sm sm:text-base font-bold text-slate-900 leading-tight">
                  شجرة الأسانيد <span className="text-brand-teal">الذكية</span>
                </span>
                <span className="text-[10px] text-slate-400 font-normal">البحث في السنة النبوية</span>
              </div>
            </Link>

            <nav className="hidden md:flex items-center gap-1 text-xs font-semibold text-slate-600">
              <Link
                href="/"
                className="px-3 py-1.5 rounded-lg hover:bg-slate-100 transition-colors flex items-center gap-1.5"
              >
                <Home className="w-3.5 h-3.5 text-slate-400" />
                <span>الرئيسية</span>
              </Link>
              <Link
                href="/books"
                className="px-3 py-1.5 rounded-lg hover:bg-slate-100 transition-colors flex items-center gap-1.5"
              >
                <BookOpen className="w-3.5 h-3.5 text-slate-400" />
                <span>تصفح الكتب</span>
              </Link>
            </nav>
          </div>

          <div className="flex items-center gap-3">
            <button
              type="button"
              onClick={handleTakhreejModeToggle}
              className={`flex items-center gap-2 px-3.5 py-1.5 rounded-xl text-xs sm:text-sm font-bold transition-all cursor-pointer ${
                takhreejMode
                  ? "bg-brand-blue text-white shadow-md ring-2 ring-brand-blue/30"
                  : "bg-slate-100 hover:bg-slate-200 text-slate-700"
              }`}
              title="تفعيل وضع تحديد الأحاديث للمقارنة والتخريج"
            >
              <GitCompareArrows className="w-4 h-4" />
              <span>وضع التخريج</span>
              {takhreejMode && selectedIds.size > 0 && (
                <span className="w-5 h-5 rounded-full bg-brand-teal text-slate-900 text-xs flex items-center justify-center font-bold">
                  {selectedIds.size}
                </span>
              )}
            </button>
          </div>
        </div>
      </header>

      {/* Main Container */}
      <main className="flex-1 max-w-4xl w-full mx-auto p-4 sm:p-6 md:pt-10 pb-28">
        {/* Search Hero */}
        <section className="text-center mb-8 space-y-2.5">
          <h1 className="text-2xl sm:text-4xl font-extrabold text-brand-dark tracking-tight">
            ابحث في الأحاديث والأسانيد
          </h1>
          <p className="text-xs sm:text-sm text-slate-500 max-w-xl mx-auto font-arabic leading-relaxed">
            استكشف متون الأحاديث النبوية وتراجم الرواة مع عرض تفاعلي لشبكة الأسانيد وطرق التخريج.
          </p>
        </section>

        {/* Search Bar Input */}
        <div className="relative mb-6">
          <div className="relative flex items-center">
            {/* Search Icon */}
            <div className="absolute inset-y-0 start-0 flex items-center ps-4 pointer-events-none text-slate-400">
              <Search className="w-5 h-5" />
            </div>

            {/* Input Element */}
            <input
              ref={inputRef}
              type="text"
              dir="rtl"
              className="block w-full py-4 ps-12 pe-28 text-base md:text-lg text-slate-900 placeholder:text-slate-400 bg-white border-2 border-slate-200/90 rounded-2xl shadow-sm hover:border-slate-300 focus:border-brand-teal focus:ring-4 focus:ring-brand-teal/15 outline-none transition-all duration-200"
              placeholder="ابحث بمتن الحديث، اسم الراوي، أو المصدر..."
              value={query}
              onChange={(e) => setQuery(e.target.value)}
            />

            {/* Right-side Controls (Clear & Shortcut / Loading) */}
            <div className="absolute inset-y-0 end-0 flex items-center pe-3.5 gap-2">
              {isFetching && (
                <Loader2 className="w-5 h-5 text-brand-teal animate-spin" />
              )}

              {query.length > 0 && !isFetching && (
                <button
                  type="button"
                  onClick={() => {
                    setQuery("");
                    inputRef.current?.focus();
                  }}
                  className="p-1 rounded-full text-slate-400 hover:text-slate-600 hover:bg-slate-100 transition-colors cursor-pointer"
                  title="مسح البحث"
                  aria-label="مسح البحث"
                >
                  <X className="w-4 h-4" />
                </button>
              )}

              {query.length === 0 && (
                <kbd className="hidden sm:inline-flex items-center gap-1 text-[11px] font-semibold text-slate-400 bg-slate-100 border border-slate-200 px-2 py-1 rounded-md">
                  /
                </kbd>
              )}
            </div>
          </div>

          {/* Under-input helper if short query */}
          {query.trim().length > 0 && query.trim().length <= 2 && (
            <p className="text-xs text-amber-600 mt-2 pr-1 font-medium animate-in fade-in">
              * أدخل 3 أحرف على الأقل لبدء البحث...
            </p>
          )}
        </div>

        {/* Takhreej Mode Helper Banner */}
        {takhreejMode && (
          <div className="mb-6 p-4 rounded-2xl bg-blue-50/80 border border-blue-200/80 text-blue-900 flex items-center justify-between gap-4 flex-wrap animate-in fade-in slide-in-from-top-2">
            <div className="flex items-center gap-2.5">
              <div className="w-8 h-8 rounded-xl bg-brand-blue text-white flex items-center justify-center shrink-0">
                <GitCompareArrows className="w-4 h-4" />
              </div>
              <div className="text-xs sm:text-sm">
                <span className="font-bold block">وضع التخريج والمقارنة مفعّل</span>
                <span className="text-blue-700 text-xs">
                  حدد حديثين أو أكثر من القائمة لعرض شجرة أسانيدها المشتركة وتفردات الطرق.
                </span>
              </div>
            </div>

            <div className="flex items-center gap-2 mr-auto">
              {filteredResults.length > 0 && (
                <button
                  type="button"
                  onClick={selectAllFiltered}
                  className="text-xs font-bold text-brand-blue hover:underline px-2 py-1 cursor-pointer"
                >
                  تحديد نتائج الصفحة ({filteredResults.length})
                </button>
              )}
              {selectedIds.size > 0 && (
                <button
                  type="button"
                  onClick={clearSelection}
                  className="text-xs font-semibold text-slate-500 hover:text-slate-800 px-2 py-1 cursor-pointer"
                >
                  إلغاء التحديد
                </button>
              )}
            </div>
          </div>
        )}

        {/* Filter Pills & Result Summary */}
        {isSearchActive && !isLoading && results && results.length > 0 && (
          <div className="mb-5 space-y-3">
            <div className="flex items-center justify-between">
              <h2 className="text-sm font-bold text-slate-700 flex items-center gap-2">
                <span>نتائج البحث</span>
                <span className="px-2 py-0.5 rounded-full bg-slate-200/70 text-slate-700 text-xs font-bold">
                  {results.length}
                </span>
              </h2>

              {selectedBook && (
                <button
                  type="button"
                  onClick={() => setSelectedBook(null)}
                  className="text-xs text-slate-500 hover:text-brand-blue flex items-center gap-1 cursor-pointer"
                >
                  <FilterX className="w-3.5 h-3.5" />
                  <span>إلغاء التصفية</span>
                </button>
              )}
            </div>

            {/* Book Filter Pills */}
            <SearchBookFilters
              books={bookStats}
              selectedBook={selectedBook}
              onSelectBook={setSelectedBook}
              totalCount={results.length}
            />
          </div>
        )}

        {/* Results Area */}
        <div>
          {/* Initial / Suggested State */}
          {!isSearchActive && (
            <SearchEmptyState
              type="initial"
              onSelectSuggestion={(sug) => {
                setQuery(sug);
                inputRef.current?.focus();
              }}
            />
          )}

          {/* Loading Skeleton */}
          {isLoading && isSearchActive && <SearchSkeleton count={4} />}

          {/* Error State */}
          {isError && (
            <div className="bg-rose-50 border border-rose-200 rounded-2xl p-6 text-center space-y-3 my-6 max-w-lg mx-auto">
              <div className="w-12 h-12 bg-rose-100 rounded-full flex items-center justify-center mx-auto text-rose-600">
                <AlertCircle className="w-6 h-6" />
              </div>
              <h3 className="font-bold text-rose-900 text-base">حدث خطأ أثناء البحث</h3>
              <p className="text-xs text-rose-700 leading-relaxed">
                تعذر الاتصال بخادم البحث. يرجى التحقق من تشغيل الخادم الخلفي وإعادة المحاولة.
              </p>
              <button
                type="button"
                onClick={() => refetch()}
                className="inline-flex items-center gap-1.5 px-4 py-2 bg-rose-600 text-white text-xs font-bold rounded-xl hover:bg-rose-700 transition-colors cursor-pointer shadow-xs"
              >
                <RotateCcw className="w-3.5 h-3.5" />
                <span>إعادة المحاولة</span>
              </button>
            </div>
          )}

          {/* Zero Results State */}
          {!isLoading && !isError && isSearchActive && results && results.length === 0 && (
            <SearchEmptyState
              type="no_results"
              query={debouncedQuery}
              onResetSearch={() => {
                setQuery("");
                inputRef.current?.focus();
              }}
            />
          )}

          {/* Results List */}
          {!isLoading && !isError && isSearchActive && results && results.length > 0 && (
            <>
              {filteredResults.length === 0 ? (
                <div className="bg-white rounded-2xl border border-slate-200/80 p-8 text-center space-y-3 my-4">
                  <p className="text-slate-600 text-sm">
                    لا توجد أحاديث مطابقة في كتاب{" "}
                    <span className="font-bold text-slate-800">«{selectedBook}»</span>.
                  </p>
                  <button
                    type="button"
                    onClick={() => setSelectedBook(null)}
                    className="text-xs font-bold text-brand-blue hover:underline cursor-pointer"
                  >
                    عرض النتائج من كافة الكتب ({results.length})
                  </button>
                </div>
              ) : (
                <div className="space-y-4">
                  {filteredResults.map((hadith) => (
                    <HadithCard
                      key={hadith.id}
                      hadith={hadith}
                      searchQuery={debouncedQuery}
                      isSelected={selectedIds.has(hadith.id)}
                      takhreejMode={takhreejMode}
                      isAutoTakhreejLoading={isAutoTakhreejLoading === hadith.id}
                      onToggleSelect={toggleSelection}
                      onOpenTree={handleOpenTree}
                      onAutoTakhreej={handleAutoTakhreej}
                    />
                  ))}
                </div>
              )}
            </>
          )}
        </div>
      </main>

      {/* Floating Action Bar for Takhreej Mode */}
      {takhreejMode && (
        <TakhreejFloatingBar
          selectedCount={selectedIds.size}
          onViewTree={handleViewComparativeTree}
          onClearSelection={clearSelection}
          onExitTakhreejMode={handleTakhreejModeToggle}
        />
      )}
    </div>
  );
}

export default function SearchPage() {
  return (
    <Suspense
      fallback={
        <div className="min-h-screen flex items-center justify-center">
          <Loader2 className="w-8 h-8 text-brand-teal animate-spin" />
        </div>
      }
    >
      <SearchContent />
    </Suspense>
  );
}
