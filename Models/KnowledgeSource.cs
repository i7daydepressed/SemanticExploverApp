using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SemanticExploverApp.Models;

public sealed record KnowledgeSource { // источник знаний
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Kind { get; init; } // вид источника
    public string Description { get; init; } = string.Empty; // пояснение к источнику

    [JsonExtensionData] // неизвестные поля JSON кладем в AdditionalProperties
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; } // дополнительные поля JSON: имя → значение
}
