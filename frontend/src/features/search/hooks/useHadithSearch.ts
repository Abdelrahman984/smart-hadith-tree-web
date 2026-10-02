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

  const [page, setPage] = useState(1);
  const pageSize = 50;

  const [prevInitialQuery, setPrevInitialQuery] = useState(initialQuery);
  const [debouncedQuery, setDebouncedQuery] = useState(initialQuery);

  // Sync state if initialQuery changes from outside without causing cascading renders in useEffect
  if (initialQuery !== prevInitialQuery) {
    setPrevInitialQuery(initialQuery);
    setQuery(initialQuery);
    setDebouncedQuery(initialQuery);
    setPage(1);
  }

  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedQuery((prev) => {
        if (prev !== query) {
          setPage(1);
        }
        return query;
      });
    }, 350); // 350ms debounce
    return () => clearTimeout(timer);
  }, [query]);

  // Standard Query
  const standardQuery = useQuery({
    queryKey: ["search", debouncedQuery, scope, match, page, pageSize],
    queryFn: () => searchHadiths(debouncedQuery, scope, match, page, pageSize),
    enabled: !advancedRequest && debouncedQuery.trim().length > 2,
    staleTime: 1000 * 60 * 5,
  });

  // Advanced / Shamela Query
  const advancedQuery = useQuery({
    queryKey: ["advanced-search", advancedRequest, page, pageSize],
    queryFn: () => advancedSearchHadiths({ ...advancedRequest!, page, pageSize }),
    enabled:
      !!advancedRequest &&
      ((advancedRequest.phrases?.length ?? 0) > 0 ||
        (advancedRequest.andPhrases?.length ?? 0) > 0 ||
        (advancedRequest.orPhrases?.length ?? 0) > 0),
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
    page,
    setPage,
    pageSize,
    debouncedQuery,
    advancedRequest,
    setAdvancedRequest: (req: SearchRequestDto | null) => {
      setPage(1);
      setAdvancedRequest(req);
    },
    ...activeQueryResult,
  };
}
