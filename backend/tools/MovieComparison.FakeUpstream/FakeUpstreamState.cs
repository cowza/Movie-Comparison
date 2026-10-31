using System.Collections.Concurrent;

namespace MovieComparison.FakeUpstream;

public sealed record FakeMovie(string Id, string Title, string Year, string? CinemaWorldPrice, string? FilmWorldPrice);

/// <summary>Everything a test or chaos run can change about the fake, plus a record of what reached it.</summary>
public sealed class FakeUpstreamState
{
    public const string Ok = "ok";
    public const string Error = "error";
    public const string Hang = "hang";
    public const string StallBody = "stall-body";
    public const string Redirect = "redirect";
    public const string Garbage = "garbage";
    public const string Empty = "empty";
    public const string Flaky = "flaky";
    public const string PricesError = "prices-error";
    public const string PricesMissing = "prices-missing";
    public const string PricesInvalid = "prices-invalid";

    public static readonly IReadOnlyList<FakeMovie> Catalogue =
    [
        new("0076759", "Star Wars: Episode IV - A New Hope", "1977", "29.5", "20.0"),
        new("0080684", "Star Wars: Episode V - The Empire Strikes Back", "1980", "25.0", "28.5"),
        new("0086190", "Star Wars: Episode VI - Return of the Jedi", "1983", "24.0", "24.5"),
        new("0120915", "Star Wars: Episode I - The Phantom Menace", "1999", "19.0", "21.5"),
        new("0121765", "Star Wars: Episode II - Attack of the Clones", "2002", "18.5", "17.0"),
        new("0121766", "Star Wars: Episode III - Revenge of the Sith", "2005", "22.0", "23.0"),
        new("2488496", "Star Wars: The Force Awakens", "2015", "25.0", null),
    ];

    private readonly ConcurrentDictionary<string, string> _modes = new();
    private readonly ConcurrentDictionary<string, string> _prices = new();
    private readonly ConcurrentQueue<string> _requests = new();
    private int _calls;

    public string Mode(string provider) => _modes.GetValueOrDefault(provider, Ok);

    public void SetMode(string provider, string mode) => _modes[provider] = mode;

    public void SetAllModes(string mode)
    {
        SetMode("cinemaworld", mode);
        SetMode("filmworld", mode);
    }

    public void SetPrice(string provider, string id, string price) => _prices[$"{provider}/{id}"] = price;

    public string? Price(string provider, FakeMovie movie) =>
        _prices.GetValueOrDefault($"{provider}/{movie.Id}")
        ?? (provider == "cinemaworld" ? movie.CinemaWorldPrice : movie.FilmWorldPrice);

    public void Record(string path) => _requests.Enqueue(path);

    public bool ShouldFlake() => Interlocked.Increment(ref _calls) % 2 == 1;

    public IReadOnlyList<string> Requests => _requests.ToArray();

    public int Hits(string provider) => _requests.Count(path => path.StartsWith($"/api/{provider}/"));

    public void ClearRequests() => _requests.Clear();

    public void Reset()
    {
        _modes.Clear();
        _prices.Clear();
        _requests.Clear();
    }
}
