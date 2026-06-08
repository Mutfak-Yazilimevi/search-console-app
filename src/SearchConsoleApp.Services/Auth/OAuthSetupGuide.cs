namespace SearchConsoleApp.Services.Auth;

public sealed class OAuthConfigurationException : InvalidOperationException
{
    public OAuthSetupGuide Guide { get; }

    public OAuthConfigurationException(OAuthSetupGuide guide)
        : base(guide.Summary)
    {
        Guide = guide;
    }
}

public sealed class OAuthSetupGuide
{
    public string Code { get; init; } = "oauth_config_missing";
    public string Provider { get; init; } = "";
    public string Purpose { get; init; } = "";
    public string Title { get; init; } = "";
    public string Summary { get; init; } = "";
    public IList<OAuthSetupStep> Steps { get; init; } = [];
    public IList<OAuthSetupLink> Links { get; init; } = [];
    public IList<OAuthConfigKeyHint> ConfigKeys { get; init; } = [];
    public string? RedirectUri { get; init; }
    public string? EnvFileHint { get; init; }
}

public sealed class OAuthSetupStep
{
    public int Order { get; init; }
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
}

public sealed class OAuthSetupLink
{
    public string Label { get; init; } = "";
    public string Url { get; init; } = "";
}

public sealed class OAuthConfigKeyHint
{
    public string AppsettingsKey { get; init; } = "";
    public string EnvVariable { get; init; } = "";
    public string Description { get; init; } = "";
    public bool Configured { get; init; }
}
