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
  Sliders,
} from "lucide-react";
import ShamelaSearchModal from "@/features/search/components/ShamelaSearchModal";

function SearchContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const initialQueryFromUrl = searchParams.get("q") || "";
  const initialScopeFromUrl = parseInt(searchParams.get("scope") || "0", 10);
  const initialMatchFromUrl = parseInt(searchParams.get("match") || "0", 10);

  const {
    query,
    setQuery,
    debouncedQuery,
    advancedRequest,
    setAdvancedRequest,
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
  const [isShamelaModalOpen, setIsShamelaModalOpen] = useState(false);

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

  const isSearchActive = !!advancedRequest || debouncedQuery.trim().length > 2;

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
                onClick={() => setIsShamelaModalOpen(true)}
                className={`p-2 rounded-xl transition-all cursor-pointer border ${
                  advancedRequest
                    ? "bg-brand-blue text-brand-teal border-brand-blue/30 shadow-xs"
                    : "text-slate-400 hover:text-brand-blue hover:bg-slate-100 border-transparent"
                }`}
                title="خيارات البحث المتقدم (نمط المكتبة الشاملة)"
                aria-label="خيارات البحث المتقدم"
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

          {/* Under-input helper if short query */}
          {query.trim().length > 0 && query.trim().length <= 2 && (
            <p className="text-xs text-amber-600 mt-2 pr-1 font-medium animate-in fade-in">
              * أدخل 3 أحرف على الأقل لبدء البحث...
            </p>
          )}
        </div>

        {/* Active Shamela Advanced Query Banner */}
        {advancedRequest && (
          <div className="mb-5 p-3.5 bg-brand-blue/5 border border-brand-blue/20 rounded-2xl flex items-center justify-between gap-3 flex-wrap animate-in fade-in">
            <div className="flex items-center gap-2 flex-wrap text-xs">
              <span className="font-bold text-brand-blue flex items-center gap-1.5">
                <Sliders className="w-4 h-4 text-brand-teal" />
                <span>شروط الشاملة النشطة:</span>
              </span>

              {/* AND Phrases Badges */}
              {advancedRequest.andPhrases && advancedRequest.andPhrases.length > 0 && (
                <div className="inline-flex items-center gap-1.5 bg-brand-blue/10 border border-brand-blue/25 text-brand-blue px-2 py-0.5 rounded-lg">
                  <span className="font-black">[و]:</span>
                  {advancedRequest.andPhrases.map((p, i) => (
                    <span key={i} className="bg-white text-slate-800 px-1.5 py-0.5 rounded font-semibold text-[11px] shadow-2xs">
                      «{p}»
                    </span>
                  ))}
                </div>
              )}

              {/* OR Phrases Badges */}
              {advancedRequest.orPhrases && advancedRequest.orPhrases.length > 0 && (
                <div className="inline-flex items-center gap-1.5 bg-amber-500/10 border border-amber-500/25 text-amber-800 px-2 py-0.5 rounded-lg">
                  <span className="font-black">[أو]:</span>
                  {advancedRequest.orPhrases.map((p, i) => (
                    <span key={i} className="bg-white text-slate-800 px-1.5 py-0.5 rounded font-semibold text-[11px] shadow-2xs">
                      «{p}»
                    </span>
                  ))}
                </div>
              )}

              {/* Fallback to legacy phrases if andPhrases/orPhrases empty */}
              {(!advancedRequest.andPhrases || advancedRequest.andPhrases.length === 0) &&
                (!advancedRequest.orPhrases || advancedRequest.orPhrases.length === 0) &&
                advancedRequest.phrases?.map((p, i) => (
                  <span key={i} className="bg-white border border-slate-200 text-slate-800 px-2 py-0.5 rounded-md font-semibold shadow-2xs">
                    «{p}»
                  </span>
                ))}

              {/* EXCLUDE Badges */}
              {advancedRequest.excludePhrases && advancedRequest.excludePhrases.length > 0 && (
                <div className="inline-flex items-center gap-1.5 bg-rose-500/10 border border-rose-500/25 text-rose-700 px-2 py-0.5 rounded-lg">
                  <span className="font-black">[ليس]:</span>
                  {advancedRequest.excludePhrases.map((p, i) => (
                    <span key={i} className="bg-white text-rose-800 px-1.5 py-0.5 rounded font-semibold text-[11px] shadow-2xs">
                      «{p}»
                    </span>
                  ))}
                </div>
              )}

              {advancedRequest.isOrdered && (
                <span className="bg-amber-50 border border-amber-200 text-amber-800 px-2 py-0.5 rounded-md font-semibold">
                  مرتبة
                </span>
              )}
              {advancedRequest.isProximity && (
                <span className="bg-emerald-50 border border-emerald-200 text-emerald-800 px-2 py-0.5 rounded-md font-semibold">
                  متقاربة ({advancedRequest.proximityWords || 15} كلمة)
                </span>
              )}
            </div>

            <div className="flex items-center gap-2 mr-auto">
              <button
                type="button"
                onClick={() => setIsShamelaModalOpen(true)}
                className="text-xs font-bold text-brand-blue hover:underline cursor-pointer"
              >
                تعديل الشروط
              </button>
              <span className="text-slate-300">•</span>
              <button
                type="button"
                onClick={() => setAdvancedRequest(null)}
                className="text-xs font-bold text-rose-600 hover:text-rose-800 hover:underline cursor-pointer"
              >
                إلغاء
              </button>
            </div>
          </div>
        )}

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
                      highlightPhrases={advancedRequest?.phrases}
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

      {/* Shamela Advanced Search Modal */}
      {isShamelaModalOpen && (
        <ShamelaSearchModal
          isOpen={isShamelaModalOpen}
          onClose={() => setIsShamelaModalOpen(false)}
          onSearch={(req) => {
            setQuery("");
            setAdvancedRequest(req);
          }}
          initialRequest={advancedRequest ?? undefined}
          initialQuery={query}
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
