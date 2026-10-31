import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import MovieOffers from './MovieOffers';
import type { MovieOffers as MovieOffersResponse, Offer } from '../api/client';

const offer = (provider: string, price: number, stale = false): Offer => ({
    provider,
    price,
    stale,
    asOf: new Date(Date.now() - 20 * 60000).toISOString(),
});

const offersFor = (best: Offer, ...others: Offer[]): MovieOffersResponse => ({
    id: '1',
    title: 'A New Hope',
    year: '1977',
    poster: null,
    bestOffer: best,
    offers: [best, ...others],
    complete: true,
    unavailableProviders: [],
});

const json = (body: unknown, status = 200, headers: Record<string, string> = {}) =>
    new Response(JSON.stringify(body), { status, headers });

const mockFetch = (handler: (url: string) => Promise<Response>) =>
    vi.stubGlobal('fetch', vi.fn((url: string) => handler(url)));

const tree = (client: QueryClient, movieId: string) => (
    <QueryClientProvider client={client}>
        <MovieOffers movieId={movieId} />
    </QueryClientProvider>
);

afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
});

describe('MovieOffers', () => {
    it('shows the best price and the other offers', async () => {
        mockFetch(async () => json(offersFor(offer('filmworld', 20), offer('cinemaworld', 29.5))));

        render(tree(new QueryClient(), '1'));

        expect(await screen.findByText('$20.00')).toBeTruthy();
        expect(screen.getByText('$29.50')).toBeTruthy();
        expect(screen.getByText('Best price from')).toBeTruthy();
        expect(screen.queryByRole('status')).toBeNull();
    });

    it('never claims a best price when a provider is unconfirmed', async () => {
        mockFetch(async () => json({
            ...offersFor(offer('cinemaworld', 30)),
            complete: false,
            unavailableProviders: ['filmworld'],
        }));

        render(tree(new QueryClient(), '1'));

        expect((await screen.findByRole('status')).textContent).toContain('Filmworld');
        expect(screen.getByText('Lowest known price from')).toBeTruthy();
        expect(screen.queryByText('Best price from')).toBeNull();
    });

    it('stops vouching for a price once a refresh fails', async () => {
        let calls = 0;
        mockFetch(async () => ++calls === 1
            ? json(offersFor(offer('filmworld', 20)))
            : json({}, 500));

        const client = new QueryClient();
        render(tree(client, '1'));
        expect(await screen.findByText('Best price from')).toBeTruthy();

        await act(async () => { await client.refetchQueries(); });

        expect((await screen.findByRole('status')).textContent).toContain('out of date');
        expect(screen.getByText('Lowest known price from')).toBeTruthy();
        expect(screen.getByText('$20.00')).toBeTruthy();
    });

    it('labels a stale offer with its age', async () => {
        mockFetch(async () => json({
            ...offersFor(offer('filmworld', 20, true), offer('cinemaworld', 30)),
            complete: false,
            unavailableProviders: ['filmworld'],
        }));

        render(tree(new QueryClient(), '1'));

        expect(await screen.findByText('Last checked 20 min ago')).toBeTruthy();
    });

    it('never shows a slow response for a previously selected movie', async () => {
        let finishFirst: (response: Response) => void = () => {};
        mockFetch(url => url.includes('/1/')
            ? new Promise(resolve => { finishFirst = resolve; })
            : Promise.resolve(json(offersFor(offer('filmworld', 11)))));

        const client = new QueryClient();
        const { rerender } = render(tree(client, '1'));
        rerender(tree(client, '2'));
        expect(await screen.findByText('$11.00')).toBeTruthy();

        await act(async () => finishFirst(json(offersFor(offer('cinemaworld', 99)))));

        expect(screen.getByText('$11.00')).toBeTruthy();
        expect(screen.queryByText('$99.00')).toBeNull();
    });

    it('clears an error when the next movie succeeds', async () => {
        mockFetch(async url => url.includes('/1/')
            ? json({}, 404)
            : json(offersFor(offer('filmworld', 11))));

        const client = new QueryClient();
        const { rerender } = render(tree(client, '1'));
        expect(await screen.findByText('Failed to fetch prices')).toBeTruthy();

        rerender(tree(client, '2'));

        expect(await screen.findByText('$11.00')).toBeTruthy();
        expect(screen.queryByText('Failed to fetch prices')).toBeNull();
    });

    it('keeps trying while prices are still loading', async () => {
        let calls = 0;
        mockFetch(async () => ++calls === 1
            ? json({}, 503, { 'Retry-After': '0.01' })
            : json(offersFor(offer('filmworld', 20))));

        render(tree(new QueryClient(), '1'));

        expect(await screen.findByText('$20.00', undefined, { timeout: 2000 })).toBeTruthy();
        expect(calls).toBe(2);
    });

    it('encodes the movie id', async () => {
        const fetchMock = vi.fn(async () => json(offersFor(offer('filmworld', 20))));
        vi.stubGlobal('fetch', fetchMock);

        render(tree(new QueryClient(), 'a/b'));
        await screen.findByText('$20.00');

        expect(fetchMock.mock.calls[0]).toContain('/api/movies/a%2Fb/offers');
    });
});
