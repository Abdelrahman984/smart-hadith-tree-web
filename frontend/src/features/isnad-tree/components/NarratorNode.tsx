import { Handle, Position } from '@xyflow/react';
import { memo } from 'react';
import { AlertTriangle } from 'lucide-react';

type NarratorNodeData = {
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
};

const NarratorNode = ({ data, selected }: { data: NarratorNodeData; selected?: boolean }) => {
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

  return (
    <div 
      dir="rtl" 
      className={`px-4 py-3 shadow-md rounded-lg border-2 min-w-[200px] max-w-[250px] text-center transition-all break-words relative ${selected ? 'ring-2 ring-brand-blue border-brand-blue' : ''}`}
      style={{ backgroundColor: bgColor, borderColor: selected ? undefined : borderColor, opacity }}
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
      
      {data.transmissionTerm && (
        <div className="text-xs text-slate-500 mb-1 border-b pb-1">
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
      
      {/* Bottom Handle - Output to Student */}
      <Handle type="source" position={Position.Bottom} className="!w-2 !h-2 !bg-transparent !border-none opacity-0 pointer-events-none" />
    </div>
  );
};

export default memo(NarratorNode);
