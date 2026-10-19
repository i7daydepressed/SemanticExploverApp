using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SemanticExploverApp.Models;

public sealed record RelationDefinition
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required List<string> SourceKinds { get; init; }
    public required List<string> TargetKinds { get; init; }
    public bool Transitive { get; init; }
    public bool Symmetric { get; init; }
    public string? InverseLabel { get; init; }
    public string Description { get; init; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}
