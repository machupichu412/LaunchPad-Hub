import { RateLimitError } from './rateLimit';
import { QueryClient } from '@tanstack/react-query';

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 60_000,
      retry: (failureCount, error) => !(error instanceof RateLimitError) && failureCount < 1,
    },
  },
});
