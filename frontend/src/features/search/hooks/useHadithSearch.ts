import { useQuery } from "@tanstack/react-query";
import { searchHadiths, advancedSearchHadiths } from "@/lib/api";
import { useState, useEffect } from "react";
import { SearchRequestDto } from "@/types/api";

export function useHadithSearch(
  initialQuery: string = "",
  initialScope: number = 0,
  initialMatch: number = 0
) {
  const [query, setQuery] = useState(initialQuery);
  const [scope, setScope] = useState(initialScope);
  const [match, setMatch] = useState(initialMatch);
  const [advancedRequest, setAdvancedRequest] = useState<SearchRequestDto | null>(null);

  const [prevInitialQuery, setPrevInitialQuery] = useState(initialQuery);
  const [debouncedQuery, setDebouncedQuery] = useState(initialQuery);

  // Sync state if initialQuery changes from outside without causing cascading renders in useEffect
  if (initialQuery !== prevInitialQuery) {
    setPrevInitialQuery(initialQuery);
    setQuery(initialQuery);
    setDebouncedQuery(initialQuery);
  }

  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedQuery(query);
    }, 350); // 350ms debounce
    return () => clearTimeout(timer);
  }, [query]);

  // Standard Query
  const standardQuery = useQuery({
    queryKey: ["search", debouncedQuery, scope, match],
    queryFn: () => searchHadiths(debouncedQuery, scope, match),
    enabled: !advancedRequest && debouncedQuery.trim().length > 2,
    staleTime: 1000 * 60 * 5,
  });

  // Advanced / Shamela Query
  const advancedQuery = useQuery({
    queryKey: ["advanced-search", advancedRequest],
    queryFn: () => advancedSearchHadiths(advancedRequest!),
    enabled: !!advancedRequest && (advancedRequest.phrases?.length ?? 0) > 0,
    staleTime: 1000 * 60 * 5,
  });

  const activeQueryResult = advancedRequest ? advancedQuery : standardQuery;

  return {
    query,
    setQuery,
    scope,
    setScope,
    match,
    setMatch,
    debouncedQuery,
    advancedRequest,
    setAdvancedRequest,
    ...activeQueryResult,
  };
}
