import { HadithSearchResultDto, IsnadTreeResponseDto, NarratorDetailDto, NarratorSummaryDto } from "@/types/api";

const API_BASE = "http://localhost:5147/api"; // Default ASP.NET Core dev port

export async function searchHadiths(query: string): Promise<HadithSearchResultDto[]> {
  const res = await fetch(`${API_BASE}/Search?q=${encodeURIComponent(query)}`);
  if (!res.ok) throw new Error("Failed to search hadiths");
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
