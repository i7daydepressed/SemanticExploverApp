using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using SemanticExploverApp.Models;
using SemanticExploverApp.Services;

namespace SemanticExploverApp.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly INetworkStore networkStore;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NetworkSummary))]
    public partial SemanticNetwork? Network { get; set; }

    [ObservableProperty]
    public partial string FileName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Откройте JSON-файл семантической сети.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanOpenNetwork))]
    public partial bool IsBusy { get; set; }

    public bool CanOpenNetwork => !IsBusy;

    public string NetworkSummary => Network is null
        ? "Сеть пока не загружена."
        : $"Загружено: узлов — {Network.Nodes.Count}, связей — {Network.Edges.Count}, типов отношений — {Network.Relations.Count}.";

    public MainViewModel() : this(new JsonNetworkStore())
    {
    }

    public MainViewModel(INetworkStore networkStore)
    {
        ArgumentNullException.ThrowIfNull(networkStore);
        this.networkStore = networkStore;
    }

    public async Task LoadNetworkAsync(Stream stream, string fileName)
    {
        StatusMessage = "Чтение файла...";

        try
        {
            SemanticNetwork loadedNetwork = await networkStore.LoadAsync(stream);

            // заменяем состояние только после успешного чтения, ошибка не стирает предыдущую сеть
            Network = loadedNetwork;
            FileName = fileName;
            StatusMessage = "Файл загружен.";
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
