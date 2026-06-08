namespace SearchConsoleApp.Services.Audit;

public static class AuditUrlNormalizer
{
    public static string Normalize(string url)
    {
        var trimmed = url.Trim();
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "https://" + trimmed;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            throw new ArgumentException("Geçersiz URL.", nameof(url));

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }
}
