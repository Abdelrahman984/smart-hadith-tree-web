"use client";

import { useState } from "react";
import { Panel } from "@xyflow/react";
import {
  ChevronDown,
  ChevronUp,
  Layers,
  BookOpen,
  GitFork,
  UserCheck,
  AlertTriangle,
  FileDiff,
} from "lucide-react";

type LegendTab = "all" | "books" | "paths" | "narrators";

const BOOKS = [
  { code: "خ", name: "صحيح البخاري", color: "#2563eb" },
  { code: "م", name: "صحيح مسلم", color: "#16a34a" },
  { code: "د", name: "سنن أبي داود", color: "#d97706" },
  { code: "ت", name: "جامع الترمذي", color: "#9333ea" },
  { code: "س", name: "سنن النسائي", color: "#0284c7" },
  { code: "ق", name: "سنن ابن ماجه", color: "#e11d48" },
  { code: "حم", name: "مسند أحمد", color: "#b45309" },
  { code: "ط", name: "موطأ مالك", color: "#0d9488" },
];

const NARRATOR_RANKS = [
  { label: "صحابي", color: "#9b59b6" },
  { label: "ثقة / عدل ضابط", color: "#2ecc71" },
  { label: "صدوق / حسن", color: "#f39c12" },
  { label: "ضعيف", color: "#e74c3c" },
  { label: "متروك / كذاب", color: "#8b0000" },
  { label: "مجهول", color: "#95a5a6" },
];

