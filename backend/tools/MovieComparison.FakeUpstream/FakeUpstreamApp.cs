namespace MovieComparison.FakeUpstream;

/// <summary>
/// Mimics the provider API over real HTTP, including the failures it is known for.
/// The integration tests and `make demo` both run this, so they exercise the same behaviour.
/// </summary>
public static class FakeUpstreamApp
{
    public static WebApplication Build(FakeUpstreamState state, string token, string[]? args = null)
    {
        var app = WebApplication.CreateBuilder(args ?? []).Build();

        app.MapGet("/api/{provider}/movies", async (string provider, HttpContext context) =>
        {
            state.Record(context.Request.Path);
            if (await FailureFor(state, provider, token, context, listing: true) is { } failure)
            {
                return failure;
            }

            var movies = state.Mode(provider) == FakeUpstreamState.Empty
                ? []
                : Sold(state, provider).Select(movie => new
                {
                    movie.Title,
                    movie.Year,
                    ID = Prefix(provider) + movie.Id,
                    Type = "movie",
                    Poster = (string?)null,
                }).ToArray();

            return Results.Json(new { Movies = movies }, options: null);
        });

        app.MapGet("/api/{provider}/movie/{id}", async (string provider, string id, HttpContext context) =>
        {
            state.Record(context.Request.Path);
            if (await FailureFor(state, provider, token, context, listing: false) is { } failure)
            {
                return failure;
            }

            var movie = Sold(state, provider).FirstOrDefault(m => Prefix(provider) + m.Id == id);
            if (movie is null || state.Mode(provider) == FakeUpstreamState.PricesMissing)
            {
                return Results.NotFound();
            }

            var price = state.Mode(provider) == FakeUpstreamState.PricesInvalid ? "N/A" : state.Price(provider, movie);
            return Results.Json(new { movie.Title, movie.Year, ID = id, Type = "movie", Price = price }, options: null);
        });

        app.MapPost("/__chaos/{provider}/mode/{mode}", (string provider, string mode) =>
        {
            if (provider == "all")
            {
                state.SetAllModes(mode);
            }
            else
            {
                state.SetMode(provider, mode);
            }
            return Results.NoContent();
        });
        app.MapPost("/__chaos/{provider}/price/{id}/{price}", (string provider, string id, string price) =>
        {
            state.SetPrice(provider, id, price);
            return Results.NoContent();
        });
        app.MapPost("/__chaos/reset", () =>
        {
            state.Reset();
            return Results.NoContent();
        });
        app.MapGet("/__chaos/requests", () => state.Requests);

        return app;
    }

    private static IEnumerable<FakeMovie> Sold(FakeUpstreamState state, string provider) =>
        FakeUpstreamState.Catalogue.Where(movie => state.Price(provider, movie) is not null);

    private static string Prefix(string provider) => provider == "cinemaworld" ? "cw" : "fw";

    private static async Task<IResult?> FailureFor(FakeUpstreamState state, string provider, string token, HttpContext context, bool listing)
    {
        if (context.Request.Headers["x-access-token"] != token)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        switch (state.Mode(provider))
        {
            case FakeUpstreamState.Error:
            case FakeUpstreamState.PricesError when !listing:
            case FakeUpstreamState.Flaky when state.ShouldFlake():
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            case FakeUpstreamState.Hang:
                await Task.Delay(Timeout.Infinite, context.RequestAborted);
                return Results.Empty;
            case FakeUpstreamState.Garbage:
                return Results.Text("<html>Service Unavailable</html>", "application/json");
            default:
                return null;
        }
    }
}
