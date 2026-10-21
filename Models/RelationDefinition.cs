using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SemanticExploverApp.Models;

public sealed record RelationDefinition { // тип отношения
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required List<string> SourceKinds { get; init; }//истоник отношения
    public required List<string> TargetKinds { get; init; }// таргет
    public bool Transitive { get; init; } // транзитивность? A → B и B → C дают A → C
    public bool Symmetric { get; init; } // симметричность? A → B означает B → A
    public string? InverseLabel { get; init; } // подпись обратного отношения, например «надкласс»
    public string Description { get; init; } = string.Empty;

    [JsonExtensionData] // атрибут: неизвестные поля JSON кладем в AdditionalProperties
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; } // имя дополнительного поля → его JSON-значение
}
