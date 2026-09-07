"use client";

import { Panel } from '@xyflow/react';

export default function BookLegend() {
  return (
    <Panel position="top-right" className="bg-white/90 p-3 rounded-lg shadow-md border border-slate-200 text-sm" dir="rtl">
      <div className="font-bold text-slate-700 mb-2 border-b pb-1">مفتاح الرموز</div>
      
      <div className="space-y-2 mb-3">
        <div className="flex items-center gap-2">
          <span className="w-5 h-5 flex items-center justify-center text-xs text-white rounded-full font-bold bg-[#2563eb]">خ</span>
          <span className="text-slate-600 text-xs">صحيح البخاري</span>
        </div>
        <div className="flex items-center gap-2">
          <span className="w-5 h-5 flex items-center justify-center text-xs text-white rounded-full font-bold bg-[#16a34a]">م</span>
          <span className="text-slate-600 text-xs">صحيح مسلم</span>
        </div>
        <div className="flex items-center gap-2">
          <span className="w-5 h-5 flex items-center justify-center text-xs text-white rounded-full font-bold bg-[#d97706]">د</span>
          <span className="text-slate-600 text-xs">سنن أبي داود</span>
        </div>
        <div className="flex items-center gap-2">
          <span className="w-5 h-5 flex items-center justify-center text-xs text-white rounded-full font-bold bg-[#9333ea]">ت</span>
          <span className="text-slate-600 text-xs">جامع الترمذي</span>
        </div>
      </div>

      <div className="border-t pt-2 space-y-2">
        <div className="flex items-center gap-2">
          <div className="w-4 h-4 rounded-full border-2 border-amber-400 ring-2 ring-amber-400/50"></div>
          <span className="text-slate-600 text-xs">مدار (رواه عن أكثر من طريق)</span>
        </div>
        <div className="flex items-center gap-2">
          <div className="w-5 h-0 border-t-2 border-dashed border-red-500"></div>
          <span className="text-slate-600 text-xs">انقطاع في السند</span>
        </div>
      </div>
    </Panel>
  );
}
