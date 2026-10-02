export interface HadithSearchResultDto {
  id: string;
  bookName: string;
  hadithNumber: number;
  chapter: string | null;
  matnArabic?: string;
  matnSnippet: string;
}

export interface IsnadNodeDto {
  id: string;
  narratorId: string;
  narratorName: string;
  knownAs: string | null;
  generationTier: string | null;
  stepOrder: number;
  parentNodeId: string | null;
  transmissionTerm: string | null;
  gradeEn?: string;
  isMudallis?: boolean;
  hasMukhtalit?: boolean;
  residencePlaces?: string | null;
  deathPlace?: string | null;
  gawamiRank?: string | null;
  totalNarrationsCount?: number | null;
  uniqueHadithCount?: number | null;
  isAnomaly?: boolean;
  anomalyReason?: string;
}

export interface IsnadTreeResponseDto {
  hadithId: string;
  bookName: string;
  hadithNumber: number;
  matnArabic: string;
  nodes: IsnadNodeDto[];
}

export interface NarratorSummaryDto {
  id: string;
  fullName: string;
  generationTier: string | null;
  gradeSummary: string;
  gradeEn?: string;
}

export interface ScholarEvaluationDto {
  scholarName: string;
  evaluationText: string;
  sourceBook: string | null;
  verdictRating: string | null;
}

export interface NarratorDetailDto {
  id: string;
  fullName: string;
  knownAs: string | null;
  kunyah: string | null;
  generationTier: string | null;
  birthYearHijri: number | null;
  deathYearHijri: number | null;
  residencePlaces?: string | null;
  deathPlace?: string | null;
  gawamiRank?: string | null;
  totalNarrationsCount?: number | null;
  uniqueHadithCount?: number | null;
  isMudallis?: boolean;
  hasMukhtalit?: boolean;
  biography: string | null;
  gradeEn?: string;
  evaluations: ScholarEvaluationDto[];
}

export interface ComparativeHadithSourceDto {
  hadithId: string;
  bookName: string;
  hadithNumber: number;
  matnArabic?: string;
  matnSnippet: string;
}

export interface ComparativeIsnadNodeDto extends IsnadNodeDto {
  sourceHadithIds: string[];
  sourceBooks: string[];
  hasMatnVariation?: boolean;
  matnVariationSnippet?: string;
}

export interface ComparativeTreeResponseDto {
  sources: ComparativeHadithSourceDto[];
  nodes: ComparativeIsnadNodeDto[];
  calculatedGrade?: string;
  taqwiyahDetails?: string;
  ilalReport?: IlalReportDto;
}

// ── علل الحديث (Ilal engine) ───────────────────────────────────────

export type IllahType =
  | "Tadlis"
  | "Ikhtilat"
  | "HiddenInqita"
  | "Ziyadah"
  | "Shudhudh"
  | "Nakarah"
  | "Idtirab"
  | "RafWaqf"
  | "WaslIrsal";

export type IllahSeverity = "Qadihah" | "GhayrQadihah" | "Tanbih";

export interface MatnSegmentDto {
  kind: "equal" | "added" | "removed";
  text: string;
}

export interface MatnComparisonDto {
  referenceHadithId: string;
  comparedHadithId: string;
  similarity: number;
  segments: MatnSegmentDto[];
}

export interface IlalFindingDto {
  type: IllahType;
  severity: IllahSeverity;
  titleAr: string;
  evidenceAr: string;
  narratorIds: string[];
  hadithIds: string[];
  confidence: number;
  matnComparison?: MatnComparisonDto;
}

export interface IlalMadarDto {
  narratorId: string;
  narratorName: string;
  branchCount: number;
  hadithIds: string[];
}

export interface IlalTariqDto {
  hadithId: string;
  bookName: string;
  hadithNumber: number;
  isMarfu: boolean;
}

export interface IlalReportDto {
  analyzedHadithIds: string[];
  turuq: IlalTariqDto[];
  madars: IlalMadarDto[];
  findings: IlalFindingDto[];
  hasQadihah: boolean;
  summaryAr: string;
}

export interface IlalExplanationDto {
  explanationAr: string;
  caveats: string[];
}

export type SearchScope = 0 | 1 | 2; // 0: All, 1: Matn, 2: Isnad
export type SearchMatchType = 0 | 1 | 2; // 0: AllWords, 1: AnyWord, 2: Exact
export type SearchLogicalOperator = 0 | 1; // 0: And, 1: Or

export interface SearchRequestDto {
  query?: string;
  scope?: SearchScope;
  match?: SearchMatchType;
  phrases?: string[];
  operator?: SearchLogicalOperator;
  andPhrases?: string[];
  orPhrases?: string[];
  excludePhrases?: string[];
  isOrdered?: boolean;
  isProximity?: boolean;
  proximityWords?: number;
  page?: number;
  pageSize?: number;
}
