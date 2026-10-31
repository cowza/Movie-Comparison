namespace MovieComparison.Core.DTOs;

public sealed record MovieDto(
    string Id,
    string Title,
    string Year,
    string? Poster,
    IReadOnlyList<string> Providers);

public sealed record MovieListDto(
    IReadOnlyList<MovieDto> Movies,
    bool Complete,
    IReadOnlyList<string> UnavailableProviders);

public sealed record OfferDto(
    string Provider,
    decimal Price,
    DateTimeOffset AsOf,
    bool Stale);

/// <summary>
/// Every known offer for a movie. <see cref="Complete"/> is false when a provider could not be
/// confirmed, so <see cref="BestOffer"/> is only the best of what is known.
/// </summary>
public sealed record MovieOffersDto(
    string Id,
    string Title,
    string Year,
    string? Poster,
    OfferDto BestOffer,
    IReadOnlyList<OfferDto> Offers,
    bool Complete,
    IReadOnlyList<string> UnavailableProviders);

public enum OffersStatus
{
    Found,
    NotFound,
    Unavailable,
}

public sealed record OffersResult(OffersStatus Status, MovieOffersDto? Offers = null);

public sealed record ProviderStatus(string Provider, bool Fresh, bool HasData);
