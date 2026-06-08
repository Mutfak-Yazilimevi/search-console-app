using System.Net;

namespace SearchConsoleApp.IntegrationTests.Infrastructure;

/// <summary>
/// Crawl worker /enqueue ve /cancel isteklerine 200 döner; gerçek worker gerekmez.
/// </summary>
public sealed class FakeCrawlWorkerHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? "";
        if (path.EndsWith("/enqueue", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/cancel", StringComparison.OrdinalIgnoreCase)
            || path.Contains("enqueue-product", StringComparison.OrdinalIgnoreCase)
            || path.Contains("enqueue-product-rescan", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
