using SearchConsoleApp.Core.Domain.PriceBenchmark;

namespace SearchConsoleApp.Services.PriceBenchmark;

public interface IPriceBenchmarkService
{
    Task<PriceBenchmarkRun> StartAsync(string url, CancellationToken cancellationToken = default);
    Task<PriceBenchmarkDetailDto?> GetDetailAsync(Guid entityId, CancellationToken cancellationToken = default);
    Task<IList<PriceBenchmarkItemDto>> GetProductsAsync(Guid entityId, int skip, int take, CancellationToken cancellationToken = default);
    Task ProcessDiscoveredProductAsync(Guid runEntityId, PriceBenchmarkProductPayload payload, CancellationToken cancellationToken = default);
    Task ProcessDiscoverCompleteAsync(Guid runEntityId, PriceBenchmarkProductPayload payload, CancellationToken cancellationToken = default);
    Task ProcessComparedProductAsync(Guid runEntityId, PriceBenchmarkProductPayload payload, CancellationToken cancellationToken = default);
    Task ProcessProductAsync(Guid runEntityId, PriceBenchmarkProductPayload payload, CancellationToken cancellationToken = default);
    Task CompleteAsync(Guid runEntityId, PriceBenchmarkCompletePayload payload, CancellationToken cancellationToken = default);
    Task FailAsync(Guid runEntityId, string errorMessage, CancellationToken cancellationToken = default);
    Task CancelAsync(Guid runEntityId, CancellationToken cancellationToken = default);
}
