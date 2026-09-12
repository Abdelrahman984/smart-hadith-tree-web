export interface BookMeta {
  code: string;
  name: string;
  color: string;
  badgeClass: string;
  borderClass: string;
  bgLightClass: string;
}

export const KNOWN_BOOKS: Record<string, BookMeta> = {
  "صحيح البخاري": {
    code: "خ",
    name: "صحيح البخاري",
    color: "#2563eb",
    badgeClass: "bg-blue-50 text-blue-700 border-blue-200 hover:bg-blue-100",
    borderClass: "border-blue-500",
    bgLightClass: "bg-blue-50/40",
  },
  "صحيح مسلم": {
    code: "م",
    name: "صحيح مسلم",
    color: "#16a34a",
    badgeClass: "bg-emerald-50 text-emerald-700 border-emerald-200 hover:bg-emerald-100",
    borderClass: "border-emerald-500",
    bgLightClass: "bg-emerald-50/40",
  },
  "سنن أبي داود": {
    code: "د",
    name: "سنن أبي داود",
    color: "#d97706",
    badgeClass: "bg-amber-50 text-amber-700 border-amber-200 hover:bg-amber-100",
    borderClass: "border-amber-500",
    bgLightClass: "bg-amber-50/40",
  },
  "جامع الترمذي": {
    code: "ت",
    name: "جامع الترمذي",
    color: "#9333ea",
    badgeClass: "bg-purple-50 text-purple-700 border-purple-200 hover:bg-purple-100",
    borderClass: "border-purple-500",
    bgLightClass: "bg-purple-50/40",
  },
  "سنن النسائي": {
    code: "س",
    name: "سنن النسائي",
    color: "#0284c7",
    badgeClass: "bg-sky-50 text-sky-700 border-sky-200 hover:bg-sky-100",
    borderClass: "border-sky-500",
    bgLightClass: "bg-sky-50/40",
  },
  "سنن ابن ماجه": {
    code: "ق",
    name: "سنن ابن ماجه",
    color: "#e11d48",
    badgeClass: "bg-rose-50 text-rose-700 border-rose-200 hover:bg-rose-100",
    borderClass: "border-rose-500",
    bgLightClass: "border-rose-50/40",
  },
  "مسند أحمد": {
    code: "حم",
    name: "مسند أحمد",
    color: "#b45309",
    badgeClass: "bg-orange-50 text-orange-800 border-orange-200 hover:bg-orange-100",
    borderClass: "border-orange-500",
    bgLightClass: "bg-orange-50/40",
  },
  "موطأ مالك": {
    code: "ط",
    name: "موطأ مالك",
    color: "#0d9488",
    badgeClass: "bg-teal-50 text-teal-700 border-teal-200 hover:bg-teal-100",
    borderClass: "border-teal-500",
    bgLightClass: "bg-teal-50/40",
  },
};

const DEFAULT_BOOK_META: BookMeta = {
  code: "ك",
  name: "كتاب حديث",
  color: "#64748b",
  badgeClass: "bg-slate-50 text-slate-700 border-slate-200 hover:bg-slate-100",
  borderClass: "border-slate-400",
  bgLightClass: "bg-slate-50/40",
};

export function getBookMeta(bookName: string): BookMeta {
  return KNOWN_BOOKS[bookName] || {
    ...DEFAULT_BOOK_META,
    name: bookName,
  };
}
