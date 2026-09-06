import Link from "next/link";
import { BookOpen, Network, ShieldCheck } from "lucide-react";

export default function Home() {
  return (
    <div className="min-h-screen bg-slate-50 flex flex-col items-center justify-center p-6 text-center">
      <div className="max-w-3xl space-y-8">
        <h1 className="text-5xl md:text-7xl font-bold text-brand-dark tracking-tight">
          شجرة الأسانيد <span className="text-brand-teal">الذكية</span>
        </h1>
        <p className="text-xl text-slate-600 leading-relaxed font-arabic">
          منصة متقدمة لرقمنة وتصور أسانيد الأحاديث النبوية، مع تحليل آلي لرجال السند باستخدام تقنيات الذكاء الاصطناعي لتقديم أحكام الجرح والتعديل.
        </p>
        
        <div className="flex items-center justify-center pt-4 gap-4">
          <Link 
            href="/search"
            className="px-8 py-4 bg-brand-blue text-white rounded-xl text-xl font-bold shadow-lg hover:bg-brand-dark hover:shadow-xl hover:-translate-y-1 transition-all duration-300"
          >
            ابدأ البحث في الأحاديث
          </Link>
          <Link 
            href="/books"
            className="px-8 py-4 bg-brand-teal text-white rounded-xl text-xl font-bold shadow-lg hover:bg-teal-700 hover:shadow-xl hover:-translate-y-1 transition-all duration-300"
          >
            تصفح الكتب
          </Link>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-8 pt-16">
          <div className="bg-white p-6 rounded-2xl shadow-sm border border-slate-100">
            <div className="w-12 h-12 bg-brand-teal/10 rounded-xl flex items-center justify-center mx-auto mb-4 text-brand-teal">
              <Network className="w-6 h-6" />
            </div>
            <h3 className="text-lg font-bold text-slate-800 mb-2">تصور شبكي للأسانيد</h3>
            <p className="text-slate-600 text-sm">عرض تفاعلي لشجرة الرواة يوضح طرق الحديث وتفرداته بوضوح تام.</p>
          </div>
          
          <div className="bg-white p-6 rounded-2xl shadow-sm border border-slate-100">
            <div className="w-12 h-12 bg-brand-blue/10 rounded-xl flex items-center justify-center mx-auto mb-4 text-brand-blue">
              <BookOpen className="w-6 h-6" />
            </div>
            <h3 className="text-lg font-bold text-slate-800 mb-2">قاعدة بيانات ضخمة</h3>
            <p className="text-slate-600 text-sm">ربط بين متون الأحاديث وتراجم الرواة من كتب الجرح والتعديل المعتمدة.</p>
          </div>
          
          <div className="bg-white p-6 rounded-2xl shadow-sm border border-slate-100">
            <div className="w-12 h-12 bg-purple-500/10 rounded-xl flex items-center justify-center mx-auto mb-4 text-purple-600">
              <ShieldCheck className="w-6 h-6" />
            </div>
            <h3 className="text-lg font-bold text-slate-800 mb-2">تحليل ذكي بالذكاء الاصطناعي</h3>
            <p className="text-slate-600 text-sm">تلخيص آلي لأقوال العلماء لبيان خلاصة حال الراوي بدقة وسرعة.</p>
          </div>
        </div>
      </div>
    </div>
  );
}
