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
  evaluations: ScholarEvaluationDto[];
}
