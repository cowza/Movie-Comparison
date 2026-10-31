import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
    Select,
    SelectContent,
    SelectGroup,
    SelectItem,
    SelectLabel,
    SelectTrigger,
    SelectValue,
} from "../components/ui/select";
import { Card, CardContent } from "../components/ui/card";
import { Badge } from "../components/ui/badge";
import { fetchMovies, retryDelay, retryWhileLoading, type Movie } from '../api/client';
import MovieOffers from './MovieOffers';

const MovieCard = ({ movie }: { movie: Movie }) => {
    const [imageError, setImageError] = useState(false);

    return (
        <Card className="mt-4">
            <CardContent className="pt-4">
                <div className="space-y-4">
                    <div className="flex justify-between items-start">
                        <div className="space-y-2">
                            <h3 className="font-semibold">{movie.title} ({movie.year})</h3>
                            <h3 className="font-semibold">Available on</h3>
                            <div className="flex flex-wrap gap-2">
                                {movie.providers.map(provider => (
                                    <Badge
                                        key={provider}
                                        variant="secondary"
                                        className="capitalize"
                                    >
                                        {provider}
                                    </Badge>
                                ))}
                            </div>
                        </div>
                        {movie.poster && !imageError && (
                            <img
                                src={movie.poster}
                                alt={movie.title}
                                className="h-32 w-auto object-contain"
                                onError={() => setImageError(true)}
                            />
                        )}
                    </div>

                    <MovieOffers movieId={movie.id} />
                </div>
            </CardContent>
        </Card>
    );
};

const MovieSelector = () => {
    const [selectedId, setSelectedId] = useState<string | null>(null);

    const { data, failureReason, isPending } = useQuery({
        queryKey: ['movies'],
        queryFn: ({ signal }) => fetchMovies(signal),
        refetchInterval: 60_000,
        retry: retryWhileLoading,
        retryDelay,
    });

    if (isPending) {
        return (
            <div className="text-center p-4">
                {failureReason ? 'Movies are loading from our providers. Trying again...' : 'Loading movies...'}
            </div>
        );
    }

    if (!data) {
        return <div className="text-red-500 p-4">Error: Failed to fetch movies</div>;
    }

    const movie = data.movies.find(m => m.id === selectedId);

    return (
        <div className="w-full max-w-md mx-auto p-4">
            <Select value={selectedId ?? undefined} onValueChange={setSelectedId}>
                <SelectTrigger className="w-full">
                    <SelectValue placeholder="Select a movie" />
                </SelectTrigger>
                <SelectContent>
                    <SelectGroup>
                        <SelectLabel>Movies</SelectLabel>
                        {data.movies.map(option => (
                            <SelectItem key={option.id} value={option.id}>
                                {option.title} ({option.year})
                            </SelectItem>
                        ))}
                    </SelectGroup>
                </SelectContent>
            </Select>

            {movie && <MovieCard key={movie.id} movie={movie} />}
        </div>
    );
};

export default MovieSelector;
