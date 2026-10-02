import Link from "next/link";
import {
  BookOpen,
  Network,
  ShieldCheck,
  Search,
  GitCompareArrows,
  ShieldAlert,
  Users,
  Sparkles,
  Library,
  ArrowLeft,
  CheckCircle2,
} from "lucide-react";
import { getBookMeta } from "@/lib/bookTheme";

interface CorpusBookItem {
  name: string;
  author: string;
  hadithsCount: string;
}

interface CorpusCategory {
  title: string;
  subtitle: string;
  books: CorpusBookItem[];
}

const CORPUS_CATEGORIES: CorpusCategory[] = [
  {
    title: "الصحاح والمستخرجات والمستدركات",
    subtitle: "أعلى دواوين السنة شرطاً وتوثيقاً واستدراكاً",
    books: [
      { name: "صحيح البخاري", author: "الإمام البخاري (256 هـ)", hadithsCount: "7,563" },
      { name: "صحيح مسلم", author: "الإمام مسلم (261 هـ)", hadithsCount: "7,500+" },
      { name: "صحيح ابن خزيمة", author: "ابن خزيمة (311 هـ)", hadithsCount: "2,851" },
      { name: "مستخرج أبي عوانة", author: "أبو عوانة الإسفراييني (316 هـ)", hadithsCount: "13,036" },
      { name: "صحيح ابن حبان", author: "ابن حبان البستي (354 هـ)", hadithsCount: "7,447" },
      { name: "المستدرك على الصحيحين", author: "الحاكم النيسابوري (405 هـ)", hadithsCount: "5,799" },
    ],
  },
  {
    title: "السنن والجوامع",
    subtitle: "موسوعات أحاديث الأحكام والسنن الصغرى والكبرى",
    books: [
      { name: "سنن سعيد بن منصور", author: "سعيد بن منصور (227 هـ)", hadithsCount: "2,712" },
      { name: "سنن الدارمي", author: "الإمام الدارمي (255 هـ)", hadithsCount: "3,500+" },
      { name: "سنن أبي داود", author: "أبو داود السجستاني (275 هـ)", hadithsCount: "5,274" },
      { name: "سنن ابن ماجه", author: "ابن ماجه القزويني (273 هـ)", hadithsCount: "4,341" },
      { name: "جامع الترمذي", author: "أبو عيسى الترمذي (279 هـ)", hadithsCount: "3,956" },
      { name: "سنن النسائي", author: "الإمام النسائي (303 هـ)", hadithsCount: "5,758" },
      { name: "السنن الكبرى للنسائي", author: "الإمام النسائي (303 هـ)", hadithsCount: "11,444" },
      { name: "سنن الدارقطني", author: "الإمام الدارقطني (385 هـ)", hadithsCount: "4,231" },
      { name: "السنن الكبرى للبيهقي", author: "الإمام البيهقي (458 هـ)", hadithsCount: "11,655" },
    ],
  },
  {
    title: "الموطآت والمصنفات المبكرة",
    subtitle: "أقدم الجوامع الحديثية والفقهية المسندة لعصر التابعين وأتباعهم",
    books: [
      { name: "موطأ مالك", author: "الإمام مالك بن أنس (179 هـ)", hadithsCount: "1,800+" },
      { name: "مصنف عبد الرزاق", author: "عبد الرزاق الصنعاني (211 هـ)", hadithsCount: "18,422" },
      { name: "مصنف ابن أبي شيبة", author: "ابن أبي شيبة (235 هـ)", hadithsCount: "37,000+" },
    ],
  },
  {
    title: "المسانيد",
    subtitle: "الدواوين المرتبة على مسانيد الصحابة لتتبع الطرق والمتابعات",
    books: [
      { name: "مسند أبي داود الطيالسي", author: "أبو داود الطيالسي (204 هـ)", hadithsCount: "2,891" },
      { name: "مسند الشافعي", author: "الإمام الشافعي (204 هـ)", hadithsCount: "1,678" },
      { name: "مسند الحميدي", author: "الإمام الحميدي (219 هـ)", hadithsCount: "1,214" },
      { name: "مسند إسحاق بن راهويه", author: "إسحاق بن راهويه (238 هـ)", hadithsCount: "2,083" },
      { name: "مسند أحمد", author: "الإمام أحمد بن حنبل (241 هـ)", hadithsCount: "27,647" },
      { name: "مسند البزار", author: "أبو بكر البزار (292 هـ)", hadithsCount: "4,470" },
      { name: "مسند أبي يعلى الموصلي", author: "أبو يعلى الموصلي (307 هـ)", hadithsCount: "7,333" },
    ],
  },
  {
    title: "المعاجم والأجزاء والآداب",
    subtitle: "معاجم الشيوخ ومصنفات الأخلاق والشمائل النبوية",
    books: [
      { name: "الأدب المفرد", author: "الإمام البخاري (256 هـ)", hadithsCount: "1,322" },
      { name: "الشمائل المحمدية", author: "أبو عيسى الترمذي (279 هـ)", hadithsCount: "415" },
      { name: "المعجم الكبير للطبراني", author: "الإمام الطبراني (360 هـ)", hadithsCount: "14,549" },
      { name: "المعجم الأوسط للطبراني", author: "الإمام الطبراني (360 هـ)", hadithsCount: "9,444" },
      { name: "المعجم الصغير للطبراني", author: "الإمام الطبراني (360 هـ)", hadithsCount: "1,187" },
      { name: "شعب الإيمان للبيهقي", author: "الإمام البيهقي (458 هـ)", hadithsCount: "6,215" },
    ],
  },
];

