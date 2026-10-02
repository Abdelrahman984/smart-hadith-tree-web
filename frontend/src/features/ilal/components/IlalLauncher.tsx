"use client";

import { useEffect, useState } from "react";
import { ShieldQuestion, X, Loader2 } from "lucide-react";
import { useIlalForHadith } from "../hooks/useIlal";
import { useIlalStore } from "../store/useIlalStore";
import IlalPanel from "./IlalPanel";

/**
 * "فحص العلل" button for the single-hadith tree page. Gathers the hadith's turuq automatically,
 * analyzes them, and shows the report in a side panel while decorating the canvas.
 */
export default function IlalLauncher({ hadithId }: { hadithId: string }) {
  const [isOpen, setIsOpen] = useState(false);
  const [requested, setRequested] = useState(false);
  const { data, isLoading, isError } = useIlalForHadith(hadithId, requested);
  const { setReport, reset } = useIlalStore();

  useEffect(() => {
    if (data) setReport(data);
  }, [data, setReport]);

  useEffect(() => reset, [reset]);

  const open = () => {
    setRequested(true);
    setIsOpen(true);
  };

  return (
    <>
      <button
        onClick={open}
        className="flex items-center gap-2 rounded-lg border border-brand-blue/30 bg-white px-3 py-2 text-sm font-semibold text-brand-blue shadow-sm transition-colors hover:bg-brand-blue hover:text-white cursor-pointer"
      >
        <ShieldQuestion className="h-4 w-4" />
        فحص العلل
        {data && data.findings.length > 0 && (
          <span className={`rounded-full px-1.5 text-[11px] text-white ${data.hasQadihah ? "bg-red-500" : "bg-amber-500"}`}>
            {data.findings.length}
          </span>
        )}
      </button>

      {isOpen && (
        <div
          className="fixed top-0 bottom-0 end-0 z-40 flex w-[26rem] max-w-full flex-col border-s border-slate-200 bg-white shadow-2xl"
          dir="rtl"
        >
          <div className="flex items-center justify-between border-b border-slate-200 p-4">
            <h2 className="text-lg font-bold text-slate-800">علل الحديث</h2>
            <button
              onClick={() => setIsOpen(false)}
              className="rounded-full p-1 text-slate-500 hover:bg-slate-100 cursor-pointer"
              aria-label="إغلاق"
            >
              <X size={20} />
            </button>
          </div>
          <div className="flex-1 overflow-y-auto p-4">
            {isLoading && (
              <div className="flex items-center justify-center gap-2 py-10 text-slate-500">
                <Loader2 className="h-5 w-5 animate-spin" /> جاري جمع الطرق وفحصها...
              </div>
            )}
            {isError && <p className="text-sm text-red-600">تعذّر فحص العلل لهذا الحديث.</p>}
            {data && <IlalPanel report={data} />}
          </div>
        </div>
      )}
    </>
  );
}