export default function BookLegend() {
  const [isOpen, setIsOpen] = useState(true);
  const [activeTab, setActiveTab] = useState<LegendTab>("all");

  if (!isOpen) {
    return (
      <Panel position="top-right" className="m-2" dir="rtl">
        <button
          onClick={() => setIsOpen(true)}
          className="flex items-center gap-2 bg-white/95 backdrop-blur-md px-3.5 py-2 rounded-xl shadow-md border border-slate-200 text-slate-700 hover:bg-slate-50 hover:text-slate-900 transition-all text-xs font-bold cursor-pointer"
          title="فتح مفتاح الرموز"
        >
          <Layers className="w-4 h-4 text-brand-blue" />
          <span>مفتاح الرموز</span>
          <ChevronDown className="w-3.5 h-3.5 text-slate-400" />
        </button>
      </Panel>
    );
  }

  return (
    <Panel
      position="top-right"
      className="m-2 bg-white/95 backdrop-blur-md w-80 rounded-2xl shadow-xl border border-slate-200/90 text-xs transition-all overflow-hidden"
      dir="rtl"
    >
      {/* Header */}
      <div className="flex items-center justify-between px-3.5 py-2.5 bg-slate-50/80 border-b border-slate-200">
        <div className="flex items-center gap-2">
          <Layers className="w-4 h-4 text-brand-blue" />
          <span className="font-bold text-slate-800 text-sm">مفتاح الرموز والمصطلحات</span>
        </div>
        <button
          onClick={() => setIsOpen(false)}
          className="p-1 rounded-lg hover:bg-slate-200/70 text-slate-500 hover:text-slate-700 transition-colors cursor-pointer"
          title="تصغير المفتاح"
        >
          <ChevronUp className="w-4 h-4" />
        </button>
      </div>

      {/* Tabs */}
      <div className="flex border-b border-slate-100 bg-slate-50/40 p-1 gap-1">
        <button
          onClick={() => setActiveTab("all")}
          className={`flex-1 py-1 rounded-md text-[11px] font-semibold transition-all cursor-pointer ${
            activeTab === "all"
              ? "bg-white text-brand-blue shadow-sm border border-slate-200/60"
              : "text-slate-500 hover:text-slate-800"
          }`}
        >
          الكل
        </button>
        <button
          onClick={() => setActiveTab("books")}
          className={`flex-1 py-1 rounded-md text-[11px] font-semibold transition-all cursor-pointer ${
            activeTab === "books"
              ? "bg-white text-brand-blue shadow-sm border border-slate-200/60"
              : "text-slate-500 hover:text-slate-800"
          }`}
        >
          الكتب
        </button>
        <button
          onClick={() => setActiveTab("paths")}
          className={`flex-1 py-1 rounded-md text-[11px] font-semibold transition-all cursor-pointer ${
            activeTab === "paths"
              ? "bg-white text-brand-blue shadow-sm border border-slate-200/60"
              : "text-slate-500 hover:text-slate-800"
          }`}
        >
          المسارات
        </button>
        <button
          onClick={() => setActiveTab("narrators")}
          className={`flex-1 py-1 rounded-md text-[11px] font-semibold transition-all cursor-pointer ${
            activeTab === "narrators"
              ? "bg-white text-brand-blue shadow-sm border border-slate-200/60"
              : "text-slate-500 hover:text-slate-800"
          }`}
        >
          الرواة
        </button>
      </div>

      {/* Body Content */}
      <div className="p-3.5 space-y-4 max-h-[72vh] overflow-y-auto">
        {/* Section 1: Books */}
        {(activeTab === "all" || activeTab === "books") && (
          <div>
            <div className="flex items-center gap-1.5 text-slate-700 font-bold mb-2 text-xs">
              <BookOpen className="w-3.5 h-3.5 text-slate-500" />
              <span>رموز كتب الحديث</span>
            </div>
            <div className="grid grid-cols-2 gap-x-2 gap-y-1.5">
              {BOOKS.map((book) => (
                <div key={book.code} className="flex items-center gap-2 py-0.5">
                  <span
                    className="w-5 h-5 flex items-center justify-center text-[10px] text-white rounded-full font-bold shadow-xs shrink-0"
                    style={{ backgroundColor: book.color }}
                  >
                    {book.code}
                  </span>
                  <span className="text-slate-600 text-[11px] truncate" title={book.name}>
                    {book.name}
                  </span>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Section 2: Paths & Matn Variations */}
        {(activeTab === "all" || activeTab === "paths") && (
          <div className={activeTab === "all" ? "border-t border-slate-100 pt-3" : ""}>
            <div className="flex items-center gap-1.5 text-slate-700 font-bold mb-2 text-xs">
              <GitFork className="w-3.5 h-3.5 text-slate-500" />
              <span>مسارات الأسانيد والمتون</span>
            </div>
            <div className="space-y-2">
              <div className="flex items-center gap-2.5">
                <div className="w-6 h-0.5 bg-blue-600 rounded shrink-0"></div>
                <div>
                  <span className="text-slate-700 font-medium block leading-tight">مسار كتاب منفرد</span>
                  <span className="text-slate-400 text-[10px]">مصبوغ بلون المصدر</span>
                </div>
              </div>

              <div className="flex items-center gap-2.5">
                <div className="w-6 h-1 bg-slate-600 rounded shrink-0"></div>
                <div>
                  <span className="text-slate-700 font-medium block leading-tight">مسار مشترك (جامع)</span>
                  <span className="text-slate-400 text-[10px]">تلتقي فيه أسانيد عدة كتب</span>
                </div>
              </div>

              <div className="flex items-center gap-2.5">
                <div className="w-6 h-0 border-t-2 border-dashed border-red-500 shrink-0"></div>
                <div>
                  <span className="text-red-700 font-medium flex items-center gap-1 leading-tight">
                    <span>انقطاع في السند</span>
                    <AlertTriangle className="w-3 h-3 text-red-500" />
                  </span>
                  <span className="text-slate-400 text-[10px]">سقط تاريخي أو عدم ثبوت معاصرة</span>
                </div>
              </div>

              <div className="flex items-center gap-2.5">
                <div className="w-6 h-0 border-t-2 border-dashed border-amber-500 shrink-0"></div>
                <div>
                  <span className="text-amber-700 font-medium flex items-center gap-1 leading-tight">
                    <span>اختلاف باللفظ</span>
                    <FileDiff className="w-3 h-3 text-amber-500" />
                  </span>
                  <span className="text-slate-400 text-[10px]">تباين في المتن بين الروايات</span>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Section 3: Narrators & Madar */}
        {(activeTab === "all" || activeTab === "narrators") && (
          <div className={activeTab === "all" ? "border-t border-slate-100 pt-3" : ""}>
            <div className="flex items-center gap-1.5 text-slate-700 font-bold mb-2 text-xs">
              <UserCheck className="w-3.5 h-3.5 text-slate-500" />
              <span>الرواة ومدار الإسناد</span>
            </div>

            {/* Madar & Inqita' Indicators */}
            <div className="space-y-1.5 mb-3 bg-slate-50/60 p-2 rounded-lg border border-slate-100">
              <div className="flex items-center gap-2">
                <div className="w-3.5 h-3.5 rounded-full border-2 border-amber-400 ring-2 ring-amber-400/50 shrink-0"></div>
                <div>
                  <span className="text-slate-700 font-medium text-[11px]">مدار الإسناد: </span>
                  <span className="text-slate-500 text-[10px]">الراوي الذي تلتقي وتتفرع عنده الطرق</span>
                </div>
              </div>
              <div className="flex items-center gap-2">
                <AlertTriangle className="w-3.5 h-3.5 text-red-500 shrink-0" />
                <div>
                  <span className="text-slate-700 font-medium text-[11px]">علة السماع: </span>
                  <span className="text-slate-500 text-[10px]">تنبيه عند وجود انقطاع أو تدليس</span>
                </div>
              </div>
            </div>

            {/* Ranks Grid */}
            <span className="text-slate-500 text-[10px] block mb-1.5 font-semibold">ألوان رتب الجرح والتعديل:</span>
            <div className="grid grid-cols-2 gap-x-2 gap-y-1">
              {NARRATOR_RANKS.map((rank) => (
                <div key={rank.label} className="flex items-center gap-1.5">
                  <span
                    className="w-2.5 h-2.5 rounded-full shrink-0 shadow-xs"
                    style={{ backgroundColor: rank.color }}
                  ></span>
                  <span className="text-slate-600 text-[11px] truncate">{rank.label}</span>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </Panel>
  );
}

