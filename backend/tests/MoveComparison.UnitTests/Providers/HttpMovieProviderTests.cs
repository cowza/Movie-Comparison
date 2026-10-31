using System.Globalization;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using MovieComparison.Core.Exceptions;
using MovieComparison.Infrastructure.Configuration;
using MovieComparison.Infrastructure.Services;

namespace MoveComparison.UnitTests.Providers;

public class HttpMovieProviderTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false) { BaseAddress = new Uri("http://upstream.test") };
    }

    private static HttpMovieProvider Provider(StubHandler handler) => new(
        new ProviderSettings { Name = "filmworld", IdPrefix = "fw" },
        new StubFactory(handler),
        NullLogger<HttpMovieProvider>.Instance);

    [Fact]
    public async Task GetMovies_StripsTheProviderPrefix()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"Movies":[{"Title":"A New Hope","Year":"1977","ID":"fw0076759","Type":"movie","Poster":"http://p"}]}""");

        var movies = await Provider(handler).GetMoviesAsync(CancellationToken.None);

        var movie = Assert.Single(movies);
        Assert.Equal("0076759", movie.Id);
        Assert.Equal("http://p", movie.Poster);
        Assert.Equal("/api/filmworld/movies", handler.LastUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetMovies_SkipsMoviesWithoutTheExpectedPrefix()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"Movies":[{"Title":"A","Year":"1977","ID":"x1"},{"Title":"B","Year":"1980","ID":"fw2"}]}""");

        var movies = await Provider(handler).GetMoviesAsync(CancellationToken.None);

        Assert.Equal("2", Assert.Single(movies).Id);
    }

    [Theory]
    [InlineData("""{"Movies":[]}""")]
    [InlineData("""{"Movies":null}""")]
    [InlineData("""{}""")]
    [InlineData("""not json""")]
    public async Task GetMovies_WhenTheCatalogueIsUnusable_Throws(string body)
    {
        var provider = Provider(new StubHandler(HttpStatusCode.OK, body));

        await Assert.ThrowsAsync<ProviderException>(() => provider.GetMoviesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetMovies_WhenTheCallFails_Throws()
    {
        var provider = Provider(new StubHandler(HttpStatusCode.InternalServerError, ""));

        await Assert.ThrowsAsync<ProviderException>(() => provider.GetMoviesAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("29.5", 29.5)]
    [InlineData("1,249.50", 1249.50)]
    [InlineData("0", 0)]
    public async Task GetPrice_ParsesTheStringPrice(string price, decimal expected)
    {
        var provider = Provider(new StubHandler(HttpStatusCode.OK, $$"""{"Price":"{{price}}"}"""));

        Assert.Equal(expected, await provider.GetPriceAsync("1", CancellationToken.None));
    }

    [Fact]
    public async Task GetPrice_IgnoresTheServerCulture()
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var provider = Provider(new StubHandler(HttpStatusCode.OK, """{"Price":"29.5"}"""));

            Assert.Equal(29.5m, await provider.GetPriceAsync("1", CancellationToken.None));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("""{"Price":"N/A"}""")]
    [InlineData("""{"Price":""}""")]
    [InlineData("""{"Price":null}""")]
    [InlineData("""{"Price":"-1"}""")]
    [InlineData("""{}""")]
    public async Task GetPrice_RejectsUnusablePrices(string body)
    {
        var provider = Provider(new StubHandler(HttpStatusCode.OK, body));

        await Assert.ThrowsAsync<ProviderException>(() => provider.GetPriceAsync("1", CancellationToken.None));
    }

    [Fact]
    public async Task GetPrice_EscapesTheMovieId()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"Price":"1"}""");

        await Provider(handler).GetPriceAsync("../../movies", CancellationToken.None);

        Assert.Equal("/api/filmworld/movie/fw..%2F..%2Fmovies", handler.LastUri!.AbsolutePath);
    }
}
