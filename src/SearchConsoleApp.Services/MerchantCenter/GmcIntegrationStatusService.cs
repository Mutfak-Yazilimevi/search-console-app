using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Services.Audit;

namespace SearchConsoleApp.Services.MerchantCenter;

public sealed class GmcIntegrationStatusResponse
{
    public IList<IntegrationSettingsItem> Integrations { get; init; } = [];
}

public interface IGmcIntegrationStatusService
{
    GmcIntegrationStatusResponse GetStatus();
}

public class GmcIntegrationStatusService : IGmcIntegrationStatusService, IScopedService
{
    private static readonly HashSet<string> RelevantIntegrationIds =
    [
        "crawl-worker",
        "pagespeed",
        "safe-browsing",
        "gemini",
        "llm-eeat",
        "merchant-center-oauth",
    ];

    private readonly IIntegrationSettingsService _integrationSettings;

    public GmcIntegrationStatusService(IIntegrationSettingsService integrationSettings)
    {
        _integrationSettings = integrationSettings;
    }

    public GmcIntegrationStatusResponse GetStatus()
    {
        var items = _integrationSettings.GetAll().Integrations
            .Where(i => RelevantIntegrationIds.Contains(i.Id))
            .ToList();

        return new GmcIntegrationStatusResponse { Integrations = items };
    }
}
