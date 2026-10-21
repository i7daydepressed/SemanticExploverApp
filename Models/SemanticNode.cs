using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SemanticExploverApp.Models;

public sealed record SemanticNode { // узел сети
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string Kind { get; init; } // class, instance или literal
    public string Description { get; init; } = string.Empty;
    public List<string> SourceRefs { get; init; } = new List<string>(); // ID источников знаний, подтверждающих узел
    public string? Assumption { get; init; } // модельное допущение об узле

    [JsonExtensionData] // неизвестные поля JSON кладем в AdditionalProperties
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; } // дополнительные поля JSON: имя → значение
}
