using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SemanticExploverApp.Models;

public sealed record SemanticEdge { // связь между узлами
    public required string Id { get; init; }
    public required string Source { get; init; } // ID исходного узла
    public required string Relation { get; init; } // ID типа отношения из каталога Relations
    public required string Target { get; init; } // ID конечного узла
    public List<string> SourceRefs { get; init; } = new List<string>(); // ID источников, подтверждающих эту связь
    public string? Assumption { get; init; } // модельное допущение для связи

    [JsonExtensionData] // неизвестные поля JSON кладем в AdditionalProperties
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; } // дополнительные поля JSON: имя → значение
}
