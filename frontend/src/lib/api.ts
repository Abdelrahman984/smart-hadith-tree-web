import {
  HadithSearchResultDto,
  IsnadTreeResponseDto,
  NarratorDetailDto,
  NarratorSummaryDto,
  ComparativeTreeResponseDto,
  SearchRequestDto,
} from "@/types/api";

const API_BASE = "http://localhost:5147/api"; // Default ASP.NET Core dev port

export async function searchHadiths(query: string, scope: number = 0, match: number = 0): Promise<HadithSearchResultDto[]> {
  const url = new URL(`${API_BASE}/Search`);
  url.searchParams.append("query", query);
  url.searchParams.append("scope", scope.toString());
  url.searchParams.append("match", match.toString());
  
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
