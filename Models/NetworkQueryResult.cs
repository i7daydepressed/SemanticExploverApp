using System;
using System.Collections.Generic;

namespace SemanticExploverApp.Models;

public sealed record NetworkQueryResult(string Summary, IReadOnlyList<QueryResultItem> Items) { // ответ запроса
    public static NetworkQueryResult NotRun { get; } = new NetworkQueryResult( // начальное состояние: запрос еще не выполнялся
        "Запрос еще не выполнен.", Array.Empty<QueryResultItem>());
}

public sealed record QueryResultItem(SemanticNode Node, string Explanation) { // строка ответа с пояснением
    public string DisplayText => $"{Node.Label} [{Node.Id}]" + Environment.NewLine + Explanation;
}
