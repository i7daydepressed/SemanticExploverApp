using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SemanticExploverApp.Models;

namespace SemanticExploverApp.Services;

public interface INetworkStore
{
    // поток открывает вызывающая сторона и она же закрывает его после загрузки
    Task<SemanticNetwork> LoadAsync(
        Stream stream,
        CancellationToken cancellationToken = default);
}
