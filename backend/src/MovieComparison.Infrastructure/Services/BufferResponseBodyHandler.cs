namespace MovieComparison.Infrastructure.Services;

/// <summary>
/// Reads the response body inside the resilience pipeline. Otherwise the pipeline finishes when the
/// headers arrive, and a body that stalls after them has no timeout or retry.
/// </summary>
public sealed class BufferResponseBodyHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        try
        {
            await response.Content.LoadIntoBufferAsync(cancellationToken);
        }
        catch
        {
            response.Dispose();
            throw;
        }

        return response;
    }
}
