namespace MovieComparison.Core.Models;

public sealed record ExternalMovieResponse(string ID, string Title, string Year, string? Poster);

public sealed record ExternalMoviesListResponse(List<ExternalMovieResponse>? Movies);

public sealed record ExternalMovieDetailsResponse(string? Price);
