import { Handle, Position } from '@xyflow/react';
import { memo } from 'react';

type NarratorNodeData = {
  narratorName: string;
  generationTier: string | null;
  transmissionTerm: string | null;
  gradeSummary?: string;
  isSelected?: boolean;
};

const NarratorNode = ({ data, selected }: { data: NarratorNodeData; selected?: boolean }) => {
  // Determine border color based on simplified grade (in a real app, map to strict colors)
  let borderClass = 'border-slate-300';
  let bgClass = 'bg-white';
  
  if (data.gradeSummary === 'ثقة' || data.gradeSummary === 'صحابي' || data.gradeSummary === 'صحابية') {
    borderClass = 'border-brand-teal';
    bgClass = 'bg-brand-teal/5';
  } else if (data.gradeSummary === 'ضعيف' || data.gradeSummary === 'متروك') {
    borderClass = 'border-red-500';
    bgClass = 'bg-red-50';
  } else if (data.gradeSummary === 'صدوق' || data.gradeSummary?.includes('حسن')) {
    borderClass = 'border-blue-400';
    bgClass = 'bg-blue-50';
  } else if (data.gradeSummary === 'مجهول' || !data.gradeSummary) {
    borderClass = 'border-orange-400';
    bgClass = 'bg-orange-50';
  }

  return (
    <div 
      dir="rtl" 
      className={`px-4 py-3 shadow-md rounded-lg border-2 min-w-[200px] max-w-[250px] text-center transition-all break-words ${bgClass} ${selected ? 'ring-2 ring-brand-blue border-brand-blue' : borderClass}`}
    >
      {/* Top Handle - Input from Sheikh */}
      <Handle type="target" position={Position.Top} className="w-3 h-3 bg-slate-400" />
      
      {data.transmissionTerm && (
        <div className="text-xs text-slate-500 mb-1 border-b pb-1">
          {data.transmissionTerm}
        </div>
      )}
      
      <div className="font-bold text-slate-800 text-lg">
        {data.narratorName}
      </div>
      
      {data.generationTier && (
        <div className="text-sm text-slate-500 mt-1">
          {data.generationTier}
        </div>
      )}
      
      {/* Bottom Handle - Output to Student */}
      <Handle type="source" position={Position.Bottom} className="w-3 h-3 bg-slate-400" />
    </div>
  );
};

export default memo(NarratorNode);
