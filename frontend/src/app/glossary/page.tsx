import type { Metadata } from "next";
import Link from "next/link";
import { Home } from "lucide-react";
import { GLOSSARY } from "@/features/glossary/glossary";

export const metadata: Metadata = {
  title: "قاموس المصطلحات | شجرة الأسانيد الذكية",
  description: "تعريفات مبسطة لمصطلحات علم الحديث المستخدمة في الأداة، مع مقابلها الإنجليزي.",
};

export default function GlossaryPage() {
  return (
    <div className="mx-auto max-w-3xl p-6 pt-12">
      <Link href="/" className="mb-6 inline-flex items-center gap-1.5 text-sm font-semibold text-slate-600 hover:text-brand-dark">
        <Home className="h-4 w-4" /> الرئيسية
      </Link>
      <h1 className="mb-2 text-3xl font-bold text-brand-dark">قاموس المصطلحات</h1>
      <p className="mb-8 text-sm leading-relaxed text-slate-600">
        تعريفات مبسّطة لتقريب المصطلحات لغير المتخصصين، وليست فصلاً في خلافات أهل العلم في حدودها. للتحقيق الدقيق ارجع إلى كتب
        مصطلح الحديث وأهل الاختصاص.
      </p>

      <dl className="space-y-4">
        {GLOSSARY.map((e) => (
          <div key={e.id} id={e.id} className="scroll-mt-20 rounded-xl border border-slate-200 bg-white p-4 target:ring-2 target:ring-brand-blue">
            <dt className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
              <span className="text-lg font-bold text-slate-900">{e.term}</span>
              <span className="text-xs text-slate-500" dir="ltr" lang="en">
                {e.english} — {e.englishGloss}
              </span>
            </dt>
            <dd className="mt-1.5 text-sm leading-relaxed text-slate-700">{e.definition}</dd>
          </div>
        ))}
      </dl>
    </div>
  );
}
