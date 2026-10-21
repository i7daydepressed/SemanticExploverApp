namespace SemanticExploverApp.Models;

public sealed record NodeDetails(// ентити узла
    string Title,
    string Id,
    string Kind, // вид узла для показа в интерфейсе
    string Description,
    string RelationsText, // текст прямых входящих и исходящих связей
    string SourcesText) { // источники узла и его модельное допущение
    public static NodeDetails Empty { get; } = new NodeDetails( // состояние без выбранного узла
        "Узел не выбран", "—", "—", "Выберите узел для просмотра сведений.",
        "Узел не выбран.", "Узел не выбран.");
}
