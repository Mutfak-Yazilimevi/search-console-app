using System.Text.Json;
using System.Text.Json.Serialization;

namespace SearchConsoleApp.Services.Audit;

public sealed class IssueDetailItemDto
{
    [JsonPropertyName("label")]
    public string Label { get; init; } = "";

    [JsonPropertyName("value")]
    public string Value { get; init; } = "";

    [JsonPropertyName("detail")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; init; }

    [JsonPropertyName("href")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Href { get; init; }
}

public static class IssueDetailEvidenceBuilder
{
    public static string Build(string headline, IEnumerable<IssueDetailItemDto> items, int? totalCount = null)
    {
        var list = items.ToList();
        var count = totalCount ?? list.Count;
        return JsonSerializer.Serialize(new
        {
            type = "issue-detail",
            headline,
            count,
            truncated = count > list.Count,
            items = list,
        });
    }

    public static string PageElement(string headline, string location, string found, string action)
        => Build(headline,
        [
            new() { Label = "Konum", Value = location },
            new() { Label = "Bulunan", Value = found },
            new() { Label = "Ne yapmalı", Value = action },
        ]);
}
