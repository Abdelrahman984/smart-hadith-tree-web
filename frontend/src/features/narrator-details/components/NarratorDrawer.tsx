"use client";

import { useQuery } from '@tanstack/react-query';
import { getNarratorDetails } from '@/lib/api';
import { useNarratorDrawerStore } from '../store/useNarratorDrawerStore';
import { X, Sparkles, AlertCircle } from 'lucide-react';
import { useState } from 'react';

const VERDICT_AR: Record<string, string> = {
  reliable: "ثقة",
  mostly_reliable: "صدوق",
  weak: "ضعيف",
  companion: "صحابي",
  unknown: "مجهول",
  abandoned: "متروك",
  fabricator: "كذاب",
};

const SCHOLAR_AR: Record<string, string> = {
  jarh: "ابن أبي حاتم (الجرح والتعديل)",
  thiqat: "ابن حبان (الثقات)",
  mughni_ducafa: "الذهبي (المغني في الضعفاء)",
  diwan_ducafa: "الذهبي (ديوان الضعفاء)",
  kashif: "الذهبي (الكاشف)",
  tahdhib_tahdhib: "ابن حجر (تهذيب التهذيب)",
  mizan: "الذهبي (ميزان الاعتدال)",
  tahdhib_kamal: "المزي (تهذيب الكمال)",
  taqrib: "ابن حجر (تقريب التهذيب)",
  kamil: "ابن عدي (الكامل في الضعفاء)",
  tabaqat: "ابن سعد (الطبقات الكبرى)",
  siyar: "الذهبي (سير أعلام النبلاء)",
  tarikh: "البخاري (التاريخ الكبير)",
  tarikh_islam: "الذهبي (تاريخ الإسلام)",
  durar_kamina: "ابن حجر (الدرر الكامنة)",
  isaba: "ابن حجر (الإصابة)",
  lisan_mizan: "ابن حجر (لسان الميزان)",
  tadhkirat_huffaz: "الذهبي (تذكرة الحفاظ)",
};

const getVerdictAr = (en: string) => VERDICT_AR[en.toLowerCase()] || en;
const getScholarAr = (en: string) => SCHOLAR_AR[en.toLowerCase()] || en;

interface ExtractedAiEvaluation {
  verbatimQuote: string;
  sourceBook: string;
  tier: string;
  justification: string;
}

