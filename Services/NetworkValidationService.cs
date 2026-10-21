using System;
using System.Collections.Generic;
using SemanticExploverApp.Models;

namespace SemanticExploverApp.Services;

public sealed class NetworkValidationService { // проверка согласованности сети
    private static readonly HashSet<string> KnownKinds = new HashSet<string>(StringComparer.Ordinal) {
        "class", "instance", "literal"
    };

    // собираем ошибки согласованности сети
    public IReadOnlyList<NetworkValidationIssue> Validate(SemanticNetwork network) {
        ArgumentNullException.ThrowIfNull(network);

        // сюда поступает объект после структурной проверки JsonNetworkStore
        List<NetworkValidationIssue> issues = new List<NetworkValidationIssue>();
        Dictionary<string, KnowledgeSource> sources = BuildIndex(
            network.Sources, (KnowledgeSource source) => source.Id, "sources", issues);
        Dictionary<string, SemanticNode> nodes = BuildIndex(
            network.Nodes, (SemanticNode node) => node.Id, "nodes", issues);
        Dictionary<string, RelationDefinition> relations = BuildIndex(
            network.Relations, (RelationDefinition relation) => relation.Id, "relations", issues);
        BuildIndex(network.Edges, (SemanticEdge edge) => edge.Id, "edges", issues);

        for (int index = 0; index < network.Nodes.Count; index++) {
            SemanticNode node = network.Nodes[index];
            ValidateSourceRefs(node.SourceRefs, $"nodes[{index}] ({node.Id})", sources, issues);
        }

        ValidateRelations(network.Relations, issues);
        ValidateEdges(network.Edges, nodes, relations, sources, issues);
        ValidateHierarchy(network.Edges, nodes, relations, issues);
        return issues.AsReadOnly();
    }

    // строим индекс по ID и отмечаем дубликаты
    private static Dictionary<string, T> BuildIndex<T>(
        IReadOnlyList<T> items,
        Func<T, string> getId,
        string collectionName,
        List<NetworkValidationIssue> issues) {
        Dictionary<string, T> result = new Dictionary<string, T>(StringComparer.Ordinal);

        for (int index = 0; index < items.Count; index++) {
            T item = items[index];
            string id = getId(item);

            // сохраняем первое определение для дальнейших проверок, но дубликат уже делает сеть непригодной
            if (!result.TryAdd(id, item)) {
                issues.Add(new NetworkValidationIssue(
                    "duplicate_id",
                    $"{collectionName}[{index}].id",
                    $"ID «{id}» повторяется в коллекции {collectionName}."));
            }
        }

        return result;
    }

    // проверяем каталог отношений
    private static void ValidateRelations(
        IReadOnlyList<RelationDefinition> relations,
        List<NetworkValidationIssue> issues) {
        for (int index = 0; index < relations.Count; index++) {
            RelationDefinition relation = relations[index];
            string location = $"relations[{index}] ({relation.Id})";
            ValidateKinds(relation.SourceKinds, location + ".sourceKinds", issues);
            ValidateKinds(relation.TargetKinds, location + ".targetKinds", issues);

            // направление иерархии имеет смысл только между классами, даже если каталог ошибочно разрешил иные виды
            if (relation.Id == "subclass_of" &&
                (relation.SourceKinds.Count != 1 || relation.SourceKinds[0] != "class" ||
                 relation.TargetKinds.Count != 1 || relation.TargetKinds[0] != "class")) {
                issues.Add(new NetworkValidationIssue(
                    "invalid_hierarchy_relation", location,
                    "Отношение subclass_of должно соединять class → class."));
            }
        }
    }

    // проверяем допустимые виды узлов в отношении
    private static void ValidateKinds(
        IReadOnlyList<string> kinds,
        string location,
        List<NetworkValidationIssue> issues) {
        if (kinds.Count == 0) {
            issues.Add(new NetworkValidationIssue(
                "empty_relation_kinds", location,
                "Нужно указать хотя бы один допустимый вид узла."));
        }

        for (int index = 0; index < kinds.Count; index++) {
            if (!KnownKinds.Contains(kinds[index])) {
                issues.Add(new NetworkValidationIssue(
                    "unknown_node_kind", $"{location}[{index}]",
                    $"Неизвестный вид узла «{kinds[index]}». Допустимы class, instance, literal."));
            }
        }
    }

