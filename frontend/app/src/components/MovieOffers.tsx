import { useQuery } from '@tanstack/react-query';
import { ApiError, fetchOffers, retryDelay, retryWhileLoading, type Offer } from '../api/client';

const formatPrice = (price: number) => `$${price.toFixed(2)}`;

const capitalize = (value: string) => value.charAt(0).toUpperCase() + value.slice(1);

const formatAge = (asOf: string) => {
    const minutes = Math.max(1, Math.round((Date.now() - new Date(asOf).getTime()) / 60000));
    return minutes < 60
        ? `${minutes} min ago`
        : `${Math.round(minutes / 60)} hr ago`;
};

const failureMessage = (error: Error | null) =>
    error instanceof ApiError && error.status === 503
        ? 'Prices are temporarily unavailable. Trying again...'
        : 'Failed to fetch prices';

const StaleLabel = ({ offer }: { offer: Offer }) =>
    offer.stale ? (
        <div className="text-xs text-amber-700">Last checked {formatAge(offer.asOf)}</div>
    ) : null;

const MovieOffers = ({ movieId }: { movieId: string }) => {
    // Keyed by movie, so a slow response for one movie can never land on another.
    const { data, error, failureReason, isPending, isRefetchError } = useQuery({
        queryKey: ['offers', movieId],
        queryFn: ({ signal }) => fetchOffers(movieId, signal),
        refetchInterval: 60_000,
        retry: retryWhileLoading,
        retryDelay,
    });

    if (isPending) {
        return failureReason
            ? <div className="text-amber-700 py-2">{failureMessage(failureReason)}</div>
            : <div className="text-center py-2">Finding best price...</div>;
    }

    if (!data) {
        return <div className="text-red-500 py-2">{failureMessage(error)}</div>;
    }

    const { bestOffer, offers, complete, unavailableProviders } = data;
    const otherOffers = offers.filter(offer => offer.provider !== bestOffer.provider);

    // Staleness is worked out on the server, so it stops updating if this page can't reach it.
    const refreshFailed = isRefetchError || failureReason !== null;

    return (
        <div className="pt-4 border-t space-y-3">
            <div className="flex justify-between items-center">
                <div className="space-y-1">
                    <div className="text-sm text-gray-500">
                        {complete && !refreshFailed ? 'Best price from' : 'Lowest known price from'}
                    </div>
                    <div className="font-medium capitalize">{bestOffer.provider}</div>
                    <StaleLabel offer={bestOffer} />
                </div>

                <div className="text-2xl font-bold">{formatPrice(bestOffer.price)}</div>
            </div>

            {otherOffers.map(offer => (
                <div key={offer.provider} className="flex justify-between items-center text-sm">
                    <div>
                        <div className="capitalize">{offer.provider}</div>
                        <StaleLabel offer={offer} />
                    </div>
                    <div>{formatPrice(offer.price)}</div>
                </div>
            ))}

            {refreshFailed ? (
                <div role="status" className="text-sm rounded bg-amber-50 text-amber-800 p-2">
                    We couldn't refresh these prices, so they may be out of date.
                </div>
            ) : !complete && (
                <div role="status" className="text-sm rounded bg-amber-50 text-amber-800 p-2">
                    We couldn't confirm current prices from {unavailableProviders.map(capitalize).join(' and ')}, so
                    this may not be the lowest price.
                </div>
            )}
        </div>
    );
};

export default MovieOffers;
