using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SemanticExploverApp.Models;

public sealed record SemanticNetwork
{
    public required int SchemaVersion { get; init; }
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required List<KnowledgeSource> Sources { get; init; }
    public required List<SemanticNode> Nodes { get; init; }
    public required List<RelationDefinition> Relations { get; init; }
    public required List<SemanticEdge> Edges { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}
