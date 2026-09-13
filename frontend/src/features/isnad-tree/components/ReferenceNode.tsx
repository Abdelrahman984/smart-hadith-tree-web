import { Handle, Position } from "@xyflow/react";
import { memo } from "react";
import { BookOpen, Bookmark } from "lucide-react";

export type ReferenceNodeData = {
  famousName: string;
  fullName?: string;
  twoPartName?: string;
  bookName: string;
  hadithNumber?: number | string;
  generationTier?: string | null;
  gradeSummary?: string;
  gradeEn?: string;
  sourceBooks?: string[];
  isSelected?: boolean;
};

const getBookColor = (book?: string) => {
  if (!book) return { border: "#2563eb", bg: "#eff6ff", badgeBg: "#dbeafe", text: "#1d4ed8" };
  if (book.includes("البخاري")) return { border: "#2563eb", bg: "#eff6ff", badgeBg: "#dbeafe", text: "#1d4ed8" };
  if (book.includes("مسلم")) return { border: "#16a34a", bg: "#f0fdf4", badgeBg: "#dcfce7", text: "#15803d" };
  if (book.includes("أبي داود") || book.includes("أبو داود")) return { border: "#d97706", bg: "#fffbeb", badgeBg: "#fef3c7", text: "#b45309" };
  if (book.includes("الترمذي")) return { border: "#9333ea", bg: "#faf5ff", badgeBg: "#f3e8ff", text: "#7e22ce" };
  if (book.includes("النسائي")) return { border: "#0284c7", bg: "#f0f9ff", badgeBg: "#e0f2fe", text: "#0369a1" };
  if (book.includes("ابن ماجه")) return { border: "#e11d48", bg: "#fff1f2", badgeBg: "#ffe4e6", text: "#be123c" };
  if (book.includes("أحمد")) return { border: "#b45309", bg: "#fffbeb", badgeBg: "#fef3c7", text: "#92400e" };
  if (book.includes("مالك")) return { border: "#0d9488", bg: "#f0fdfa", badgeBg: "#ccfbf1", text: "#0f766e" };
  return { border: "#2563eb", bg: "#eff6ff", badgeBg: "#dbeafe", text: "#1d4ed8" };
};

const ReferenceNode = ({ data, selected }: { data: ReferenceNodeData; selected?: boolean }) => {
  const theme = getBookColor(data.bookName);

  return (
    <div
      dir="rtl"
      className={`relative px-4 py-3.5 shadow-lg rounded-xl border-2 min-w-[240px] max-w-[280px] text-center transition-all duration-200 cursor-pointer ${
        selected ? "ring-4 ring-offset-2 ring-blue-400 border-blue-600 scale-[1.02]" : "hover:shadow-xl hover:scale-[1.01]"
      }`}
      style={{
        backgroundColor: theme.bg,
        borderColor: selected ? undefined : theme.border,
      }}
    >
      {/* Top Handle - Input from Sheikh (Invisible anchor so arrowhead touches card border) */}
      <Handle
        type="target"
        position={Position.Top}
        className="!w-2 !h-2 !bg-transparent !border-none opacity-0 pointer-events-none"
      />

      {/* Top Header Tag */}
      <div className="flex items-center justify-between gap-1 pb-2 mb-2 border-b border-slate-200/80">
        <div className="flex items-center gap-1.5 text-xs font-semibold" style={{ color: theme.text }}>
          <BookOpen className="w-3.5 h-3.5" />
          <span>المصدر والمُخَرِّج</span>
        </div>
        {data.hadithNumber !== undefined && data.hadithNumber !== null && (
          <span
            className="px-2 py-0.5 rounded-full text-[11px] font-bold shadow-xs flex items-center gap-0.5"
            style={{ backgroundColor: theme.badgeBg, color: theme.text }}
          >
            <Bookmark className="w-3 h-3 inline" />
            <span>رقم {data.hadithNumber}</span>
          </span>
        )}
      </div>

      {/* Book Name */}
      <div className="text-xs font-medium text-slate-600 mb-1">
        {data.bookName}
      </div>

      {/* Famous Reference Owner Name (e.g. البخاري) */}
      <div
        className="font-extrabold text-slate-900 text-xl tracking-wide font-arabic my-0.5"
        title={data.fullName || data.famousName}
      >
        {data.famousName}
      </div>

      {/* Historical Two-Part Name Subtitle (e.g. محمد بن إسماعيل) */}
      <div className="text-xs text-slate-500 font-arabic truncate" title={data.fullName}>
        ({data.twoPartName || data.fullName || data.famousName})
      </div>

      {data.generationTier && (
        <div className="text-[11px] text-slate-400 mt-1.5 pt-1 border-t border-slate-200/50">
          {data.generationTier}
        </div>
      )}

      {/* Notice: No bottom handle because reference node is the terminal compiler */}
    </div>
  );
};

export default memo(ReferenceNode);
