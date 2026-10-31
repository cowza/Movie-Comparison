import type { components } from './schema';

export type Movie = components['schemas']['MovieDto'];
export type MovieList = components['schemas']['MovieListDto'];
export type MovieOffers = components['schemas']['MovieOffersDto'];
export type Offer = components['schemas']['OfferDto'];

export class ApiError extends Error {
    constructor(
        readonly status: number,
        readonly retryAfterSeconds: number | null,
    ) {
        super(`Request failed with status ${status}`);
    }
}

const get = async <T>(path: string, signal?: AbortSignal): Promise<T> => {
    const response = await fetch(path, { signal });

    if (!response.ok) {
        const retryAfter = Number(response.headers.get('Retry-After'));
        throw new ApiError(response.status, retryAfter > 0 ? retryAfter : null);
    }

    return response.json();
};

export const fetchMovies = (signal?: AbortSignal) =>
    get<MovieList>('/api/movies', signal);

export const fetchOffers = (id: string, signal?: AbortSignal) =>
    get<MovieOffers>(`/api/movies/${encodeURIComponent(id)}/offers`, signal);

// A 503 means the snapshot hasn't loaded yet, so keep trying. Anything else won't fix itself.
export const retryWhileLoading = (failureCount: number, error: Error) =>
    error instanceof ApiError && error.status === 503 && failureCount < 20;

export const retryDelay = (_attempt: number, error: Error) =>
    error instanceof ApiError && error.retryAfterSeconds
        ? error.retryAfterSeconds * 1000
        : 5000;
