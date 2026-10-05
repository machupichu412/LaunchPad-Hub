export class RateLimitError extends Error {
  readonly status = 429;
  readonly retryAfterSeconds: number;
  constructor(retryAfterSeconds: number) {
    super(`Request limit reached. Try again in ${retryAfterSeconds} seconds.`);
    this.name = 'RateLimitError';
    this.retryAfterSeconds = retryAfterSeconds;
  }
}

export function checkRateLimit(response: Response): Response {
  if (response.status === 429) {
    const seconds = Number(response.headers.get('Retry-After') ?? 60);
    throw new RateLimitError(Number.isFinite(seconds) && seconds > 0 ? Math.ceil(seconds) : 60);
  }
  return response;
}
