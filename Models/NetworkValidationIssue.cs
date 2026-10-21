namespace SemanticExploverApp.Models;

public sealed record NetworkValidationIssue(// структура ошибки проверки сети
    string Code, // код вида ошибки, например duplicate_id
    string Location, // поле, элемент или раздел сети с ошибкой
    string Message) {
    public string DisplayText => $"[{Code}] {Location}: {Message}";
}