    // проверяем концы связей, отношения и источники
    private static void ValidateEdges(
        IReadOnlyList<SemanticEdge> edges,
        Dictionary<string, SemanticNode> nodes,
        Dictionary<string, RelationDefinition> relations,
        Dictionary<string, KnowledgeSource> sources,
        List<NetworkValidationIssue> issues) {
        for (int index = 0; index < edges.Count; index++) {
            SemanticEdge edge = edges[index];
            string location = $"edges[{index}] ({edge.Id})";
            ValidateSourceRefs(edge.SourceRefs, location, sources, issues);
            bool hasSource = nodes.TryGetValue(edge.Source, out SemanticNode? source);
            bool hasTarget = nodes.TryGetValue(edge.Target, out SemanticNode? target);
            bool hasRelation = relations.TryGetValue(edge.Relation, out RelationDefinition? relation);

            if (!hasSource) {
                issues.Add(new NetworkValidationIssue(
                    "unknown_source_node", location + ".source",
                    $"Узел «{edge.Source}» отсутствует."));
            }

            if (!hasTarget) {
                issues.Add(new NetworkValidationIssue(
                    "unknown_target_node", location + ".target",
                    $"Узел «{edge.Target}» отсутствует."));
            }

            if (!hasRelation) {
                issues.Add(new NetworkValidationIssue(
                    "unknown_relation", location + ".relation",
                    $"Отношение «{edge.Relation}» отсутствует в каталоге."));
            }

            // типы проверяем только у тех концов и отношений, которые удалось найти
            if (hasRelation && relation is not null) {
                if (hasSource && source is not null && !relation.SourceKinds.Contains(source.Kind)) {
                    issues.Add(new NetworkValidationIssue(
                        "invalid_source_kind", location + ".source",
                        $"Узел «{edge.Source}» имеет вид {source.Kind}, недопустимый для начала отношения «{edge.Relation}»."));
                }

                if (hasTarget && target is not null && !relation.TargetKinds.Contains(target.Kind)) {
                    issues.Add(new NetworkValidationIssue(
                        "invalid_target_kind", location + ".target",
                        $"Узел «{edge.Target}» имеет вид {target.Kind}, недопустимый для конца отношения «{edge.Relation}»."));
                }
            }
        }
    }

    // проверяем ссылки на источники знаний
    private static void ValidateSourceRefs(
        IReadOnlyList<string> sourceRefs,
        string location,
        Dictionary<string, KnowledgeSource> sources,
        List<NetworkValidationIssue> issues) {
        for (int index = 0; index < sourceRefs.Count; index++) {
            if (!sources.ContainsKey(sourceRefs[index])) {
                issues.Add(new NetworkValidationIssue(
                    "unknown_knowledge_source", $"{location}.sourceRefs[{index}]",
                    $"Источник «{sourceRefs[index]}» отсутствует в sources."));
            }
        }
    }

    // для иерархии берем дуги class → class с отношением subclass_of и строим списки родителей
    // обходим их своим стеком: path хранит текущую цепочку, а кадр — следующего родителя
    // переход к узлу этой цепочки замыкает цикл, а завершенный общий предок цикла не создает
    // при первом цикле добавляем сообщение с замкнутым путем и прекращаем поиск циклов
    // ищем первый цикл в иерархии классов
    private static void ValidateHierarchy(
        IReadOnlyList<SemanticEdge> edges,
        Dictionary<string, SemanticNode> nodes,
        Dictionary<string, RelationDefinition> relations,
        List<NetworkValidationIssue> issues) {
        if (!relations.ContainsKey("subclass_of")) {
            return;
        }

        Dictionary<string, List<string>> parents = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (SemanticNode node in nodes.Values) {
            if (node.Kind == "class") {
                parents.Add(node.Id, new List<string>());
            }
        }

        foreach (SemanticEdge edge in edges) {
            if (edge.Relation == "subclass_of" &&
                parents.ContainsKey(edge.Source) && parents.ContainsKey(edge.Target)) {
                parents[edge.Source].Add(edge.Target);
            }
        }

        // состояние 1 означает узел в текущем пути, состояние 2 — полностью пройденный узел
        Dictionary<string, int> states = new Dictionary<string, int>(StringComparer.Ordinal);
        List<string> path = new List<string>();
        Stack<(string NodeId, int NextIndex)> stack = new Stack<(string NodeId, int NextIndex)>();

        foreach (string start in parents.Keys) {
            if (states.ContainsKey(start)) {
                continue;
            }

            states[start] = 1;
            path.Add(start);
            stack.Push((start, 0));

            while (stack.Count > 0) {
                (string NodeId, int NextIndex) frame = stack.Pop();
                List<string> nextNodes = parents[frame.NodeId];

                if (frame.NextIndex >= nextNodes.Count) {
                    states[frame.NodeId] = 2;
                    path.RemoveAt(path.Count - 1);
                    continue;
                }

                // запоминаем, к какому соседу вернуться после завершения вложенного обхода
                stack.Push((frame.NodeId, frame.NextIndex + 1));
                string next = nextNodes[frame.NextIndex];

                if (states.TryGetValue(next, out int state)) {
                    if (state == 1) {
                        int cycleStart = path.IndexOf(next);
                        List<string> cycle = path.GetRange(cycleStart, path.Count - cycleStart);
                        cycle.Add(next);
                        issues.Add(new NetworkValidationIssue(
                            "hierarchy_cycle", "subclass_of",
                            $"Обнаружен цикл классов: {string.Join(" → ", cycle)}."));
                        return;
                    }

                    continue;
                }

                states[next] = 1;
                path.Add(next);
                stack.Push((next, 0));
            }
        }
    }
}
