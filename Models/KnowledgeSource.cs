using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SemanticExploverApp.Models;

public sealed record KnowledgeSource
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Kind { get; init; }
    public string Description { get; init; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}
