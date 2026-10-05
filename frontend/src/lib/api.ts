import {
  HadithSearchResultDto,
  IsnadTreeResponseDto,
  NarratorDetailDto,
  NarratorSummaryDto,
  ComparativeTreeResponseDto,
  SearchRequestDto,
  IlalReportDto,
  IlalExplanationDto,
  HadithVerificationResultDto,
} from "@/types/api";

/**
 * Backend base URL. Set NEXT_PUBLIC_API_BASE (e.g. https://api.example.com/api) at build time;
 * it is inlined into the client bundle. Defaults to the local ASP.NET Core dev port.
 */
export const API_BASE = (process.env.NEXT_PUBLIC_API_BASE ?? "http://localhost:5147/api").replace(/\/+$/, "");

export async function searchHadiths(
  query: string,
  scope: number = 0,
  match: number = 0,
  page: number = 1,
  pageSize: number = 50
): Promise<HadithSearchResultDto[]> {
  const url = new URL(`${API_BASE}/Search`);
  url.searchParams.append("query", query);
  url.searchParams.append("scope", scope.toString());
  url.searchParams.append("match", match.toString());
  url.searchParams.append("page", page.toString());
  url.searchParams.append("pageSize", pageSize.toString());
  
  const res = await fetch(url.toString());
  if (!res.ok) throw new Error("Failed to search hadiths");
  return res.json();
}

export async function advancedSearchHadiths(req: SearchRequestDto): Promise<HadithSearchResultDto[]> {
  const res = await fetch(`${API_BASE}/Search/advanced`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(req),
  });
  if (!res.ok) throw new Error("Failed to execute advanced search");
  return res.json();
}

export async function getIsnadTree(hadithId: string): Promise<IsnadTreeResponseDto> {
  const res = await fetch(`${API_BASE}/Tree/${hadithId}`);
  if (!res.ok) throw new Error("Failed to fetch tree");
  return res.json();
}

export async function getNarratorDetails(id: string): Promise<NarratorDetailDto> {
  const res = await fetch(`${API_BASE}/Narrators/${id}`);
  if (!res.ok) throw new Error("Failed to fetch narrator details");
  return res.json();
}

export async function getNarratorTooltip(id: string): Promise<NarratorSummaryDto> {
  const res = await fetch(`${API_BASE}/Narrators/${id}/tooltip`);
  if (!res.ok) throw new Error("Failed to fetch narrator tooltip");
  return res.json();
}

export async function getBooks(): Promise<string[]> {
  const res = await fetch(`${API_BASE}/Books`);
  if (!res.ok) throw new Error("Failed to fetch books");
  return res.json();
}

export async function getChapters(bookName: string): Promise<string[]> {
  const res = await fetch(`${API_BASE}/Books/${encodeURIComponent(bookName)}/chapters`);
  if (!res.ok) throw new Error("Failed to fetch chapters");
  return res.json();
}

export async function getBookHadiths(bookName: string, chapter: string): Promise<HadithSearchResultDto[]> {
  const res = await fetch(`${API_BASE}/Books/${encodeURIComponent(bookName)}/chapters/${encodeURIComponent(chapter)}/hadiths`);
  if (!res.ok) throw new Error("Failed to fetch hadiths for chapter");
  return res.json();
}

export async function getComparativeTree(hadithIds: string[]): Promise<ComparativeTreeResponseDto> {
  const ids = hadithIds.join(',');
  const res = await fetch(`${API_BASE}/Takhreej?ids=${ids}`);
  if (!res.ok) throw new Error('Failed to fetch comparative tree');
  return res.json();
}

export async function getRelatedHadiths(hadithId: string): Promise<HadithSearchResultDto[]> {
  const res = await fetch(`${API_BASE}/Takhreej/related/${hadithId}`);
  if (!res.ok) throw new Error('Failed to fetch related hadiths');
  return res.json();
}

export async function getIlalReport(hadithIds: string[]): Promise<IlalReportDto> {
  const res = await fetch(`${API_BASE}/Ilal?ids=${hadithIds.join(',')}`);
  if (!res.ok) throw new Error('Failed to fetch ilal report');
  return res.json();
}

export async function getIlalForHadith(hadithId: string): Promise<IlalReportDto> {
  const res = await fetch(`${API_BASE}/Ilal/${hadithId}`);
  if (!res.ok) throw new Error('Failed to fetch ilal report');
  return res.json();
}

export async function explainIlal(report: IlalReportDto): Promise<IlalExplanationDto> {
  const res = await fetch(`${API_BASE}/Ilal/explain`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(report),
  });
  if (!res.ok) throw new Error('Failed to explain ilal report');
  return res.json();
}

export async function verifyHadith(text: string): Promise<HadithVerificationResultDto> {
  const res = await fetch(`${API_BASE}/Verify`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ text }),
  });
  if (!res.ok) throw new Error('Failed to verify hadith');
  return res.json();
}
