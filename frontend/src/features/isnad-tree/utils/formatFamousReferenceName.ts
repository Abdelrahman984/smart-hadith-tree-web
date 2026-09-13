import { formatTwoPartNarratorName } from "./formatNarratorName";

/**
 * Mapping of canonical Hadith books and compilers to their universally recognized famous names.
 */
const CANONICAL_COMPILERS: Array<{
  matchKey: string;
  famousName: string;
  shortName: string;
}> = [
  { matchKey: "البخاري", famousName: "البخاري", shortName: "البخاري" },
  { matchKey: "مسلم", famousName: "مسلم", shortName: "مسلم" },
  { matchKey: "أبي داود", famousName: "أبو داود", shortName: "أبو داود" },
  { matchKey: "ابو داود", famousName: "أبو داود", shortName: "أبو داود" },
  { matchKey: "الترمذي", famousName: "الترمذي", shortName: "الترمذي" },
  { matchKey: "النسائي", famousName: "النسائي", shortName: "النسائي" },
  { matchKey: "ابن ماجه", famousName: "ابن ماجه", shortName: "ابن ماجه" },
  { matchKey: "ابن ماجة", famousName: "ابن ماجه", shortName: "ابن ماجه" },
  { matchKey: "أحمد", famousName: "أحمد بن حنبل", shortName: "أحمد" },
  { matchKey: "احمد", famousName: "أحمد بن حنبل", shortName: "أحمد" },
  { matchKey: "مالك", famousName: "مالك بن أنس", shortName: "مالك" },
  { matchKey: "الدارمي", famousName: "الدارمي", shortName: "الدارمي" },
];

/**
 * Resolves the famous reference compiler name (e.g. 'البخاري' instead of 'محمد بن إسماعيل').
 */
export function getFamousReferenceOwnerName(
  narratorName?: string | null,
  knownAs?: string | null,
  bookName?: string | null
): string {
  // 1. Check against canonical mappings using bookName first
  if (bookName) {
    for (const item of CANONICAL_COMPILERS) {
      if (bookName.includes(item.matchKey)) {
        return item.famousName;
      }
    }
  }

  // 2. Check knownAs
  if (knownAs) {
    for (const item of CANONICAL_COMPILERS) {
      if (knownAs.includes(item.matchKey)) {
        return item.famousName;
      }
    }
    // Clean knownAs if it contains titles like "الإمام البخاري"
    const cleanedKnown = knownAs.replace(/^(?:الإمام|الشيخ|الحافظ|العلامة)\s+/, "").trim();
    if (cleanedKnown && !cleanedKnown.includes("بن")) {
      return cleanedKnown;
    }
  }

  // 3. Check narratorName / fullName
  if (narratorName) {
    for (const item of CANONICAL_COMPILERS) {
      if (narratorName.includes(item.matchKey)) {
        return item.famousName;
      }
    }
  }

  // 4. Fallback: if knownAs exists, use it; otherwise use formatTwoPartNarratorName
  if (knownAs && knownAs.trim()) {
    return knownAs.trim();
  }

  return formatTwoPartNarratorName(narratorName);
}
