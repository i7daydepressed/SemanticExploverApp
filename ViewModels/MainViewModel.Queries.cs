using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SemanticExploverApp.Models;
using SemanticExploverApp.Services;

namespace SemanticExploverApp.ViewModels;

public partial class MainViewModel {
    private readonly NetworkQueryService queryService = new NetworkQueryService();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QueryResultText))]
    public partial NetworkQueryResult QueryResult { get; set; } = NetworkQueryResult.NotRun;

    [ObservableProperty]
    public partial QueryResultItem? SelectedResultItem { get; set; }

    public string QueryResultText => QueryResult.Summary;

    public ObservableCollection<QueryResultItem> QueryResultItems { get; } = new ObservableCollection<QueryResultItem>();

    partial void OnQueryResultChanged(NetworkQueryResult value) {
        SelectedResultItem = null;
        // список остается тем же объектом, Clear сообщает интерфейсу об удалении старых строк
        QueryResultItems.Clear();
        foreach (QueryResultItem item in value.Items) {
            QueryResultItems.Add(item);
        }
    }

    private bool CanFindNeighbors() => CanConfigureQuery && QuerySource is not null &&
        QueryRelation is not null && NeighborDirectionIndex is 0 or 1;

    private bool CanFindHierarchy() => CanConfigureQuery && HierarchyClass is not null &&
        HierarchyClass.Kind == "class" && HierarchyDirectionIndex is 0 or 1;

    private bool CanFindPath() => CanConfigureQuery && QuerySource is not null && QueryTarget is not null;

    [RelayCommand(CanExecute = nameof(CanFindNeighbors))]
    private void FindNeighbors() {
        if (!CanFindNeighbors() || Network is null || QuerySource is null || QueryRelation is null) {
            return;
        }

        ShowQueryResult(queryService.GetNeighbors(
            Network, QuerySource.Id, QueryRelation.Id, incoming: NeighborDirectionIndex == 1));
    }

    [RelayCommand(CanExecute = nameof(CanFindHierarchy))]
    private void FindHierarchy() {
        if (!CanFindHierarchy() || Network is null || HierarchyClass is null) {
            return;
        }

        ShowQueryResult(queryService.GetHierarchy(
            Network, HierarchyClass.Id, descendants: HierarchyDirectionIndex == 1));
    }

    [RelayCommand(CanExecute = nameof(CanFindPath))]
    private void FindPath() {
        if (!CanFindPath() || Network is null || QuerySource is null || QueryTarget is null) {
            return;
        }

        ShowQueryResult(queryService.FindPath(Network, QuerySource.Id, QueryTarget.Id));
    }

    private void ShowQueryResult(NetworkQueryResult result) {
        SelectedResultItem = null;
        QueryResult = result;
        RightTabIndex = 0;
    }

    private void ResetQueryResults() {
        SelectedResultItem = null;
        QueryResult = NetworkQueryResult.NotRun;
    }

    partial void OnSelectedResultItemChanged(QueryResultItem? value) {
        if (value is not null) {
            // строка результата хранит настоящий узел, нижняя панель использует обычный путь выбора
            SelectedNode = value.Node;
        }
    }
}
