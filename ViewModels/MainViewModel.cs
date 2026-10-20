using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using SemanticExploverApp.Models;
using SemanticExploverApp.Services;

namespace SemanticExploverApp.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly INetworkStore networkStore;
    private readonly NetworkValidationService validationService;
    private readonly NetworkDetailsService detailsService = new NetworkDetailsService();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NetworkSummary))]
    [NotifyPropertyChangedFor(nameof(Nodes))]
    [NotifyPropertyChangedFor(nameof(ClassNodes))]
    [NotifyPropertyChangedFor(nameof(Relations))]
    [NotifyPropertyChangedFor(nameof(CanConfigureQuery))]
    [NotifyPropertyChangedFor(nameof(GraphPlaceholder))]
    [NotifyCanExecuteChangedFor(nameof(FindNeighborsCommand), nameof(FindHierarchyCommand), nameof(FindPathCommand))]
    public partial SemanticNetwork? Network { get; set; }

    [ObservableProperty]
    public partial string FileName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Откройте JSON-файл семантической сети.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationErrors))]
    public partial string ValidationMessage { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanOpenNetwork))]
    [NotifyPropertyChangedFor(nameof(CanConfigureQuery))]
    [NotifyCanExecuteChangedFor(nameof(FindNeighborsCommand), nameof(FindHierarchyCommand), nameof(FindPathCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial SemanticNode? SelectedNode { get; set; }

    [ObservableProperty]
    public partial NodeDetails SelectedNodeDetails { get; set; } = NodeDetails.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindNeighborsCommand), nameof(FindPathCommand))]
    public partial SemanticNode? QuerySource { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindPathCommand))]
    public partial SemanticNode? QueryTarget { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindHierarchyCommand))]
    public partial SemanticNode? HierarchyClass { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindNeighborsCommand))]
    public partial RelationDefinition? QueryRelation { get; set; }

    [ObservableProperty]
    public partial int QueryModeIndex { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindNeighborsCommand))]
    public partial int NeighborDirectionIndex { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindHierarchyCommand))]
    public partial int HierarchyDirectionIndex { get; set; }

    [ObservableProperty]
    public partial int RightTabIndex { get; set; }

    [ObservableProperty]
    public partial int NodeTabIndex { get; set; }

    public bool CanOpenNetwork => !IsBusy;
    public bool CanConfigureQuery => Network is not null && !IsBusy;
    public bool HasValidationErrors => !string.IsNullOrEmpty(ValidationMessage);
    public IReadOnlyList<SemanticNode> Nodes => Network is null
        ? Array.Empty<SemanticNode>() : Network.Nodes;
    public IReadOnlyList<SemanticNode> ClassNodes => Network is null
        ? Array.Empty<SemanticNode>()
        : Network.Nodes.Where((SemanticNode node) => node.Kind == "class").ToArray();
    public IReadOnlyList<RelationDefinition> Relations => Network is null
        ? Array.Empty<RelationDefinition>() : Network.Relations;
    public string GraphPlaceholder => Network is null
        ? "Откройте файл семантической сети."
        : "Сеть загружена. Визуализация графа пока недоступна.";

    public string NetworkSummary => Network is null
        ? "Сеть пока не загружена."
        : $"Загружено: узлов — {Network.Nodes.Count}, связей — {Network.Edges.Count}, типов отношений — {Network.Relations.Count}.";

    public MainViewModel() : this(new JsonNetworkStore())
    {
    }

    public MainViewModel(INetworkStore networkStore)
        : this(networkStore, new NetworkValidationService())
    {
    }

    public MainViewModel(INetworkStore networkStore, NetworkValidationService validationService)
    {
        ArgumentNullException.ThrowIfNull(networkStore);
        ArgumentNullException.ThrowIfNull(validationService);
        this.networkStore = networkStore;
        this.validationService = validationService;
    }

    partial void OnSelectedNodeChanged(SemanticNode? value)
    {
        SelectedNodeDetails = Network is null
            ? NodeDetails.Empty : detailsService.GetDetails(Network, value);
        if (SelectedResultItem is not null && SelectedResultItem.Node.Id != value?.Id) {
            SelectedResultItem = null;
        }
    }

    partial void OnNetworkChanged(SemanticNetwork? value)
    {
        // старые выбранные объекты не должны оставаться привязанными к новому набору данных
        SelectedNode = null;
        SelectedNodeDetails = NodeDetails.Empty;
        QuerySource = null;
        QueryTarget = null;
        HierarchyClass = null;
        QueryRelation = null;
        ResetQueryResults();
    }

    private void InitializeQueryParameters()
    {
        QuerySource = Nodes.FirstOrDefault();
        QueryTarget = Nodes.FirstOrDefault((SemanticNode node) => node.Kind == "literal")
            ?? Nodes.FirstOrDefault();
        HierarchyClass = ClassNodes.FirstOrDefault();
        QueryRelation = Relations.FirstOrDefault();
    }

    public async Task LoadNetworkAsync(Stream stream, string fileName)
    {
        StatusMessage = "Чтение файла...";
        ValidationMessage = string.Empty;

        try
        {
            SemanticNetwork loadedNetwork = await networkStore.LoadAsync(stream);
            IReadOnlyList<NetworkValidationIssue> issues = validationService.Validate(loadedNetwork);

            if (issues.Count > 0)
            {
                ValidationMessage = string.Join(
                    Environment.NewLine,
                    issues.Select((NetworkValidationIssue issue) => issue.DisplayText));
                StatusMessage = $"Файл «{fileName}» отклонен: ошибок — {issues.Count}.";
                RightTabIndex = 1;
                return;
            }

            // заменяем состояние только после успешного чтения, ошибка не стирает предыдущую сеть
            Network = loadedNetwork;
            FileName = fileName;
            InitializeQueryParameters();
            RightTabIndex = 0;
            StatusMessage = "Файл загружен. Проверка сети пройдена.";
        }
        catch (JsonException exception)
        {
            StatusMessage = $"Некорректный JSON: {exception.Message}";
            RightTabIndex = 1;
        }
        catch (InvalidDataException exception)
        {
            StatusMessage = $"Неподходящий формат сети: {exception.Message}";
            RightTabIndex = 1;
        }
        catch (IOException exception)
        {
            StatusMessage = $"Не удалось прочитать файл: {exception.Message}";
            RightTabIndex = 1;
        }
    }
}
