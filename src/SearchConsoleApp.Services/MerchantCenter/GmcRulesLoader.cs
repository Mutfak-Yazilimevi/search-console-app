using System.Text.Json;
using SearchConsoleApp.Core.Domain.MerchantCenter;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;

namespace SearchConsoleApp.Services.MerchantCenter;

public interface IGmcRulesLoader
{
    IReadOnlyList<GmcRuleDefinition> GetProductSpecRules();
    IReadOnlyList<GmcRuleDefinition> GetSiteRules();
}

public partial class GmcRulesLoader : IGmcRulesLoader, ISingletonService
{
    private readonly Lazy<IReadOnlyList<GmcRuleDefinition>> _productRules;
    private readonly Lazy<IReadOnlyList<GmcRuleDefinition>> _siteRules;

    public GmcRulesLoader()
    {
        _productRules = new Lazy<IReadOnlyList<GmcRuleDefinition>>(() => LoadRules("product-spec.json"));
        _siteRules = new Lazy<IReadOnlyList<GmcRuleDefinition>>(() => LoadRules("site-requirements.json"));
    }

    public IReadOnlyList<GmcRuleDefinition> GetProductSpecRules() => _productRules.Value;
    public IReadOnlyList<GmcRuleDefinition> GetSiteRules() => _siteRules.Value;

    private static IReadOnlyList<GmcRuleDefinition> LoadRules(string fileName)
    {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.Combine(baseDir, "MerchantCenter", "Rules", fileName);
        if (!File.Exists(path))
            path = Path.Combine(baseDir, "Rules", fileName);

        if (!File.Exists(path))
            return [];

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<GmcRuleDefinition>>(json, JsonOptions) ?? [];
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
