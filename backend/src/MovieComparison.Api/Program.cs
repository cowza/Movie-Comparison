using MovieComparison.Core.Interfaces;
using MovieComparison.Infrastructure.Configuration;
using MovieComparison.Infrastructure.Services;
using Polly;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure caching
builder.Services.AddMemoryCache();

// Configure HTTP clients with Polly
builder.Services.AddHttpClient<IExternalMovieProvider, CinemaWorldProvider>()
    .AddPolicyHandler(GetRetryPolicy());

builder.Services.AddHttpClient<IExternalMovieProvider, FilmWorldProvider>()
    .AddPolicyHandler(GetRetryPolicy());

// Register services
builder.Services.AddScoped<IMovieService, MovieService>();

// Configure ExternalApiSettings
// This binds the configuration from both appsettings.json and environment variables
builder.Services.Configure<ExternalApiSettings>(
    builder.Configuration.GetSection("ExternalApiSettings"));

var apiSettings = builder.Configuration.GetSection("ExternalApiSettings").Get<ExternalApiSettings>();
if (string.IsNullOrEmpty(apiSettings?.BaseUrl) || string.IsNullOrEmpty(apiSettings?.ApiToken))
{
    throw new InvalidOperationException("ExternalApiSettings is not properly configured. Please check your configuration.");
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting();
app.MapControllers();

app.Run();

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(10); // 10 seconds timeout

    var retryPolicy = Policy<HttpResponseMessage>
        .Handle<HttpRequestException>() // Handle exceptions like network failures
        .OrResult(r => !r.IsSuccessStatusCode) // Retry on non-success HTTP status codes
        .RetryAsync(3, onRetry: (outcome, retryCount, context) =>
        {
            Console.WriteLine($"Retry {retryCount} for {context.OperationKey}");
        });

    return Policy.WrapAsync(retryPolicy, timeoutPolicy);
}
