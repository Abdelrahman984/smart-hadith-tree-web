import { useQuery } from '@tanstack/react-query';
import { searchHadiths } from '@/lib/api';
import { useState, useEffect } from 'react';

export function useHadithSearch(initialQuery: string = "") {
  const [query, setQuery] = useState(initialQuery);
  const [debouncedQuery, setDebouncedQuery] = useState(initialQuery);

  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedQuery(query);
    }, 400); // 400ms debounce
    return () => clearTimeout(timer);
  }, [query]);

  const queryResult = useQuery({
    queryKey: ['search', debouncedQuery],
    queryFn: () => searchHadiths(debouncedQuery),
    enabled: debouncedQuery.length > 2, // Only search if > 2 chars
  });

  return {
    query,
    setQuery,
    ...queryResult
  };
}
