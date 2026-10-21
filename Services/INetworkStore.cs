using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SemanticExploverApp.Models;

namespace SemanticExploverApp.Services;

public interface INetworkStore { // контракт чтения сети
    // поток открывает вызывающая сторона и она же закрывает его после загрузки
    // читаем сеть из переданного потока
    Task<SemanticNetwork> LoadAsync(
        Stream stream,
        CancellationToken cancellationToken = default);
}