const QUICK_SEARCH_EXAMPLES = [
  "إنما الأعمال بالنيات",
  "الزهري عن سالم عن أبيه",
  "لا ضرر ولا ضرار",
  "سفيان الثوري",
  "معمر عن الزهري",
];

export default function Home() {
  return (
    <div className="min-h-screen bg-slate-50 flex flex-col">
      {/* Top Navbar */}
      <header className="sticky top-0 z-30 bg-white/85 backdrop-blur-md border-b border-slate-200/80">
        <div className="max-w-7xl mx-auto px-6 h-16 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-brand-blue text-white flex items-center justify-center shadow-sm">
              <Network className="w-5 h-5 text-brand-teal" />
            </div>
            <div>
              <span className="text-lg font-bold text-brand-dark">شجرة الأسانيد الذكية</span>
              <span className="hidden sm:inline-block mr-2 text-xs font-semibold px-2 py-0.5 rounded-full bg-teal-50 text-teal-700 border border-teal-200">
                31 ديواناً مسنداً
              </span>
            </div>
          </div>

          <nav className="flex items-center gap-3">
            <Link
              href="/books"
              className="px-4 py-2 rounded-xl text-sm font-semibold text-slate-700 hover:bg-slate-100 transition-colors flex items-center gap-2"
            >
              <BookOpen className="w-4 h-4 text-brand-teal" />
              <span>فهرس الدواوين (31)</span>
            </Link>
            <Link
              href="/search"
              className="px-4 py-2 rounded-xl text-sm font-bold bg-brand-blue text-white hover:bg-brand-dark transition-colors flex items-center gap-2 shadow-sm"
            >
              <Search className="w-4 h-4" />
              <span>البحث والتخريج</span>
            </Link>
          </nav>
        </div>
      </header>

      {/* Hero Section */}
      <section className="relative overflow-hidden pt-14 pb-16 px-6 bg-gradient-to-b from-white via-slate-50 to-slate-100/70 border-b border-slate-200/70">
        <div className="max-w-5xl mx-auto text-center space-y-7">
          <div className="inline-flex items-center gap-2 px-4 py-1.5 rounded-full bg-brand-blue/5 border border-brand-blue/15 text-brand-blue text-sm font-semibold">
            <Sparkles className="w-4 h-4 text-brand-teal" />
            <span>الجيل الجديد من رقمنة علوم الحديث والتخريج ودراسة الأسانيد والعلل</span>
          </div>

          <h1 className="text-4xl sm:text-6xl md:text-7xl font-bold text-brand-dark tracking-tight leading-tight">
            شجرة الأسانيد <span className="text-brand-teal">الذكية</span>
          </h1>

          <p className="text-lg sm:text-xl text-slate-600 max-w-3xl mx-auto leading-relaxed">
            منصة علمية متكاملة تجمع <strong className="text-slate-800">31 ديواناً من أصول السنة المسندة</strong> (الصحاح، السنن، المصنفات، المسانيد، المعاجم، والمستدركات) لرسم أشجار الأسانيد تفاعلياً، والتخريج المقارن، ورصد علل الانقطاع والتدليس والاختلاط، وتلخيص الجرح والتعديل بالذكاء الاصطناعي.
          </p>

          {/* Direct Search Bar on Home */}
          <form
            action="/search"
            method="GET"
            className="max-w-2xl mx-auto pt-2"
          >
            <div className="relative flex items-center bg-white rounded-2xl shadow-lg border border-slate-200/90 focus-within:border-brand-teal focus-within:ring-4 focus-within:ring-brand-teal/15 transition-all p-2">
              <div className="pr-3 pl-2 text-slate-400">
                <Search className="w-6 h-6" />
              </div>
              <input
                type="text"
                name="q"
                placeholder="ابحث بطرف الحديث، أو اسم الراوي، أو سلسلة الإسناد (مثال: الزهري عن سالم)..."
                className="w-full py-3 px-2 text-base sm:text-lg text-slate-800 placeholder:text-slate-400 focus:outline-none bg-transparent"
              />
              <button
                type="submit"
                className="shrink-0 px-6 py-3 bg-brand-blue hover:bg-brand-dark text-white font-bold rounded-xl transition-colors flex items-center gap-2 cursor-pointer"
              >
                <span>بحث</span>
                <ArrowLeft className="w-4 h-4" />
              </button>
            </div>
          </form>

          {/* Quick Search Chips */}
          <div className="flex items-center justify-center flex-wrap gap-2 pt-1 text-sm">
            <span className="text-slate-500 font-medium">نماذج بحث سريعة:</span>
            {QUICK_SEARCH_EXAMPLES.map((example) => (
              <Link
                key={example}
                href={`/search?q=${encodeURIComponent(example)}`}
                className="px-3 py-1 rounded-full bg-white border border-slate-200 text-slate-700 hover:border-brand-teal hover:text-brand-blue hover:bg-teal-50/30 transition-all text-xs font-semibold shadow-2xs"
              >
                {example}
              </Link>
            ))}
          </div>

          {/* Primary Action Buttons */}
          <div className="flex flex-wrap items-center justify-center pt-3 gap-4">
            <Link
              href="/search"
              className="px-7 py-3.5 bg-brand-blue text-white rounded-xl text-base font-bold shadow-md hover:bg-brand-dark hover:shadow-lg hover:-translate-y-0.5 transition-all flex items-center gap-2.5"
            >
              <GitCompareArrows className="w-5 h-5 text-brand-teal" />
              <span>محرك البحث والتخريج المقارن</span>
            </Link>
            <Link
              href="/books"
              className="px-7 py-3.5 bg-white text-brand-dark border border-slate-200 rounded-xl text-base font-bold shadow-sm hover:border-brand-teal hover:shadow-md hover:-translate-y-0.5 transition-all flex items-center gap-2.5"
            >
              <Library className="w-5 h-5 text-brand-teal" />
              <span>تصفح الدواوين الـ 31</span>
            </Link>
          </div>
        </div>

        {/* Live Database Stats Bar */}
        <div className="max-w-5xl mx-auto mt-12 grid grid-cols-2 lg:grid-cols-4 gap-4">
          <div className="bg-white/90 backdrop-blur-xs p-5 rounded-2xl border border-slate-200/80 shadow-xs text-center">
            <div className="text-3xl sm:text-4xl font-bold text-brand-blue font-latin">31</div>
            <div className="text-sm font-bold text-slate-800 mt-1">ديواناً حديثياً مسنداً</div>
            <div className="text-xs text-slate-500 mt-0.5">الصحاح والسنن والمصنفات والمسانيد والمعاجم</div>
          </div>

          <div className="bg-white/90 backdrop-blur-xs p-5 rounded-2xl border border-slate-200/80 shadow-xs text-center">
            <div className="text-3xl sm:text-4xl font-bold text-brand-teal font-latin">233,224</div>
            <div className="text-sm font-bold text-slate-800 mt-1">حديث وأثر مسند</div>
            <div className="text-xs text-slate-500 mt-0.5">مفهرسة ومطبّعة للبحث الفوري</div>
          </div>

          <div className="bg-white/90 backdrop-blur-xs p-5 rounded-2xl border border-slate-200/80 shadow-xs text-center">
            <div className="text-3xl sm:text-4xl font-bold text-emerald-600 font-latin">1,078,668</div>
            <div className="text-sm font-bold text-slate-800 mt-1">حلقة إسناد متصلة</div>
            <div className="text-xs text-slate-500 mt-0.5">روابط شيخ–تلميذ مفكوكة الاشتباك سياقياً</div>
          </div>

          <div className="bg-white/90 backdrop-blur-xs p-5 rounded-2xl border border-slate-200/80 shadow-xs text-center">
            <div className="text-3xl sm:text-4xl font-bold text-purple-600 font-latin">115,735</div>
            <div className="text-sm font-bold text-slate-800 mt-1">ترجمة راوٍ في القاعدة</div>
            <div className="text-xs text-slate-500 mt-0.5">مع أقوال أئمة الجرح والتعديل والطبقة</div>
          </div>
        </div>
      </section>

      {/* Core Capabilities Section */}
      <section className="py-16 px-6 max-w-7xl mx-auto w-full">
        <div className="text-center max-w-3xl mx-auto mb-12 space-y-3">
          <h2 className="text-2xl sm:text-3xl font-bold text-brand-dark">
            إمكانات هندسية وعلمية متقدمة لدراسة الأسانيد
          </h2>
          <p className="text-slate-600 text-base">
            تجمع المنصة بين قواعد صناعة الحديث التراثية وأحدث خوارزميات الرسوم البيانية (Graph Layout) والذكاء الاصطناعي التوليدي.
          </p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          <div className="bg-white p-6 rounded-2xl shadow-xs border border-slate-200/80 hover:shadow-md transition-shadow">
            <div className="w-12 h-12 bg-brand-teal/10 rounded-xl flex items-center justify-center mb-4 text-brand-teal">
              <Network className="w-6 h-6" />
            </div>
            <h3 className="text-lg font-bold text-slate-800 mb-2">تصور شبكي تفاعلي للأسانيد</h3>
            <p className="text-slate-600 text-sm leading-relaxed">
              رسم شجري تفاعلي (ELK Graph) يوضح طبقات الرواة من الصحابي إلى المصنف، ويكشف مدارات الحديث والتفردات والمتابعات بوضوح بصري فائق.
            </p>
          </div>

          <div className="bg-white p-6 rounded-2xl shadow-xs border border-slate-200/80 hover:shadow-md transition-shadow">
            <div className="w-12 h-12 bg-rose-500/10 rounded-xl flex items-center justify-center mb-4 text-rose-600">
              <ShieldAlert className="w-6 h-6" />
            </div>
            <h3 className="text-lg font-bold text-slate-800 mb-2">محرك كشف العلل والانقطاع</h3>
            <p className="text-slate-600 text-sm leading-relaxed">
              فحص آلي لاتصال السند، ورصد الانقطاع، والتنبيه على عنعنة المدلسين (وفق مراتب ابن حجر الخمس)، وتمييز الرواة المختلطين (من سمع منهم قبل الاختلاط أو بعده).
            </p>
          </div>

          <div className="bg-white p-6 rounded-2xl shadow-xs border border-slate-200/80 hover:shadow-md transition-shadow">
            <div className="w-12 h-12 bg-brand-blue/10 rounded-xl flex items-center justify-center mb-4 text-brand-blue">
              <GitCompareArrows className="w-6 h-6" />
            </div>
            <h3 className="text-lg font-bold text-slate-800 mb-2">التخريج المقارن عبر 31 ديواناً</h3>
            <p className="text-slate-600 text-sm leading-relaxed">
              دمج عدة روايات للحديث من مختلف الصحاح والسنن والمسانيد والمعاجم في شجرة مقارنة واحدة لتحديد المدار المشترك ومواطن الزيادة والشذوذ.
            </p>
          </div>

          <div className="bg-white p-6 rounded-2xl shadow-xs border border-slate-200/80 hover:shadow-md transition-shadow">
            <div className="w-12 h-12 bg-amber-500/10 rounded-xl flex items-center justify-center mb-4 text-amber-600">
              <Users className="w-6 h-6" />
            </div>
            <h3 className="text-lg font-bold text-slate-800 mb-2">فك الاشتباك السياقي للرواة</h3>
            <p className="text-slate-600 text-sm leading-relaxed">
              خوارزمية ذكية لتمييز الرواة المتشابهين أو المهملين في السند (مثل سفيان، معمر، ابن جريج، يحيى بن سعيد) بالاعتماد على شبكة الشيوخ والتلاميذ والطبقة.
            </p>
          </div>

          <div className="bg-white p-6 rounded-2xl shadow-xs border border-slate-200/80 hover:shadow-md transition-shadow">
            <div className="w-12 h-12 bg-purple-500/10 rounded-xl flex items-center justify-center mb-4 text-purple-600">
              <ShieldCheck className="w-6 h-6" />
            </div>
            <h3 className="text-lg font-bold text-slate-800 mb-2">تلخيص الجرح والتعديل بالذكاء الاصطناعي</h3>
            <p className="text-slate-600 text-sm leading-relaxed">
              تحليل وتلخيص آلي لأقوال أئمة النقد (كالذهبي وابن حجر والمزي وابن معين) عبر تقنية RAG لتقديم خلاصة موثقة لحال الراوي ورتبته.
            </p>
          </div>

          <div className="bg-white p-6 rounded-2xl shadow-xs border border-slate-200/80 hover:shadow-md transition-shadow">
            <div className="w-12 h-12 bg-emerald-500/10 rounded-xl flex items-center justify-center mb-4 text-emerald-600">
              <BookOpen className="w-6 h-6" />
            </div>
            <h3 className="text-lg font-bold text-slate-800 mb-2">بحث متقدم في المتون والأسانيد</h3>
            <p className="text-slate-600 text-sm leading-relaxed">
              بحث فوري يتجاوز التشكيل والهمزات مع إمكانية التصفية، والبحث بشرط وجود راوٍ معين في السند، واستكشاف المتابعات والشواهد آلياً.
            </p>
          </div>
        </div>
      </section>

      {/* 31 Canonical Books Showcase */}
      <section className="py-16 px-6 bg-white border-t border-slate-200/80">
        <div className="max-w-7xl mx-auto">
          <div className="flex flex-col md:flex-row md:items-end justify-between mb-10 gap-4">
            <div>
              <div className="inline-flex items-center gap-2 text-brand-teal font-bold text-sm mb-2">
                <CheckCircle2 className="w-4 h-4" />
                <span>المكتبة الحديثية الكاملة المدمجة</span>
              </div>
              <h2 className="text-2xl sm:text-3xl font-bold text-brand-dark">
                خزانة دواوين السنة المسندة (31 مصدراً أصلياً)
              </h2>
              <p className="text-slate-600 text-sm sm:text-base mt-1">
                تغطي الدواوين التسعة، والمصنفات المبكرة، والمسانيد، والصحاح والمستخرجات، والمعاجم الثلاثة، والسنن الكبرى والمستدركات.
              </p>
            </div>
            <Link
              href="/books"
              className="self-start md:self-auto px-5 py-2.5 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-800 font-bold text-sm transition-colors flex items-center gap-2"
            >
              <span>فتح فهرس الأبواب الكامل</span>
              <ArrowLeft className="w-4 h-4" />
            </Link>
          </div>

          <div className="space-y-8">
            {CORPUS_CATEGORIES.map((category) => (
              <div
                key={category.title}
                className="bg-slate-50/70 rounded-2xl p-6 border border-slate-200/80"
              >
                <div className="mb-4">
                  <h3 className="text-lg font-bold text-brand-dark">{category.title}</h3>
                  <p className="text-xs sm:text-sm text-slate-500">{category.subtitle}</p>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3.5">
                  {category.books.map((book) => {
                    const meta = getBookMeta(book.name);
                    return (
                      <Link
                        key={book.name}
                        href={`/books/${encodeURIComponent(book.name)}`}
                        className="group bg-white p-4 rounded-xl border border-slate-200/80 hover:border-brand-teal hover:shadow-md transition-all flex items-center justify-between gap-3"
                      >
                        <div className="flex items-center gap-3 min-w-0">
                          <span
                            className={`w-10 h-10 shrink-0 rounded-xl flex items-center justify-center text-xs font-bold border ${meta.badgeClass}`}
                          >
                            {meta.code}
                          </span>
                          <div className="min-w-0">
                            <div className="font-bold text-slate-800 group-hover:text-brand-blue transition-colors truncate">
                              {book.name}
                            </div>
                            <div className="text-xs text-slate-500 truncate">{book.author}</div>
                          </div>
                        </div>

                        <span className="shrink-0 text-xs font-semibold font-latin px-2.5 py-1 rounded-lg bg-slate-100 text-slate-600 group-hover:bg-brand-teal/10 group-hover:text-brand-dark transition-colors">
                          {book.hadithsCount}
                        </span>
                      </Link>
                    );
                  })}
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Footer */}
      <footer className="mt-auto py-8 px-6 bg-brand-dark text-slate-300 text-sm border-t border-slate-800">
        <div className="max-w-7xl mx-auto flex flex-col sm:flex-row items-center justify-between gap-4">
          <div className="flex items-center gap-2.5">
            <Network className="w-5 h-5 text-brand-teal" />
            <span className="font-bold text-white">شجرة الأسانيد الذكية (Smart Hadith Tree)</span>
          </div>
          <div className="text-xs text-slate-400 text-center sm:text-left">
            موسوعة رقمية لدراسة الأسانيد والتخريج وكشف العلل • تضم 31 ديواناً مسنداً (+233,000 حديث و +1,078,000 حلقة إسناد)
          </div>
        </div>
      </footer>
    </div>
  );
}
