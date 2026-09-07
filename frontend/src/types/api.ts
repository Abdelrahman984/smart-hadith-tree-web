export interface HadithSearchResultDto {
  id: string;
  bookName: string;
  hadithNumber: number;
  chapter: string | null;
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
  biography: string | null;
  gradeEn?: string;
  evaluations: ScholarEvaluationDto[];
}

export interface ComparativeHadithSourceDto {
  hadithId: string;
  bookName: string;
  hadithNumber: number;
  matnSnippet: string;
}

export interface ComparativeIsnadNodeDto extends IsnadNodeDto {
  sourceHadithIds: string[];
  sourceBooks: string[];
}

export interface ComparativeTreeResponseDto {
  sources: ComparativeHadithSourceDto[];
  nodes: ComparativeIsnadNodeDto[];
}
