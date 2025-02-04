import { useState, useEffect } from 'react';
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

interface Movie {
    title: string;
    year: string;
    poster: string;
    id: string;
    providers: string;
}

interface BestPrice {
    provider: string;
    price: number;
}

const MovieSelector = () => {
    const [movies, setMovies] = useState<Movie[]>([]);
    const [selectedMovie, setSelectedMovie] = useState<Movie | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [priceError, setPriceError] = useState<string | null>(null);
    const [imageError, setImageError] = useState<boolean>(false);
    const [bestPrice, setBestPrice] = useState<BestPrice | null>(null);
    const [loadingPrice, setLoadingPrice] = useState(false);

    useEffect(() => {
        const fetchMovies = async () => {
            try {
                const response = await fetch('/api/movies');
                if (!response.ok) {
                    throw new Error('Failed to fetch movies');
                }
                const data: Movie[] = await response.json();
                setMovies(data);
            } catch (err) {
                setError(err instanceof Error ? err.message : 'Failed to fetch movies');
            } finally {
                setLoading(false);
            }
        };

        fetchMovies();
    }, []);

    const fetchBestPrice = async (id: string) => {
        setLoadingPrice(true);
        setBestPrice(null);

        try {
            const response = await fetch(`/api/movies/prices?id=${id}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
            });

            if (!response.ok) {
                throw new Error('Failed to fetch price');
            }

            const data: BestPrice = await response.json();
            setBestPrice(data);
        } catch (err) {
            setPriceError(err instanceof Error ? err.message : 'Failed to fetch price');
        } finally {
            setLoadingPrice(false);
        }
    };

    const handleMovieSelect = (value: string) => {
        const movie = movies.find(m => m.title === value);
        setSelectedMovie(movie || null);
        setImageError(false);

        if (movie) {
            fetchBestPrice(movie.id);
        }
    };

    if (loading) {
        return <div className="text-center p-4">Loading movies...</div>;
    }

    if (error) {
        return <div className="text-red-500 p-4">Error: {error}</div>;
    }

    return (
        <div className="w-full max-w-md mx-auto p-4">
            <Select onValueChange={handleMovieSelect}>
                <SelectTrigger className="w-full">
                    <SelectValue placeholder="Select a movie" />
                </SelectTrigger>
                <SelectContent>
                    <SelectGroup>
                        <SelectLabel>Movies</SelectLabel>
                        {movies.map((movie) => (
                            <SelectItem key={movie.title} value={movie.title}>
                                {movie.title} ({movie.year})
                            </SelectItem>
                        ))}
                    </SelectGroup>
                </SelectContent>
            </Select>

            {selectedMovie && (
                <Card className="mt-4">
                    <CardContent className="pt-4">
                        <div className="space-y-4">
                            <div className="flex justify-between items-start">
                                <div className="space-y-2">
                                    <h3 className="font-semibold">{selectedMovie.title} ({selectedMovie.year})</h3>
                                    <h3 className="font-semibold">Available on</h3>
                                    <div className="flex flex-wrap gap-2">
                                        {selectedMovie.providers.split(';').map((provider, index) => (
                                            <Badge
                                                key={index}
                                                variant="secondary"
                                                className="capitalize"
                                            >
                                                {provider}
                                            </Badge>
                                        ))}
                                    </div>
                                </div>
                                {selectedMovie.poster && !imageError && (
                                    <img
                                        src={selectedMovie.poster}
                                        alt={selectedMovie.title}
                                        className="h-32 w-auto object-contain"
                                        onError={() => setImageError(true)}
                                    />
                                )}
                            </div>

                            {loadingPrice && (
                                <div className="text-center py-2">Finding best price...</div>
                            )}

                            {priceError && (
                                <div className="text-red-500 p-4">Error: {priceError}</div>
                            )}

                            {bestPrice && (
                                <div className="pt-4 border-t">
                                    <div className="flex justify-between items-center">
                                        <div className="space-y-1">
                                            <div className="text-sm text-gray-500">Best price from</div>
                                            <div className="font-medium capitalize">{bestPrice.provider}</div>
                                        </div>

                                        <div className="text-2xl font-bold">
                                            ${bestPrice.price}
                                        </div>
                                    </div>
                                </div>
                            )}
                        </div>
                    </CardContent>
                </Card>
            )}
        </div>
    );
};

export default MovieSelector;