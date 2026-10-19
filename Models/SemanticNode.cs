using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SemanticExploverApp.Models;

public sealed record SemanticNode
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string Kind { get; init; }
    public string Description { get; init; } = string.Empty;
    public List<string> SourceRefs { get; init; } = new List<string>();
    public string? Assumption { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}
