using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using MovieComparison.Core.DTOs;
using MovieComparison.Core.Interfaces;
using MovieComparison.Infrastructure.Configuration;

namespace MovieComparison.Api.Endpoints;

public static class MovieEndpoints
{
    public const string RateLimitPolicy = "api";

    public static void MapMovieEndpoints(this IEndpointRouteBuilder app)
    {
        var movies = app.MapGroup("/api/movies")
            .WithTags("Movies")
            .RequireRateLimiting(RateLimitPolicy);

        movies.MapGet("/", GetMovies)
            .WithName("GetMovies")
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        movies.MapGet("/{id}/offers", GetOffers)
            .WithName("GetMovieOffers")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

    private static Results<Ok<MovieListDto>, ProblemHttpResult> GetMovies(
        IMovieService movieService, HttpContext context, IOptions<SnapshotSettings> settings)
    {
        var movies = movieService.GetMovies();
        return movies is null ? Unavailable(context, settings.Value) : TypedResults.Ok(movies);
    }

    private static Results<Ok<MovieOffersDto>, ProblemHttpResult> GetOffers(
        string id, IMovieService movieService, HttpContext context, IOptions<SnapshotSettings> settings)
    {
        var result = movieService.GetOffers(id);
        return result.Status switch
        {
            OffersStatus.Found => TypedResults.Ok(result.Offers!),
            OffersStatus.NotFound => TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Movie not found"),
            _ => Unavailable(context, settings.Value),
        };
    }

    private static ProblemHttpResult Unavailable(HttpContext context, SnapshotSettings settings)
    {
        context.Response.Headers.RetryAfter = ((int)settings.FailureRetryInterval.TotalSeconds).ToString();
        return TypedResults.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Prices are temporarily unavailable");
    }
}
