using System;
using System.Collections.Generic;
using System.Linq;
using SemanticExploverApp.Models;

namespace SemanticExploverApp.Services;

public sealed class NetworkQueryService { // поиск по сети
    // ищем соседей по отношению и направлению
    public NetworkQueryResult GetNeighbors(
        SemanticNetwork network, string nodeId, string relationId, bool incoming = false) {
        QueryGraph graph = new QueryGraph(network);
        SemanticNode start = graph.RequireNode(nodeId);
        if (!graph.Relations.TryGetValue(relationId, out RelationDefinition? relation)) {
            throw new ArgumentException($"Неизвестное отношение: {relationId}", nameof(relationId));
        }

        List<SemanticEdge> edges = incoming ? graph.Incoming[nodeId] : graph.Outgoing[nodeId];
        List<QueryResultItem> items = new List<QueryResultItem>();
        // один сосед может быть подтвержден несколькими фактами, ни один из них не теряем
        IEnumerable<IGrouping<string, SemanticEdge>> groups = edges
            .Where((SemanticEdge edge) => edge.Relation == relationId)
            .GroupBy((SemanticEdge edge) => incoming ? edge.Source : edge.Target, StringComparer.Ordinal);
        foreach (IGrouping<string, SemanticEdge> group in groups) {
            string explanation = string.Join(Environment.NewLine, group.Select(graph.FormatFact));
            items.Add(new QueryResultItem(graph.Nodes[group.Key], explanation));
        }

        string direction = incoming ? "Входящие" : "Исходящие";
        string summary = $"{direction} соседи узла «{start.Label}» [{start.Id}], " +
            $"отношение «{relation.Label}» [{relation.Id}]: {items.Count}.";
        return new NetworkQueryResult(summary, items.AsReadOnly());
    }

    // ищем предков или потомков класса по subclass_of
    public NetworkQueryResult GetHierarchy(SemanticNetwork network, string classId, bool descendants = false) {
        QueryGraph graph = new QueryGraph(network);
        SemanticNode start = graph.RequireNode(classId);
        if (start.Kind != "class") {
            throw new ArgumentException("Для поиска по иерархии выберите класс.", nameof(classId));
        }

        // берем проверенный класс и обходим subclass_of по уровням: к родителям или против дуг к потомкам
        // visited хранит уже найденные классы вместе с исходным, поэтому ромб не дает повторов
        // previous запоминает первую дугу к каждому классу: из нее собираем один кратчайший маршрут
        // факты в ответе сохраняют направление из JSON, даже когда ищем потомков против дуг
        Dictionary<string, List<SemanticEdge>> adjacency = descendants ? graph.Incoming : graph.Outgoing;
        Queue<string> queue = new Queue<string>();
        HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal) { classId };
        Dictionary<string, SemanticEdge> previous = new Dictionary<string, SemanticEdge>(StringComparer.Ordinal);
        List<QueryResultItem> items = new List<QueryResultItem>();
        queue.Enqueue(classId);
        while (queue.Count > 0) {
            string current = queue.Dequeue();
            foreach (SemanticEdge edge in adjacency[current]) {
                if (edge.Relation != "subclass_of") {
                    continue;
                }

                string next = descendants ? edge.Source : edge.Target;
                if (!visited.Add(next)) {
                    continue;
                }

                previous.Add(next, edge);
                queue.Enqueue(next);
                List<SemanticEdge> witness = BuildWitness(classId, next, previous, incoming: descendants);
                string explanation = $"Уровней от исходного класса: {witness.Count}. Опорные факты:" +
                    Environment.NewLine + string.Join(Environment.NewLine, witness.Select(graph.FormatFact));
                items.Add(new QueryResultItem(graph.Nodes[next], explanation));
            }
        }