export default function NarratorDrawer() {
  const { isOpen, selectedNarratorId, closeDrawer } = useNarratorDrawerStore();
  const [isAiLoading, setIsAiLoading] = useState(false);
  const [aiSummary, setAiSummary] = useState<ExtractedAiEvaluation | null>(null);
  const [aiError, setAiError] = useState<string | null>(null);

  const { data: narrator, isLoading, isError } = useQuery({
    queryKey: ['narrator', selectedNarratorId],
    queryFn: () => getNarratorDetails(selectedNarratorId!),
    enabled: !!selectedNarratorId && isOpen,
  });

  const handleGenerateSummary = async () => {
    if (!selectedNarratorId) return;
    setIsAiLoading(true);
    setAiError(null);
    try {
      const res = await fetch(`http://localhost:5147/api/Narrators/${selectedNarratorId}/ai-summary`);
      if (!res.ok) throw new Error("فشل استخراج البيانات. تأكد من إعداد مفتاح OpenAI.");
      const data = await res.json();
      setAiSummary(data);
    } catch (err) {
      if (err instanceof Error) {
        setAiError(err.message);
      } else {
        setAiError("حدث خطأ غير متوقع.");
      }
    } finally {
      setIsAiLoading(false);
    }
  };

  // Reset states when drawer closes
  const handleClose = () => {
    setAiSummary(null);
    setAiError(null);
    closeDrawer();
  };

  if (!isOpen) return null;

  return (
    <>
      {/* Backdrop */}
      <div 
        className="absolute inset-0 bg-slate-900/20 z-40 transition-opacity"
        onClick={handleClose}
      />
      
      {/* Drawer Panel */}
      <div 
        className="absolute top-0 bottom-0 start-0 w-96 bg-white shadow-2xl z-50 flex flex-col transform transition-transform duration-300"
        dir="rtl"
      >
        <div className="flex items-center justify-between p-4 border-b border-slate-200">
          <h2 className="text-xl font-bold text-slate-800">تفاصيل الراوي</h2>
          <button 
            onClick={handleClose}
            className="p-1 rounded-full hover:bg-slate-100 text-slate-500"
          >
            <X size={20} />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-4 space-y-6">
          {isLoading ? (
            <div className="animate-pulse space-y-4">
              <div className="h-6 bg-slate-200 rounded w-3/4"></div>
              <div className="h-4 bg-slate-200 rounded w-1/2"></div>
              <div className="h-20 bg-slate-200 rounded"></div>
            </div>
          ) : isError || !narrator ? (
            <div className="text-red-500">حدث خطأ أثناء تحميل بيانات الراوي.</div>
          ) : (
            <>
              {/* Header Info */}
              <div>
                <h3 className="text-2xl font-bold text-brand-dark mb-1">
                  {narrator.knownAs || narrator.fullName}
                </h3>
                {narrator.knownAs && (
                  <p className="text-sm text-slate-500 mb-2">{narrator.fullName}</p>
                )}
                
                <div className="flex gap-2 flex-wrap mt-3">
                  {narrator.generationTier && (
                    <span className="px-2 py-1 bg-brand-blue/10 text-brand-blue rounded text-xs font-semibold">
                      {narrator.generationTier}
                    </span>
                  )}
                  {narrator.gradeEn && (
                    <span className="px-2 py-1 bg-green-100 text-green-700 rounded text-xs font-semibold">
                      {getVerdictAr(narrator.gradeEn)}
                    </span>
                  )}
                  {narrator.gawamiRank && (
                    <span className="px-2 py-1 bg-indigo-50 text-indigo-700 border border-indigo-200 rounded text-xs font-semibold">
                      الرتبة: {narrator.gawamiRank}
                    </span>
                  )}
                  {narrator.birthYearHijri && (
                    <span className="px-2 py-1 bg-slate-100 text-slate-600 rounded text-xs">
                      مواليد: {narrator.birthYearHijri} هـ
                    </span>
                  )}
                  {narrator.deathYearHijri && (
                    <span className="px-2 py-1 bg-slate-100 text-slate-600 rounded text-xs">
                      وفيات: {narrator.deathYearHijri} هـ
                    </span>
                  )}
                  {narrator.isMudallis && (
                    <span className="px-2 py-1 bg-orange-100 text-orange-800 border border-orange-300 rounded text-xs font-bold">
                      موصوف بالتدليس
                    </span>
                  )}
                  {narrator.hasMukhtalit && (
                    <span className="px-2 py-1 bg-yellow-100 text-yellow-800 border border-yellow-300 rounded text-xs font-bold">
                      اختلط بأخرة
                    </span>
                  )}
                </div>
              </div>

              {/* Geography & Schools of Hadith */}
              {(narrator.residencePlaces || narrator.deathPlace) && (
                <div className="bg-slate-50 rounded-xl p-3.5 border border-slate-200 space-y-2">
                  <h4 className="text-sm font-bold text-slate-800">🌍 البلدان والرحلة العلمية</h4>
                  {narrator.residencePlaces && (
                    <div>
                      <span className="text-xs text-slate-500 block mb-1">بلدان الإقامة والرحلة:</span>
                      <div className="flex flex-wrap gap-1.5">
                        {narrator.residencePlaces.split(/[،,-]/).map((city, idx) => {
                          const trimmed = city.trim();
                          if (!trimmed) return null;
                          return (
                            <span
                              key={idx}
                              className="px-2 py-0.5 bg-white border border-slate-200 rounded-full text-xs text-slate-700 font-medium shadow-2xs"
                            >
                              📍 {trimmed}
                            </span>
                          );
                        })}
                      </div>
                    </div>
                  )}
                  {narrator.deathPlace && (
                    <div className="text-xs text-slate-600 pt-1 border-t border-slate-200/70">
                      <span className="text-slate-500">بلد الوفاة: </span>
                      <span className="font-semibold text-slate-800">{narrator.deathPlace}</span>
                    </div>
                  )}
                </div>
              )}

              {/* Narration Volume & Tafarrud Indicator */}
              {(narrator.uniqueHadithCount != null || narrator.totalNarrationsCount != null) && (
                <div className="bg-blue-50/60 rounded-xl p-3.5 border border-blue-100 space-y-2">
                  <div className="flex items-center justify-between">
                    <h4 className="text-sm font-bold text-slate-800">📊 إحصائيات المرويات (جوامع الكلم)</h4>
                    {narrator.uniqueHadithCount != null && (
                      <span
                        className={`px-2 py-0.5 text-xs font-bold rounded-full ${
                          narrator.uniqueHadithCount <= 5
                            ? 'bg-amber-100 text-amber-800 border border-amber-300'
                            : narrator.uniqueHadithCount >= 500
                              ? 'bg-emerald-100 text-emerald-800 border border-emerald-300'
                              : 'bg-blue-100 text-blue-800 border border-blue-200'
                        }`}
                      >
                        {narrator.uniqueHadithCount <= 5
                          ? 'راوٍ مُقِلّ (يُحذر من تفرده)'
                          : narrator.uniqueHadithCount >= 500
                            ? 'إمام حافظ مُكثِر'
                            : 'متوسط الرواية'}
                      </span>
                    )}
                  </div>
                  <div className="grid grid-cols-2 gap-2 pt-1">
                    {narrator.uniqueHadithCount != null && (
                      <div className="bg-white p-2.5 rounded-lg border border-blue-100 text-center">
                        <div className="text-lg font-bold text-brand-dark">{narrator.uniqueHadithCount.toLocaleString('ar-EG')}</div>
                        <div className="text-[11px] text-slate-500">أطراف الأحاديث الفريدة</div>
                      </div>
                    )}
                    {narrator.totalNarrationsCount != null && (
                      <div className="bg-white p-2.5 rounded-lg border border-blue-100 text-center">
                        <div className="text-lg font-bold text-brand-blue">{narrator.totalNarrationsCount.toLocaleString('ar-EG')}</div>
                        <div className="text-[11px] text-slate-500">إجمالي الأسانيد والطرق</div>
                      </div>
                    )}
                  </div>
                </div>
              )}

              {/* Bio */}
              {narrator.biography && (
                <div>
                  <h4 className="text-lg font-semibold text-slate-800 mb-2">ترجمة الراوي</h4>
                  <p className="text-slate-600 text-sm leading-relaxed whitespace-pre-wrap">
                    {narrator.biography}
                  </p>
                </div>
              )}

              {/* AI Summary Section */}
              {narrator.evaluations.length > 0 && (
                <div className="bg-purple-50 rounded-xl p-4 border border-purple-100">
                  <div className="flex items-center gap-2 mb-3">
                    <Sparkles className="w-5 h-5 text-purple-600" />
                    <h4 className="text-purple-900 font-bold">الاستخراج الذكي (AI)</h4>
                  </div>
                  
                  {!aiSummary && !isAiLoading && !aiError && (
                    <button 
                      onClick={handleGenerateSummary}
                      className="w-full py-2 bg-purple-600 hover:bg-purple-700 text-white rounded-lg text-sm font-semibold transition-colors"
                    >
                      استخراج التقييم الأكاديمي
                    </button>
                  )}

                  {isAiLoading && (
                    <div className="text-sm text-purple-600 animate-pulse text-center py-2">
                      جاري البحث واستخراج الأقوال...
                    </div>
                  )}

                  {aiError && (
                    <div className="text-sm text-red-500 bg-red-50 p-3 rounded-lg flex gap-2 items-start">
                      <AlertCircle className="w-4 h-4 shrink-0 mt-0.5" />
                      <span>{aiError}</span>
                    </div>
                  )}

                  {aiSummary && (
                    <div className="text-sm text-purple-900 leading-relaxed font-arabic space-y-2">
                      <div className="bg-white p-3 rounded shadow-sm border border-purple-100">
                        <p className="font-bold text-lg mb-1">«{aiSummary.verbatimQuote}»</p>
                        <p className="text-xs text-purple-600 mb-3">— {aiSummary.sourceBook}</p>
                        <div className="flex gap-2 items-center bg-purple-50 p-2 rounded">
                          <span className="px-2 py-1 bg-purple-200 text-purple-900 rounded text-xs font-bold shadow-sm">
                            {aiSummary.tier}
                          </span>
                          <span className="text-xs text-slate-700">
                            {aiSummary.justification}
                          </span>
                        </div>
                      </div>
                    </div>
                  )}
                </div>
              )}

              {/* Scholar Evaluations */}
              <div>
                <h4 className="text-lg font-semibold text-slate-800 mb-3">أقوال الجرح والتعديل</h4>
                {narrator.evaluations.length > 0 ? (
                  <div className="space-y-3">
                    {narrator.evaluations.map((evalRecord, idx) => (
                      <div key={idx} className="bg-slate-50 p-3 rounded border border-slate-100">
                        <div className="flex justify-between items-start mb-1">
                          <span className="font-semibold text-brand-teal text-sm">{getScholarAr(evalRecord.scholarName)}</span>
                          {evalRecord.verdictRating && (
                            <span className="text-xs px-1.5 py-0.5 bg-white border border-slate-200 rounded text-slate-600">
                              {getVerdictAr(evalRecord.verdictRating)}
                            </span>
                          )}
                        </div>
                        <p className="text-slate-700 text-sm italic">&quot;{evalRecord.evaluationText}&quot;</p>
                      </div>
                    ))}
                  </div>
                ) : (
                  <p className="text-sm text-slate-500">لا توجد أقوال مسجلة لهذا الراوي.</p>
                )}
              </div>
            </>
          )}
        </div>
      </div>
    </>
  );
}
