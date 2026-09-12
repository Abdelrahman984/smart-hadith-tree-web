import { useState, useMemo } from "react";
import { HadithSearchResultDto } from "@/types/api";
import { getBookMeta } from "@/lib/bookTheme";
import {
  Book,
  GitCompareArrows,
  CheckSquare,
  Square,
  Loader2,
  Copy,
  Check,
  Network,
  ChevronDown,
  ChevronUp,
} from "lucide-react";

interface HadithCardProps {
  hadith: HadithSearchResultDto;
  searchQuery: string;
  isSelected: boolean;
  isAutoTakhreejLoading: boolean;
  onToggleSelect: (id: string) => void;
  onOpenSingleTree: (id: string) => void;
  onAutoTakhreej: (id: string) => void;
}

/**
 * Escapes regex special characters
 */
function escapeRegExp(string: string) {
  return string.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

/**
 * Highlights matching query terms in text
 */
function HighlightedText({ text, query }: { text: string; query: string }) {
  const parts = useMemo(() => {
    const trimmed = query.trim();
    if (!trimmed || trimmed.length < 2) {
      return [{ text, match: false }];
    }

    const keywords = trimmed
      .split(/\s+/)
      .filter((w) => w.length > 1)
      .map(escapeRegExp);

    if (keywords.length === 0) {
      return [{ text, match: false }];
    }

    const regex = new RegExp(`(${keywords.join("|")})`, "gi");
    const tokens = text.split(regex);

    return tokens.map((token) => ({
      text: token,
      match: keywords.some((kw) => new RegExp(`^${kw}$`, "i").test(token)),
    }));
  }, [text, query]);

  return (
    <>
      {parts.map((part, index) =>
        part.match ? (
          <mark
            key={index}
            className="bg-amber-100 text-amber-900 rounded-xs px-0.5 py-0.5 font-medium"
          >
            {part.text}
          </mark>
        ) : (
          <span key={index}>{part.text}</span>
        )
      )}
    </>
  );
}

export default function HadithCard({
  hadith,
  searchQuery,
  isSelected,
  isAutoTakhreejLoading,
  onToggleSelect,
  onOpenSingleTree,
  onAutoTakhreej,
}: HadithCardProps) {
  const [copied, setCopied] = useState(false);
  const [isExpanded, setIsExpanded] = useState(false);

  const fullText = hadith.matnArabic || hadith.matnSnippet || "";
  const isLongText = fullText.length > 280;
  const bookMeta = getBookMeta(hadith.bookName);

  const handleCopy = (e: React.MouseEvent) => {
    e.stopPropagation();
    navigator.clipboard.writeText(fullText);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleCardClick = () => {
    onToggleSelect(hadith.id);
  };

  return (
    <article
      onClick={handleCardClick}
      className={`group relative bg-white rounded-2xl p-5 border transition-all duration-200 cursor-pointer select-none ${
        isSelected
          ? "border-brand-blue ring-2 ring-brand-blue/30 bg-blue-50/20 shadow-md"
          : "border-slate-200/80 hover:border-slate-300 hover:shadow-md hover:-translate-y-0.5"
      }`}
      style={{
        borderRightWidth: "4px",
        borderRightColor: isSelected ? "#1A3A5C" : bookMeta.color,
      }}
    >
      {/* Top Meta Bar */}
      <div className="flex items-start justify-between gap-3 pb-3 border-b border-slate-100/90">
        <div className="flex items-center gap-2.5 flex-wrap">
          {/* Always-active Takhreej Selection Checkbox */}
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              onToggleSelect(hadith.id);
            }}
            className="text-brand-blue hover:scale-110 transition-transform cursor-pointer p-0.5"
            aria-label={isSelected ? "إلغاء التحديد" : "تحديد الحديث للتخريج"}
            title={isSelected ? "إلغاء التحديد" : "تحديد الحديث للمقارنة والتخريج"}
          >
            {isSelected ? (
              <CheckSquare className="w-5 h-5 text-brand-blue fill-brand-blue/15" />
            ) : (
              <Square className="w-5 h-5 text-slate-300 hover:text-slate-500 transition-colors" />
            )}
          </button>

          {/* Book Badge */}
          <span
            className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-xs font-bold border transition-colors ${bookMeta.badgeClass}`}
          >
            <Book className="w-3.5 h-3.5" />
            <span>{hadith.bookName}</span>
          </span>

          {/* Hadith Number */}
          <span className="text-xs font-semibold text-slate-500 bg-slate-100 px-2 py-1 rounded-lg">
            حديث #{hadith.hadithNumber}
          </span>

          {/* Chapter */}
          {hadith.chapter && (
            <span
              className="text-xs text-slate-600 bg-slate-50 border border-slate-200/70 px-2.5 py-1 rounded-lg font-medium truncate max-w-xs"
              title={hadith.chapter}
            >
              {hadith.chapter}
            </span>
          )}
        </div>

        {/* Quick Copy Action */}
        <div className="flex items-center gap-1.5 shrink-0">
          <button
            type="button"
            onClick={handleCopy}
            className={`flex items-center gap-1 text-xs px-2.5 py-1 rounded-lg transition-colors cursor-pointer border ${
              copied
                ? "bg-emerald-50 text-emerald-700 border-emerald-200"
                : "bg-slate-50 hover:bg-slate-100 text-slate-600 border-slate-200/80"
            }`}
            title="نسخ نص الحديث كاملاً"
          >
            {copied ? (
              <>
                <Check className="w-3.5 h-3.5 text-emerald-600" />
                <span className="font-semibold">تم النسخ</span>
              </>
            ) : (
              <>
                <Copy className="w-3.5 h-3.5 text-slate-500" />
                <span>نسخ</span>
              </>
            )}
          </button>
        </div>
      </div>

      {/* Matn Content */}
      <div className="pt-3.5">
        <p
          className={`text-slate-800 text-base md:text-lg leading-loose font-arabic text-justify select-text whitespace-pre-wrap ${
            !isExpanded && isLongText ? "line-clamp-4" : ""
          }`}
        >
          <HighlightedText text={fullText} query={searchQuery} />
        </p>

        {isLongText && (
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              setIsExpanded(!isExpanded);
            }}
            className="mt-2 inline-flex items-center gap-1 text-xs font-bold text-brand-blue hover:text-blue-800 transition-colors cursor-pointer"
          >
            {isExpanded ? (
              <>
                <span>عرض أقل</span>
                <ChevronUp className="w-3.5 h-3.5" />
              </>
            ) : (
              <>
                <span>عرض المتن كاملاً...</span>
                <ChevronDown className="w-3.5 h-3.5" />
              </>
            )}
          </button>
        )}
      </div>

      {/* Footer Actions */}
      <div className="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between gap-2 flex-wrap">
        <div className="text-xs">
          {isSelected ? (
            <span className="font-bold text-brand-blue flex items-center gap-1">
              <span>✓ محدد للتخريج المقارن</span>
            </span>
          ) : (
            <span className="text-slate-400">انقر على البطاقة لتحديد الحديث للمقارنة</span>
          )}
        </div>

        <div className="flex items-center gap-2 mr-auto">
          {/* Subtle Single Chain button for inspection */}
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              onOpenSingleTree(hadith.id);
            }}
            className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold text-slate-600 hover:text-slate-900 hover:bg-slate-100 rounded-xl transition-colors cursor-pointer"
            title="معاينة إسناد هذا الكتاب منفرداً"
          >
            <Network className="w-3.5 h-3.5 text-slate-400" />
            <span>سند منفرد</span>
          </button>

          {/* Primary Auto Takhreej Button */}
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              onAutoTakhreej(hadith.id);
            }}
            disabled={isAutoTakhreejLoading}
            className="inline-flex items-center gap-1.5 px-3.5 py-1.5 text-xs font-bold bg-brand-blue text-white hover:bg-slate-900 rounded-xl transition-all shadow-xs hover:shadow-sm cursor-pointer"
            title="البحث التلقائي عن شواهد ومتون الروايات الأخرى ورسم شجرة التخريج الموحدة"
          >
            {isAutoTakhreejLoading ? (
              <Loader2 className="w-3.5 h-3.5 animate-spin text-brand-teal" />
            ) : (
              <GitCompareArrows className="w-3.5 h-3.5 text-brand-teal" />
            )}
            <span>تخريج فوري</span>
          </button>
        </div>
      </div>
    </article>
  );
}
