import { Handle, Position } from '@xyflow/react';
import { memo } from 'react';
import { AlertTriangle } from 'lucide-react';
import { useIlalStore } from '@/features/ilal/store/useIlalStore';

type ComparativeNarratorNodeData = {
  narratorName: string;
  fullName?: string;
  generationTier: string | null;
  transmissionTerm: string | null;
  gradeSummary?: string;
  gradeEn?: string;
  isAnomaly?: boolean;
  anomalyReason?: string;
  isSelected?: boolean;
  showWeakOnly?: boolean;
  sourceBooks: string[];
  isMudallis?: boolean;
  hasMukhtalit?: boolean;
};

const getBookBadge = (book: string) => {
  switch(book) {
    case 'صحيح البخاري': return { text: 'خ', color: '#2563eb' };
    case 'صحيح مسلم': return { text: 'م', color: '#16a34a' };
    case 'سنن أبي داود': return { text: 'د', color: '#d97706' };
    case 'جامع الترمذي': return { text: 'ت', color: '#9333ea' };
    case 'سنن النسائي': return { text: 'س', color: '#0284c7' };
    case 'سنن ابن ماجه': return { text: 'ق', color: '#e11d48' };
    case 'مسند أحمد': return { text: 'حم', color: '#b45309' };
    case 'موطأ مالك': return { text: 'ط', color: '#0d9488' };
    default: return { text: book.substring(0, 1), color: '#475569' };
  }
};

const ComparativeNarratorNode = ({ id, data, selected }: { id: string; data: ComparativeNarratorNodeData; selected?: boolean }) => {
  const isIlalHighlighted = useIlalStore((s) => s.highlightedNarratorIds.includes(id));
  let borderColor = '#cbd5e1'; // default slate-300
  let bgColor = '#ffffff';

  switch(data.gradeEn) {
    case 'reliable':
      borderColor = '#2ecc71';
      bgColor = '#2ecc7115';
      break;
    case 'mostly_reliable':
      borderColor = '#f39c12';
      bgColor = '#f39c1215';
      break;
    case 'weak':
      borderColor = '#e74c3c';
      bgColor = '#e74c3c15';
      break;
    case 'companion':
      borderColor = '#9b59b6';
      bgColor = '#9b59b615';
      break;
    case 'unknown':
      borderColor = '#95a5a6';
      bgColor = '#95a5a615';
      break;
    case 'abandoned':
      borderColor = '#c0392b';
      bgColor = '#c0392b15';
      break;
    case 'fabricator':
      borderColor = '#8b0000';
      bgColor = '#8b000015';
      break;
  }

  const isReliable = data.gradeEn === 'reliable' || data.gradeEn === 'mostly_reliable' || data.gradeEn === 'companion';
  const opacity = data.showWeakOnly && isReliable ? 0.3 : 1;
  const isMadar = data.sourceBooks && data.sourceBooks.length >= 2;

  let finalBorderClass = '';
  if (selected) {
    finalBorderClass = 'ring-2 ring-brand-blue border-brand-blue';
  } else if (isIlalHighlighted) {
    finalBorderClass = 'ring-4 ring-rose-400 ring-offset-2 scale-105';
  } else if (isMadar) {
    finalBorderClass = 'ring-2 ring-amber-400';
  }

  return (
    <div 
      dir="rtl" 
      className={`px-4 py-3 shadow-md rounded-lg border-2 min-w-[200px] max-w-[250px] text-center transition-all break-words relative ${finalBorderClass}`}
      style={{ backgroundColor: bgColor, borderColor: (selected || isMadar) ? undefined : borderColor, opacity }}
    >
      {data.isAnomaly && (
        <div 
          className="absolute -top-3 -right-3 bg-white rounded-full p-1 shadow border border-red-200" 
          title={data.anomalyReason}
        >
          <AlertTriangle className="w-5 h-5 text-red-500" />
        </div>
      )}

      {/* Top Handle - Input from Sheikh */}
      <Handle type="target" position={Position.Top} className="!w-2 !h-2 !bg-transparent !border-none opacity-0 pointer-events-none" />
      
      {data.sourceBooks && data.sourceBooks.length > 0 && (
        <div className="absolute -top-3 left-2 flex items-center gap-0.5 bg-white rounded-full px-1.5 py-0.5 shadow-xs border border-slate-200 z-10">
          {data.sourceBooks.map((book, idx) => {
            const badge = getBookBadge(book);
            return (
              <span 
                key={idx} 
                className="w-4.5 h-4.5 flex items-center justify-center text-[9px] text-white rounded-full font-bold shrink-0"
                style={{ backgroundColor: badge.color }}
                title={book}
              >
                {badge.text}
              </span>
            );
          })}
        </div>
      )}

      {data.transmissionTerm && (
        <div className="text-xs text-slate-500 mb-1 border-b pb-1 mt-2">
          {data.transmissionTerm}
        </div>
      )}
      
      <div className="font-bold text-slate-800 text-lg" title={data.fullName || data.narratorName}>
        {data.narratorName}
      </div>
      
      {data.generationTier && (
        <div className="text-sm text-slate-500 mt-1">
          {data.generationTier}
        </div>
      )}

      {(data.isMudallis || data.hasMukhtalit) && (
        <div className="flex gap-1 justify-center mt-2 flex-wrap">
          {data.isMudallis && (
            <span className="px-2 py-0.5 text-[10px] font-bold text-white bg-orange-500 rounded-full" title="موصوف بالتدليس">مدلس</span>
          )}
          {data.hasMukhtalit && (
            <span className="px-2 py-0.5 text-[10px] font-bold text-white bg-yellow-500 rounded-full" title="اختلط في آخر عمره">اختلط</span>
          )}
        </div>
      )}
      
      {/* Bottom Handle - Output to Student */}
      <Handle type="source" position={Position.Bottom} className="!w-2 !h-2 !bg-transparent !border-none opacity-0 pointer-events-none" />
    </div>
  );
};

export default memo(ComparativeNarratorNode);
