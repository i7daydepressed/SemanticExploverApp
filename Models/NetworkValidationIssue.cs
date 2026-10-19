namespace SemanticExploverApp.Models;

public sealed record NetworkValidationIssue(
    string Code,
    string Location,
    string Message)
{
    public string DisplayText => $"[{Code}] {Location}: {Message}";
}
