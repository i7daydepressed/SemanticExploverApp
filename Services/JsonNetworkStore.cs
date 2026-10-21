using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SemanticExploverApp.Models;

namespace SemanticExploverApp.Services;

public sealed class JsonNetworkStore : INetworkStore { // загрузка сети из JSON
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        RespectNullableAnnotations = true
    };

    // читаем JSON и проверяем структуру сети
    public async Task<SemanticNetwork> LoadAsync(
        Stream stream,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead) {
            throw new InvalidDataException("Поток файла недоступен для чтения.");
        }

        SemanticNetwork? network = await JsonSerializer.DeserializeAsync<SemanticNetwork>(
            stream,
            SerializerOptions,
            cancellationToken).ConfigureAwait(false);

        if (network is null) {
            throw new InvalidDataException("Вместо объекта сети получено значение null.");
        }

        // проверяем форму данных, а согласованность связей затем проверяет отдельный валидатор
        EnsureReadableStructure(network);
        return network;
    }

    // проверяем версию формата и обязательные поля
    private static void EnsureReadableStructure(SemanticNetwork network) {
        if (network.SchemaVersion != 1) {
            throw new InvalidDataException(
                $"Версия формата {network.SchemaVersion} не поддерживается. Ожидается версия 1.");
        }

        RequireText(network.Id, "id");
        RequireText(network.Name, "name");

        RequireItems(network.Sources, "sources", (KnowledgeSource source, int index) => {
            RequireText(source.Id, $"sources[{index}].id");
            RequireText(source.Title, $"sources[{index}].title");
            RequireText(source.Kind, $"sources[{index}].kind");
        });

        RequireItems(network.Nodes, "nodes", (SemanticNode node, int index) => {
            RequireText(node.Id, $"nodes[{index}].id");
            RequireText(node.Label, $"nodes[{index}].label");
            RequireText(node.Kind, $"nodes[{index}].kind");
            RequireStrings(node.SourceRefs, $"nodes[{index}].sourceRefs");

            if (node.Kind != "class" && node.Kind != "instance" && node.Kind != "literal") {
                throw new InvalidDataException(
                    $"Неизвестный вид узла {node.Id}: {node.Kind}.");
            }
        });

        RequireItems(network.Relations, "relations", (RelationDefinition relation, int index) => {
            RequireText(relation.Id, $"relations[{index}].id");
            RequireText(relation.Label, $"relations[{index}].label");
            RequireStrings(relation.SourceKinds, $"relations[{index}].sourceKinds");
            RequireStrings(relation.TargetKinds, $"relations[{index}].targetKinds");
        });

        RequireItems(network.Edges, "edges", (SemanticEdge edge, int index) => {
            RequireText(edge.Id, $"edges[{index}].id");
            RequireText(edge.Source, $"edges[{index}].source");
            RequireText(edge.Relation, $"edges[{index}].relation");
            RequireText(edge.Target, $"edges[{index}].target");
            RequireStrings(edge.SourceRefs, $"edges[{index}].sourceRefs");
        });
    }

    // проверяем коллекцию и каждый ее элемент
    private static void RequireItems<T>(
        IEnumerable<T>? items,
        string location,
        Action<T, int> inspect) where T : class {
        if (items is null) {
            throw new InvalidDataException($"Массив {location} не может быть null.");
        }

        int index = 0;
        foreach (T item in items) {
            if (item is null) {
                throw new InvalidDataException($"Элемент {location}[{index}] не может быть null.");
            }

            inspect(item, index);
            index++;
        }
    }

    // проверяем список непустых строк
    private static void RequireStrings(IEnumerable<string>? values, string location) {
        RequireItems(values, location, (string value, int index) => {
            RequireText(value, $"{location}[{index}]");
        });
    }

    // отклоняем пустой текст
    private static void RequireText(string? value, string location) {
        if (string.IsNullOrWhiteSpace(value)) {
            throw new InvalidDataException($"Поле {location} должно содержать непустой текст.");
        }
    }
}
