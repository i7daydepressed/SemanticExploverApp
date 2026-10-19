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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NetworkSummary))]
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
    public partial bool IsBusy { get; set; }

    public bool CanOpenNetwork => !IsBusy;
    public bool HasValidationErrors => !string.IsNullOrEmpty(ValidationMessage);

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
                return;
            }

            // заменяем состояние только после успешного чтения, ошибка не стирает предыдущую сеть
            Network = loadedNetwork;
            FileName = fileName;
            StatusMessage = "Файл загружен. Проверка сети пройдена.";
        }
        catch (JsonException exception)
        {
            StatusMessage = $"Некорректный JSON: {exception.Message}";
        }
        catch (InvalidDataException exception)
        {
            StatusMessage = $"Неподходящий формат сети: {exception.Message}";
        }
        catch (IOException exception)
        {
            StatusMessage = $"Не удалось прочитать файл: {exception.Message}";
        }
    }
}
