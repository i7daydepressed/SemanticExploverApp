using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SemanticExploverApp.Models;

public sealed record SemanticEdge
{
    public required string Id { get; init; }
    public required string Source { get; init; }
    public required string Relation { get; init; }
    public required string Target { get; init; }
    public List<string> SourceRefs { get; init; } = new List<string>();
    public string? Assumption { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}
