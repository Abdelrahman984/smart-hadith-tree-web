"use client";

import { useEffect, useRef, useState } from "react";
import { useDialogFocus } from "@/components/useDialogFocus";
import { isPhone } from "@/lib/viewport";
import { useNarratorDrawerStore } from "@/features/narrator-details/store/useNarratorDrawerStore";
import { ShieldQuestion, X, Loader2 } from "lucide-react";
import { useIlalForHadith } from "../hooks/useIlal";
import { useIlalStore } from "../store/useIlalStore";
import IlalPanel from "./IlalPanel";
import { countVisibleFindings } from "../utils/ilalLabels";

/**
 * "فحص العلل" button for the single-hadith tree page. Gathers the hadith's turuq automatically,
 * analyzes them, and shows the report in a side panel while decorating the canvas.
 */
export default function IlalLauncher({ hadithId }: { hadithId: string }) {
  const [isOpen, setIsOpen] = useState(false);
  const [requested, setRequested] = useState(false);
  const { data, isLoading, isError } = useIlalForHadith(hadithId, requested);
  const { setReport, reset } = useIlalStore();
  const panelRef = useRef<HTMLDivElement>(null);
  const close = () => setIsOpen(false);
  useDialogFocus(panelRef, isOpen, close);

  // Only one side panel at a time: opening the narrator drawer closes this one, and the reverse.
  const isNarratorOpen = useNarratorDrawerStore((s) => s.isOpen);
  const closeNarrator = useNarratorDrawerStore((s) => s.closeDrawer);
  const [prevNarratorOpen, setPrevNarratorOpen] = useState(isNarratorOpen);
  if (isNarratorOpen !== prevNarratorOpen) {
    setPrevNarratorOpen(isNarratorOpen);
    if (isNarratorOpen) setIsOpen(false);
  }

  useEffect(() => {
    if (data) setReport(data);
  }, [data, setReport]);

  useEffect(() => reset, [reset]);

  // On a phone the panel covers the graph: picking a finding closes it, so the narrators it names can be seen.
  useEffect(
    () =>
      useIlalStore.subscribe((state, prev) => {
        if (state.activeFindingIndex !== null && state.activeFindingIndex !== prev.activeFindingIndex && isPhone()) {
          setIsOpen(false);
        }
      }),
    []
  );

  const open = () => {
    setRequested(true);
    closeNarrator();
    setIsOpen(true);
  };

  return (
    <>
      <button
        type="button"
        onClick={open}
        aria-haspopup="dialog"
        className="flex items-center gap-2 rounded-lg border border-brand-blue/30 bg-surface px-3 py-2 text-sm font-semibold text-brand-blue shadow-sm transition-colors hover:bg-brand-blue hover:text-white cursor-pointer"
      >
        <ShieldQuestion className="h-4 w-4" />
        فحص العلل
        {data && countVisibleFindings(data.findings) > 0 && (
          <span className={`rounded-full px-1.5 text-[11px] text-white ${data.hasQadihah ? "bg-red-500" : "bg-amber-500"}`}>
            {countVisibleFindings(data.findings)}
          </span>
        )}
      </button>

      {isOpen && (
        <div className="fixed inset-0 z-40 bg-slate-900/20" onClick={close} aria-hidden />
      )}
      {isOpen && (
        <div
          ref={panelRef}
          tabIndex={-1}
          role="dialog"
          aria-modal="true"
          aria-labelledby="ilal-panel-title"
          className="fixed top-0 bottom-0 end-0 z-50 flex w-full sm:w-[26rem] flex-col border-s border-line bg-surface shadow-2xl outline-none animate-in slide-in-from-left duration-200"
          dir="rtl"
        >
          <div className="flex items-center justify-between border-b border-line p-4">
            <h2 id="ilal-panel-title" className="text-lg font-bold text-ink">علل الحديث</h2>
            <button
              onClick={close}
              className="rounded-full p-1 text-ink-subtle hover:bg-slate-100 cursor-pointer"
              aria-label="إغلاق لوحة العلل"
            >
              <X size={20} />
            </button>
          </div>
          <div className="flex-1 overflow-y-auto p-4">
            {isLoading && (
              <div className="flex items-center justify-center gap-2 py-10 text-ink-subtle">
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
