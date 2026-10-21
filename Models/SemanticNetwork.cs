using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SemanticExploverApp.Models;

public sealed record SemanticNetwork { // семантическая сеть
    public required int SchemaVersion { get; init; } // версия JSON-формата; загрузчик принимает 1
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required List<KnowledgeSource> Sources { get; init; } // каталог источников для ссылок SourceRefs
    public required List<SemanticNode> Nodes { get; init; }
    public required List<RelationDefinition> Relations { get; init; } // каталог типов отношений между узлами
    public required List<SemanticEdge> Edges { get; init; } // факты сети: исходный узел → отношение → конечный узел

    [JsonExtensionData] // неизвестные поля JSON кладем в AdditionalProperties
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; } // дополнительные поля JSON: имя → значение
}
