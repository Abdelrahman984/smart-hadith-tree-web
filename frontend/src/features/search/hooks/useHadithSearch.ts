import { useQuery } from "@tanstack/react-query";
import { searchHadiths } from "@/lib/api";
import { useState, useEffect } from "react";

export function useHadithSearch(initialQuery: string = "") {
  const [query, setQuery] = useState(initialQuery);
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

  const queryResult = useQuery({
    queryKey: ["search", debouncedQuery],
    queryFn: () => searchHadiths(debouncedQuery),
    enabled: debouncedQuery.trim().length > 2,
    staleTime: 1000 * 60 * 5, // Cache for 5 minutes
  });

  return {
    query,
    setQuery,
    debouncedQuery,
    ...queryResult,
  };
}
