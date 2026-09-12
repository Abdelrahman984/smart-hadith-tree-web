import { useQuery } from "@tanstack/react-query";
import { searchHadiths } from "@/lib/api";
import { useState, useEffect } from "react";

export function useHadithSearch(
  initialQuery: string = "",
  initialScope: number = 0,
  initialMatch: number = 0
) {
  const [query, setQuery] = useState(initialQuery);
  const [scope, setScope] = useState(initialScope);
  const [match, setMatch] = useState(initialMatch);

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

  // Optionally we can update the URL when query/scope/match changes, but we'll handle URL sync at the page level.

  const queryResult = useQuery({
    queryKey: ["search", debouncedQuery, scope, match],
    queryFn: () => searchHadiths(debouncedQuery, scope, match),
    enabled: debouncedQuery.trim().length > 2,
    staleTime: 1000 * 60 * 5, // Cache for 5 minutes
  });

  return {
    query,
    setQuery,
    scope,
    setScope,
    match,
    setMatch,
    debouncedQuery,
    ...queryResult,
  };
}