        string direction = descendants ? "Потомки" : "Предки";
        return new NetworkQueryResult(
            $"{direction} класса «{start.Label}» [{classId}]: {items.Count}. Поиск только по subclass_of.",
            items.AsReadOnly());
    }

    // ищем кратчайший путь по направлению дуг
    public NetworkQueryResult FindPath(SemanticNetwork network, string sourceId, string targetId) {
        QueryGraph graph = new QueryGraph(network);
        SemanticNode source = graph.RequireNode(sourceId);
        SemanticNode target = graph.RequireNode(targetId);
        // списки исходящих дуг сохраняют порядок Edges, очередь проходит узлы по числу переходов
        // visited содержит все найденные узлы, previous — первую дугу, которой пришли в каждый из них
        // так цикл не зацикливает поиск, а равные по длине пути выбираются по порядку исходных дуг
        // недостижимая цель дает пустой ответ, совпавшие начало и конец — один узел без дуг
        Queue<string> queue = new Queue<string>();
        HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal) { sourceId };
        Dictionary<string, SemanticEdge> previous = new Dictionary<string, SemanticEdge>(StringComparer.Ordinal);
        queue.Enqueue(sourceId);

        // очередь обходит узлы по числу переходов, первый найденный путь будет кратчайшим
        while (queue.Count > 0 && !visited.Contains(targetId)) {
            string current = queue.Dequeue();
            foreach (SemanticEdge edge in graph.Outgoing[current]) {
                if (!visited.Add(edge.Target)) {
                    continue;
                }

                previous.Add(edge.Target, edge);
                queue.Enqueue(edge.Target);
                if (edge.Target == targetId) {
                    break;
                }
            }
        }

        string endpoints = $"«{source.Label}» [{sourceId}] → «{target.Label}» [{targetId}]";
        if (!visited.Contains(targetId)) {
            return new NetworkQueryResult(
                $"Направленный путь {endpoints} не найден.", Array.Empty<QueryResultItem>());
        }

        List<SemanticEdge> path = BuildWitness(sourceId, targetId, previous, incoming: false);
        List<QueryResultItem> items = new List<QueryResultItem> {
            new QueryResultItem(source, "Начало пути.")
        };
        foreach (SemanticEdge edge in path) {
            items.Add(new QueryResultItem(graph.Nodes[edge.Target], graph.FormatFact(edge)));
        }

        return new NetworkQueryResult(
            $"Путь {endpoints}: связей — {path.Count}. Только по направлению записанных дуг." +
            Environment.NewLine + "Цепочка описывает связи понятий и не определяет текущее состояние устройства.",
            items.AsReadOnly());
    }

    // идем по previous от конца к началу и затем разворачиваем список фактов
    // для потомков возвращаемся по Target, сами дуги не переворачиваем
    // на исходном узле останавливаемся: для пути к себе список сразу остается пустым
    // восстанавливаем цепочку фактов из previous
    private static List<SemanticEdge> BuildWitness(
        string startId, string endId, Dictionary<string, SemanticEdge> previous, bool incoming) {
        List<SemanticEdge> edges = new List<SemanticEdge>();
        string current = endId;
        while (current != startId) {
            SemanticEdge edge = previous[current];
            edges.Add(edge);
            current = incoming ? edge.Target : edge.Source;
        }

        edges.Reverse();
        return edges;
    }

    private sealed class QueryGraph { // индексы сети для запроса
        public Dictionary<string, SemanticNode> Nodes { get; }
        public Dictionary<string, RelationDefinition> Relations { get; }
        public Dictionary<string, List<SemanticEdge>> Outgoing { get; }
        public Dictionary<string, List<SemanticEdge>> Incoming { get; }
        private readonly Dictionary<string, KnowledgeSource> sources;

        // строим индексы узлов, отношений и дуг
        public QueryGraph(SemanticNetwork network) {
            ArgumentNullException.ThrowIfNull(network);
            // сервис получает сеть после валидатора, ссылки и ID в ней уже проверены
            Nodes = network.Nodes.ToDictionary((SemanticNode node) => node.Id, StringComparer.Ordinal);
            Relations = network.Relations.ToDictionary((RelationDefinition relation) => relation.Id, StringComparer.Ordinal);
            sources = network.Sources.ToDictionary((KnowledgeSource source) => source.Id, StringComparer.Ordinal);
            Outgoing = new Dictionary<string, List<SemanticEdge>>(StringComparer.Ordinal);
            Incoming = new Dictionary<string, List<SemanticEdge>>(StringComparer.Ordinal);
            foreach (SemanticNode node in network.Nodes) {
                Outgoing.Add(node.Id, new List<SemanticEdge>());
                Incoming.Add(node.Id, new List<SemanticEdge>());
            }

            foreach (SemanticEdge edge in network.Edges) {
                Outgoing[edge.Source].Add(edge);
                Incoming[edge.Target].Add(edge);
            }
        }

        // получаем узел по ID или сообщаем об ошибке
        public SemanticNode RequireNode(string nodeId) {
            if (!Nodes.TryGetValue(nodeId, out SemanticNode? node)) {
                throw new ArgumentException($"Неизвестный узел: {nodeId}", nameof(nodeId));
            }

            return node;
        }

        // собираем текст факта с его источниками и допущением
        public string FormatFact(SemanticEdge edge) {
            string fact = $"{edge.Id}: {Nodes[edge.Source].Label} [{edge.Source}] — " +
                $"{Relations[edge.Relation].Label} [{edge.Relation}] → {Nodes[edge.Target].Label} [{edge.Target}]";
            if (edge.SourceRefs.Count > 0) {
                fact += Environment.NewLine + "Источники факта: " + string.Join("; ",
                    edge.SourceRefs.Select((string id) => $"{id}: {sources[id].Title}"));
            }

            if (!string.IsNullOrWhiteSpace(edge.Assumption)) {
                fact += Environment.NewLine + "Модельное допущение: " + edge.Assumption;
            }

            return fact;
        }
    }
}
