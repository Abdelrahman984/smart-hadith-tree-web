import { Network } from "lucide-react";

export default function SiteFooter() {
  return (
    <footer className="mt-auto py-8 px-6 bg-brand-dark text-slate-300 text-sm border-t border-slate-800">
      <div className="max-w-7xl mx-auto flex flex-col sm:flex-row items-center justify-between gap-4">
        <div className="flex items-center gap-2.5">
          <Network className="w-5 h-5 text-brand-teal" />
          <span className="font-bold text-white">شجرة الأسانيد الذكية (Smart Hadith Tree)</span>
        </div>
        <div className="text-xs text-slate-400 text-center sm:text-left">
          موسوعة رقمية لدراسة الأسانيد والتخريج وكشف العلل
        </div>
      </div>
      <p className="max-w-7xl mx-auto mt-4 text-[11px] text-slate-500 text-center">
        أداة بحثية مدعومة بالذكاء الاصطناعي، وليست مصدراً للفتوى أو الحكم النهائي على الأحاديث؛ يُرجع في المسائل الدقيقة إلى أهل الاختصاص.
      </p>
    </footer>
  );
}
