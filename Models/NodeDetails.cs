namespace SemanticExploverApp.Models;

public sealed record NodeDetails(
    string Title,
    string Id,
    string Kind,
    string Description,
    string RelationsText,
    string SourcesText)
{
    public static NodeDetails Empty { get; } = new NodeDetails(
        "Узел не выбран", "—", "—", "Выберите узел для просмотра сведений.",
        "Узел не выбран.", "Узел не выбран.");
}
