using System;
using System.Collections.Generic;

namespace SemanticExploverApp.Models;

public sealed record NetworkQueryResult(string Summary, IReadOnlyList<QueryResultItem> Items) {
    public static NetworkQueryResult NotRun { get; } = new NetworkQueryResult(
        "Запрос еще не выполнен.", Array.Empty<QueryResultItem>());
}

public sealed record QueryResultItem(SemanticNode Node, string Explanation) {
    public string DisplayText => $"{Node.Label} [{Node.Id}]" + Environment.NewLine + Explanation;
}
