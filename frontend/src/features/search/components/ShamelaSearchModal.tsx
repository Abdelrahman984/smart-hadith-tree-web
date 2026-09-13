"use client";

import { useState } from "react";
import { SearchRequestDto, SearchLogicalOperator, SearchScope } from "@/types/api";
import {
  X,
  Plus,
  Trash2,
  Sliders,
  RotateCcw,
  Search,
} from "lucide-react";

interface ShamelaSearchModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSearch: (request: SearchRequestDto) => void;
  initialRequest?: SearchRequestDto;
  initialQuery?: string;
}

export default function ShamelaSearchModal({
  isOpen,
  onClose,
  onSearch,
  initialRequest,
  initialQuery,
}: ShamelaSearchModalProps) {
  const [operator, setOperator] = useState<SearchLogicalOperator>(
    initialRequest?.operator ?? 0 // 0 = AND, 1 = OR
  );
  const [phrases, setPhrases] = useState<string[]>(() => {
    if (initialRequest?.phrases && initialRequest.phrases.length > 0) {
      return initialRequest.phrases;
    }
    if (initialQuery && initialQuery.trim().length > 0) {
      return [initialQuery.trim(), ""];
    }
    return ["", ""];
  });
  const [excludeText, setExcludeText] = useState<string>(
    initialRequest?.excludePhrases?.join(", ") ?? ""
  );
  const [isOrdered, setIsOrdered] = useState<boolean>(
    initialRequest?.isOrdered ?? false
  );
  const [isProximity, setIsProximity] = useState<boolean>(
    initialRequest?.isProximity ?? false
  );
  const [proximityWords, setProximityWords] = useState<number>(
    initialRequest?.proximityWords ?? 15
  );
  const [scope, setScope] = useState<SearchScope>(
    initialRequest?.scope ?? 1 // Default to Matn in Shamela style
  );

  if (!isOpen) return null;

  const handleAddPhrase = () => {
    if (phrases.length < 6) {
      setPhrases([...phrases, ""]);
    }
  };

  const handleRemovePhrase = (index: number) => {
    if (phrases.length > 1) {
      setPhrases(phrases.filter((_, i) => i !== index));
    }
  };

  const handlePhraseChange = (index: number, val: string) => {
    const updated = [...phrases];
    updated[index] = val;
    setPhrases(updated);
  };

  const handleReset = () => {
    setOperator(0);
    setPhrases(["", ""]);
    setExcludeText("");
    setIsOrdered(false);
    setIsProximity(false);
    setProximityWords(15);
    setScope(1);
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const cleanPhrases = phrases.map((p) => p.trim()).filter((p) => p.length > 0);
    if (cleanPhrases.length === 0) return;

    const cleanExclude = excludeText
      .split(/[,،]+/)
      .map((t) => t.trim())
      .filter((t) => t.length > 0);

    const request: SearchRequestDto = {
      phrases: cleanPhrases,
      operator,
      excludePhrases: cleanExclude,
      isOrdered,
      isProximity,
      proximityWords,
      scope,
    };

    onSearch(request);
    onClose();
  };

  const arabicNumbers = ["١", "٢", "٣", "٤", "٥", "٦"];

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in duration-200">
      <div
        dir="rtl"
        className="relative w-full max-w-2xl bg-white rounded-3xl shadow-2xl border border-slate-200 overflow-hidden flex flex-col max-h-[90vh] animate-in zoom-in-95 duration-200"
      >
        {/* Modal Header */}
        <div className="px-6 py-5 bg-gradient-to-r from-slate-900 via-brand-blue to-slate-900 text-white flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-2xl bg-white/10 flex items-center justify-center border border-white/10 text-brand-teal">
              <Sliders className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-lg font-bold flex items-center gap-2">
                <span>البحث المتقدم</span>
                <span className="text-xs px-2 py-0.5 rounded-full bg-brand-teal/20 text-brand-teal border border-brand-teal/30 font-normal">
                  نمط المكتبة الشاملة
                </span>
              </h2>
              <p className="text-xs text-slate-300">
                عبارات متعددة، روابط منطقية، استبعاد نصوص، وضبط ترتيب وتقارب الكلمات
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="p-2 rounded-xl text-slate-300 hover:text-white hover:bg-white/10 transition-colors cursor-pointer"
            aria-label="إغلاق"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Modal Form Content */}
        <form onSubmit={handleSubmit} className="flex-1 overflow-y-auto p-6 space-y-6">
          {/* Logical Operator Tabs ([و] / [أو]) */}
          <div className="space-y-2">
            <label className="text-xs font-bold text-slate-700 block">
              العلاقة المنطقية بين العبارات:
            </label>
            <div className="grid grid-cols-2 gap-2 bg-slate-100 p-1.5 rounded-2xl border border-slate-200/80">
              <button
                type="button"
                onClick={() => setOperator(0)}
                className={`flex items-center justify-center gap-2 py-2.5 px-4 rounded-xl text-xs sm:text-sm font-bold transition-all cursor-pointer ${
                  operator === 0
                    ? "bg-white text-brand-blue shadow-sm border border-slate-200"
                    : "text-slate-500 hover:text-slate-800"
                }`}
              >
                <span className="text-base font-black">[ و ]</span>
                <span>يلزم وجود كل العبارات (AND)</span>
              </button>

              <button
                type="button"
                onClick={() => setOperator(1)}
                className={`flex items-center justify-center gap-2 py-2.5 px-4 rounded-xl text-xs sm:text-sm font-bold transition-all cursor-pointer ${
                  operator === 1
                    ? "bg-white text-brand-blue shadow-sm border border-slate-200"
                    : "text-slate-500 hover:text-slate-800"
                }`}
              >
                <span className="text-base font-black">[ أو ]</span>
                <span>يكفي وجود أي من العبارات (OR)</span>
              </button>
            </div>
          </div>

          {/* Multi-Phrase Numbered Inputs (١، ٢، ٣...) */}
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <label className="text-xs font-bold text-slate-700 block">
                عبارات البحث المستقلة:
              </label>
              <span className="text-[11px] text-slate-400">
                كل سطر يُعامل كعبارة مستقلة
              </span>
            </div>

            <div className="space-y-2.5">
              {phrases.map((phrase, idx) => (
                <div key={idx} className="flex items-center gap-2">
                  <span className="w-7 h-9 rounded-xl bg-slate-100 border border-slate-200 text-slate-600 font-bold text-sm flex items-center justify-center shrink-0">
                    {arabicNumbers[idx] || idx + 1}
                  </span>
                  <input
                    type="text"
                    dir="rtl"
                    value={phrase}
                    onChange={(e) => handlePhraseChange(idx, e.target.value)}
                    placeholder={
                      idx === 0
                        ? "العبارة الأولى (مثال: نهى رسول الله)..."
                        : idx === 1
                        ? "العبارة الثانية (مثال: عن بيع)..."
                        : `العبارة ${idx + 1}...`
                    }
                    className="flex-1 py-2.5 px-3.5 text-sm bg-slate-50/60 border border-slate-200 rounded-xl focus:bg-white focus:border-brand-teal focus:ring-3 focus:ring-brand-teal/15 outline-none transition-all"
                  />
                  {phrases.length > 1 && (
                    <button
                      type="button"
                      onClick={() => handleRemovePhrase(idx)}
                      className="p-2 text-slate-400 hover:text-rose-600 hover:bg-rose-50 rounded-xl transition-colors cursor-pointer"
                      title="حذف هذا السطر"
                    >
                      <Trash2 className="w-4 h-4" />
                    </button>
                  )}
                </div>
              ))}
            </div>

            {phrases.length < 6 && (
              <button
                type="button"
                onClick={handleAddPhrase}
                className="inline-flex items-center gap-1.5 text-xs font-bold text-brand-blue hover:text-brand-dark transition-colors cursor-pointer pt-1"
              >
                <Plus className="w-4 h-4" />
                <span>إضافة عبارة بحث أخرى ({arabicNumbers[phrases.length] || phrases.length + 1})</span>
              </button>
            )}
          </div>

          {/* Modifiers (مرتبة / متقاربة) */}
          <div className="p-4 bg-slate-50 border border-slate-200/80 rounded-2xl space-y-3">
            <h3 className="text-xs font-bold text-slate-700">خيارات الترتيب والسياق:</h3>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 text-xs">
              <label className="flex items-start gap-2.5 cursor-pointer select-none">
                <input
                  type="checkbox"
                  checked={isOrdered}
                  onChange={(e) => setIsOrdered(e.target.checked)}
                  className="mt-0.5 w-4 h-4 text-brand-blue rounded-md border-slate-300 focus:ring-brand-teal"
                />
                <div>
                  <span className="font-bold text-slate-800 block">مرتبة (In-Order)</span>
                  <span className="text-[11px] text-slate-500">
                    تشترط ورود العبارة (٢) بعد (١) في سياق الحديث
                  </span>
                </div>
              </label>

              <label className="flex items-start gap-2.5 cursor-pointer select-none">
                <input
                  type="checkbox"
                  checked={isProximity}
                  onChange={(e) => setIsProximity(e.target.checked)}
                  className="mt-0.5 w-4 h-4 text-brand-blue rounded-md border-slate-300 focus:ring-brand-teal"
                />
                <div>
                  <span className="font-bold text-slate-800 block">متقاربة (Proximity)</span>
                  <span className="text-[11px] text-slate-500">
                    تشترط ورود العبارات في نفس الجملة/السياق
                  </span>
                </div>
              </label>
            </div>

            {isProximity && (
              <div className="pt-2 border-t border-slate-200/60 flex items-center gap-3 animate-in fade-in">
                <span className="text-xs text-slate-600">أقصى مسافة فاصلة بين العبارات:</span>
                <input
                  type="number"
                  min={3}
                  max={50}
                  value={proximityWords}
                  onChange={(e) => setProximityWords(Number(e.target.value))}
                  className="w-16 py-1 px-2 text-xs text-center font-bold border border-slate-300 rounded-lg bg-white"
                />
                <span className="text-xs text-slate-500">كلمة</span>
              </div>
            )}
          </div>

          {/* Exclude Field ([ليس] / NOT) */}
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <label className="text-xs font-bold text-slate-700 flex items-center gap-1.5">
                <span className="text-rose-600 font-black">[ ليس ]</span>
                <span>استبعاد نصوص وكلمات (NOT):</span>
              </label>
              <span className="text-[11px] text-slate-400">فصل الكلمات بفاصلة</span>
            </div>
            <input
              type="text"
              dir="rtl"
              value={excludeText}
              onChange={(e) => setExcludeText(e.target.value)}
              placeholder="مثال: رمضان (لاستبعاد صيام الفريضة والبحث عن صيام التطوع فقط)..."
              className="w-full py-2.5 px-3.5 text-sm bg-rose-50/20 border border-slate-200 rounded-xl focus:bg-white focus:border-rose-400 focus:ring-3 focus:ring-rose-400/15 outline-none transition-all placeholder:text-slate-400"
            />
          </div>

          {/* Search Scope */}
          <div className="space-y-2">
            <label className="text-xs font-bold text-slate-700 block">مجال البحث:</label>
            <div className="flex bg-slate-100 p-1 rounded-xl border border-slate-200/60 text-xs">
              {[
                { label: "في المتن فقط (موصى به)", val: 1 },
                { label: "في السند والرواة", val: 2 },
                { label: "بحث شامل (الكل)", val: 0 },
              ].map((item) => (
                <button
                  key={item.val}
                  type="button"
                  onClick={() => setScope(item.val as SearchScope)}
                  className={`flex-1 py-1.5 px-2 rounded-lg font-medium transition-all cursor-pointer ${
                    scope === item.val
                      ? "bg-white text-brand-blue shadow-xs font-bold"
                      : "text-slate-500 hover:text-slate-800"
                  }`}
                >
                  {item.label}
                </button>
              ))}
            </div>
          </div>
        </form>

        {/* Modal Footer Controls */}
        <div className="p-4 px-6 bg-slate-50 border-t border-slate-200 flex items-center justify-between gap-3">
          <button
            type="button"
            onClick={handleReset}
            className="inline-flex items-center gap-1.5 px-3.5 py-2 text-xs font-semibold text-slate-600 hover:text-slate-900 hover:bg-slate-200/70 rounded-xl transition-colors cursor-pointer"
          >
            <RotateCcw className="w-3.5 h-3.5" />
            <span>إعادة تعيين</span>
          </button>

          <div className="flex items-center gap-2.5">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-xs font-bold text-slate-600 hover:text-slate-800 rounded-xl transition-colors cursor-pointer"
            >
              إلغاء
            </button>
            <button
              type="button"
              onClick={handleSubmit}
              className="inline-flex items-center gap-2 px-5 py-2.5 bg-brand-blue text-white hover:bg-slate-900 text-xs sm:text-sm font-bold rounded-xl shadow-sm hover:shadow-md transition-all cursor-pointer"
            >
              <Search className="w-4 h-4 text-brand-teal" />
              <span>تنفيذ البحث المتقدم</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
