import { Handle, Position } from "@xyflow/react";
import { memo } from "react";
import { BookOpen, Bookmark } from "lucide-react";
import { getBookMeta } from "@/lib/bookTheme";

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

const ReferenceNode = ({ data, selected }: { data: ReferenceNodeData; selected?: boolean }) => {
  const meta = getBookMeta(data.bookName);
  const booksList = data.sourceBooks && data.sourceBooks.length > 0 ? data.sourceBooks : [data.bookName];

  return (
    <div
      dir="rtl"
      className={`relative px-4 py-3.5 shadow-lg rounded-xl border-2 min-w-[240px] max-w-[280px] text-center transition-all duration-200 cursor-pointer ${
        selected ? "ring-4 ring-offset-2 ring-blue-400 border-blue-600 scale-[1.02]" : "hover:shadow-xl hover:scale-[1.01]"
      }`}
      style={{
        backgroundColor: `${meta.color}12`,
        borderColor: selected ? undefined : meta.color,
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
        <div className="flex items-center gap-1.5 text-xs font-semibold" style={{ color: meta.color }}>
          <BookOpen className="w-3.5 h-3.5" />
          <span>المصدر والمُخَرِّج</span>
        </div>
        <div className="flex items-center gap-1">
          {booksList.map((book, idx) => {
            const bMeta = getBookMeta(book);
            return (
              <span
                key={idx}
                className="min-w-[18px] h-[18px] px-1 flex items-center justify-center text-[9px] text-white rounded-full font-bold shadow-2xs shrink-0"
                style={{ backgroundColor: bMeta.color }}
                title={bMeta.name}
              >
                {bMeta.code}
              </span>
            );
          })}
          {data.hadithNumber !== undefined && data.hadithNumber !== null && (
            <span
              className="px-2 py-0.5 rounded-full text-[11px] font-bold shadow-xs flex items-center gap-0.5"
              style={{ backgroundColor: `${meta.color}20`, color: meta.color }}
            >
              <Bookmark className="w-3 h-3 inline" />
              <span>رقم {data.hadithNumber}</span>
            </span>
          )}
        </div>
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

      {/* Bottom Handle - Output to Student when a compiler is also an intermediate sheikh in a later collection */}
      <Handle
        type="source"
        position={Position.Bottom}
        className="!w-2 !h-2 !bg-transparent !border-none opacity-0 pointer-events-none"
      />
    </div>
  );
};

export default memo(ReferenceNode);

