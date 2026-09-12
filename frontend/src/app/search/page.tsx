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
  Network,
  BookOpen,
  Home,
  AlertCircle,
  RotateCcw,
  FilterX,
  CheckSquare,
  SlidersHorizontal,
} from "lucide-react";

function SearchContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const initialQueryFromUrl = searchParams.get("q") || "";
  const initialScopeFromUrl = parseInt(searchParams.get("scope") || "0", 10);
  const initialMatchFromUrl = parseInt(searchParams.get("match") || "0", 10);

  const {
    query,
    setQuery,
    scope,
    setScope,
    match,
    setMatch,
    debouncedQuery,
    data: results,
    isLoading,
    isFetching,
    isError,
    refetch,
  } = useHadithSearch(initialQueryFromUrl, initialScopeFromUrl, initialMatchFromUrl);

  const inputRef = useRef<HTMLInputElement>(null);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [isAutoTakhreejLoading, setIsAutoTakhreejLoading] = useState<string | null>(null);
  const [selectedBook, setSelectedBook] = useState<string | null>(null);
  const [showAdvancedOptions, setShowAdvancedOptions] = useState(false);

  // Sync URL when debounced query, scope, or match updates
  useEffect(() => {
    if (typeof window === "undefined") return;
    const url = new URL(window.location.href);
    if (debouncedQuery.trim().length > 2) {
      url.searchParams.set("q", debouncedQuery.trim());
      url.searchParams.set("scope", scope.toString());
      url.searchParams.set("match", match.toString());
    } else {
      url.searchParams.delete("q");
      url.searchParams.delete("scope");
      url.searchParams.delete("match");
    }
    window.history.replaceState(null, "", url.toString());
  }, [debouncedQuery, scope, match]);

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

  // Takhreej selection handlers
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

  const handleOpenSingleTree = (hadithId: string) => {
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

  const handleViewTree = () => {
    const ids = Array.from(selectedIds);
    if (ids.length === 1) {
      router.push(`/tree/${ids[0]}`);
    } else if (ids.length >= 2) {
      router.push(`/takhreej?ids=${ids.join(",")}`);
    }
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
                <span className="text-[10px] text-slate-400 font-normal">منصة التخريج ودراسة الأسانيد</span>
              </div>
            </Link>

            <nav className="hidden sm:flex items-center gap-1 text-xs font-semibold text-slate-600">
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

          {selectedIds.size > 0 && (
            <div className="flex items-center gap-2 text-xs font-bold bg-brand-blue/10 text-brand-blue px-3 py-1.5 rounded-xl animate-in fade-in">
              <CheckSquare className="w-4 h-4" />
              <span>{selectedIds.size} محدد للتخريج</span>
            </div>
          )}
        </div>
      </header>

      {/* Main Container */}
      <main className="flex-1 max-w-4xl w-full mx-auto p-4 sm:p-6 md:pt-10 pb-28">
        {/* Search Hero */}
        <section className="text-center mb-8 space-y-2.5">
          <h1 className="text-2xl sm:text-4xl font-extrabold text-brand-dark tracking-tight">
            ابحث وخرّج الأحاديث النبوية
          </h1>
          <p className="text-xs sm:text-sm text-slate-500 max-w-xl mx-auto font-arabic leading-relaxed">
            ابحث في المتون والرواة، وحدد الروايات من مختلف كتب السنة لرسم شجرة التخريج المقارنة وبيان مدار الإسناد.
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

              <button
                type="button"
                onClick={() => setShowAdvancedOptions(!showAdvancedOptions)}
                className={`p-1.5 rounded-xl transition-colors cursor-pointer border ${
                  showAdvancedOptions || scope !== 0 || match !== 0
                    ? "bg-brand-blue/10 text-brand-blue border-brand-blue/20"
                    : "text-slate-400 hover:text-slate-600 hover:bg-slate-100 border-transparent"
                }`}
                title="خيارات البحث المتقدم"
              >
                <SlidersHorizontal className="w-4 h-4" />
              </button>

              {query.length === 0 && (
                <kbd className="hidden sm:inline-flex items-center gap-1 text-[11px] font-semibold text-slate-400 bg-slate-100 border border-slate-200 px-2 py-1 rounded-md">
                  /
                </kbd>
              )}
            </div>
          </div>

          {/* Advanced Search Options Panel */}
          {showAdvancedOptions && (
            <div className="absolute top-full mt-2 w-full sm:w-auto sm:min-w-[400px] sm:end-0 z-10 bg-white border border-slate-200 rounded-2xl shadow-xl overflow-hidden animate-in fade-in slide-in-from-top-2">
              <div className="p-4 space-y-4">
                {/* Search Scope */}
                <div className="space-y-2">
                  <label className="text-xs font-bold text-slate-700 block">نطاق البحث</label>
                  <div className="flex bg-slate-100 p-1 rounded-xl">
                    {["الكل", "المتن", "السند"].map((label, index) => (
                      <button
                        key={index}
                        onClick={() => setScope(index)}
                        className={`flex-1 text-xs py-1.5 px-3 rounded-lg font-medium transition-all ${
                          scope === index
                            ? "bg-white text-brand-blue shadow-sm"
                            : "text-slate-500 hover:text-slate-700"
                        }`}
                      >
                        {label}
                      </button>
                    ))}
                  </div>
                </div>

                {/* Match Type */}
                <div className="space-y-2">
                  <label className="text-xs font-bold text-slate-700 block">طريقة المطابقة</label>
                  <div className="flex bg-slate-100 p-1 rounded-xl">
                    {[
                      { label: "جميع الكلمات", value: 0 },
                      { label: "أي كلمة", value: 1 },
                      { label: "تطابق تام", value: 2 },
                    ].map((opt) => (
                      <button
                        key={opt.value}
                        onClick={() => setMatch(opt.value)}
                        className={`flex-1 text-xs py-1.5 px-3 rounded-lg font-medium transition-all ${
                          match === opt.value
                            ? "bg-white text-brand-blue shadow-sm"
                            : "text-slate-500 hover:text-slate-700"
                        }`}
                      >
                        {opt.label}
                      </button>
                    ))}
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* Under-input helper if short query */}
          {query.trim().length > 0 && query.trim().length <= 2 && (
            <p className="text-xs text-amber-600 mt-2 pr-1 font-medium animate-in fade-in">
              * أدخل 3 أحرف على الأقل لبدء البحث...
            </p>
          )}
        </div>

        {/* Filter Pills & Result Summary */}
        {isSearchActive && !isLoading && results && results.length > 0 && (
          <div className="mb-5 space-y-3">
            <div className="flex items-center justify-between flex-wrap gap-2">
              <div className="flex items-center gap-3">
                <h2 className="text-sm font-bold text-slate-700 flex items-center gap-2">
                  <span>نتائج البحث</span>
                  <span className="px-2 py-0.5 rounded-full bg-slate-200/70 text-slate-700 text-xs font-bold">
                    {results.length}
                  </span>
                </h2>

                <div className="flex items-center gap-2 text-xs border-r border-slate-200 pr-3">
                  <button
                    type="button"
                    onClick={selectAllFiltered}
                    className="text-brand-blue hover:underline font-semibold cursor-pointer"
                  >
                    تحديد المعروض ({filteredResults.length})
                  </button>
                  {selectedIds.size > 0 && (
                    <>
                      <span className="text-slate-300">•</span>
                      <button
                        type="button"
                        onClick={clearSelection}
                        className="text-slate-500 hover:text-slate-800 font-semibold cursor-pointer"
                      >
                        إلغاء التحديد ({selectedIds.size})
                      </button>
                    </>
                  )}
                </div>
              </div>

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
                      isAutoTakhreejLoading={isAutoTakhreejLoading === hadith.id}
                      onToggleSelect={toggleSelection}
                      onOpenSingleTree={handleOpenSingleTree}
                      onAutoTakhreej={handleAutoTakhreej}
                    />
                  ))}
                </div>
              )}
            </>
          )}
        </div>
      </main>

      {/* Floating Action Bar for Takhreej */}
      <TakhreejFloatingBar
        selectedCount={selectedIds.size}
        onViewTree={handleViewTree}
        onClearSelection={clearSelection}
      />
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
