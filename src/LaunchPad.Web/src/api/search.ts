import { authedFetch } from './authedFetch';
import type { SearchResultDto } from './types';

export async function search(query: string): Promise<SearchResultDto[]> {
  const response = await authedFetch(`/api/search?q=${encodeURIComponent(query)}`);
  if (!response.ok) throw new Error(`Search failed: ${response.status}`);
  return response.json() as Promise<SearchResultDto[]>;
}
