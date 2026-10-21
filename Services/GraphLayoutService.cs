using System;
using System.Collections.Generic;
using System.Linq;
using SemanticExploverApp.Models;

namespace SemanticExploverApp.Services;

public sealed class GraphLayoutService { // расчет координат узлов
    private const double Margin = 100;
    private const double ColumnPitch = GraphLayout.NodeWidth + 220;
    private const double RowPitch = GraphLayout.NodeHeight + 76;

    // слабые компоненты размещаем отдельно, направление фактов сохраняет отрисовка
    public GraphLayout Build(SemanticNetwork network) {
        ArgumentNullException.ThrowIfNull(network);
        if (network.Nodes.Count == 0) {
            return GraphLayout.Empty;
        }
        Dictionary<string, SemanticNode> nodes = network.Nodes.ToDictionary(
            (SemanticNode node) => node.Id, StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> adjacent = nodes.Keys.ToDictionary(
            (string id) => id, (string _) => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (SemanticEdge edge in network.Edges) {
            adjacent[edge.Source].Add(edge.Target);
            adjacent[edge.Target].Add(edge.Source);
        }

        List<GraphLayoutNode> result = new List<GraphLayoutNode>();
        double top = Margin;
        double right = 0;
        foreach (List<string> component in FindComponents(adjacent)) {
            Dictionary<string, GraphPoint> positions = Arrange(component, network.Edges);
            double height = positions.Values.Max((GraphPoint point) => point.Y) + GraphLayout.NodeHeight;
            foreach (string id in component) {
                GraphPoint point = positions[id];
                result.Add(new GraphLayoutNode(nodes[id], new GraphRect(Margin + point.X,
                    top + point.Y, GraphLayout.NodeWidth, GraphLayout.NodeHeight)));
                right = Math.Max(right, Margin + point.X + GraphLayout.NodeWidth);
            }
            top += height + Margin * 2;
        }
        return new GraphLayout(result.AsReadOnly(), new GraphRect(0, 0, right + Margin, top - Margin));
    }

    // собираем слабые компоненты, начиная с упорядоченных ID
    private static List<List<string>> FindComponents(Dictionary<string, HashSet<string>> adjacent) {
        HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
        List<List<string>> result = new List<List<string>>();
        foreach (string start in adjacent.Keys.OrderBy((string id) => id, StringComparer.Ordinal)) {
            if (!visited.Add(start)) {
                continue;
            }
            Queue<string> queue = new Queue<string>();
            List<string> component = new List<string>();
            queue.Enqueue(start);
            while (queue.Count > 0) {
                string current = queue.Dequeue();
                component.Add(current);
                foreach (string next in adjacent[current].OrderBy((string id) => id, StringComparer.Ordinal)) {
                    if (visited.Add(next)) {
                        queue.Enqueue(next);
                    }
                }
            }
            component.Sort(StringComparer.Ordinal);
            result.Add(component);
        }
        return result.OrderByDescending((List<string> component) => component.Count)
            .ThenBy((List<string> component) => component[0], StringComparer.Ordinal).ToList();
    }

    // берем одну компоненту и реальные направления фактов, строим списки соседей
    // явный стек DFS различает активные и завершенные узлы; обратные дуги пропускаем только при расчете rank
    // истоки получают rank = 0, остальные — максимальный rank предшественника плюс один без обратных дуг
    // за 12 проходов меняем порядок строк по средним позициям соседей, сохраняя столбцы
    // возвращаем координаты всех ID; это эвристика рисунка, циклы и факты в знаниях остаются прежними
    private static Dictionary<string, GraphPoint> Arrange(List<string> ids, IReadOnlyList<SemanticEdge> edges) {
        HashSet<string> members = new HashSet<string>(ids, StringComparer.Ordinal);
        Dictionary<string, List<string>> outgoing = ids.ToDictionary((string id) => id,
            (string _) => new List<string>(), StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> neighbors = ids.ToDictionary((string id) => id,
            (string _) => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (SemanticEdge edge in edges) {
            if (edge.Source == edge.Target || !members.Contains(edge.Source) || !members.Contains(edge.Target)) {
                continue;
            }
            outgoing[edge.Source].Add(edge.Target);
            neighbors[edge.Source].Add(edge.Target);
            neighbors[edge.Target].Add(edge.Source);
        }
        foreach (string id in ids) {
            outgoing[id] = outgoing[id].Distinct(StringComparer.Ordinal)
                .OrderBy((string next) => next, StringComparer.Ordinal).ToList();
        }

        // обратные дуги исключаем только из расчета слоев; исходные факты остаются в сети
        // 0 — узел еще не посещали, 1 — он в текущем пути DFS, 2 — его исходящие дуги уже просмотрены
        Dictionary<string, int> state = ids.ToDictionary((string id) => id, (string _) => 0, StringComparer.Ordinal);
        HashSet<(string Source, string Target)> back = new HashSet<(string Source, string Target)>();
        List<string> order = new List<string>();
        foreach (string start in ids) {
            if (state[start] != 0) {
                continue;
            }
            Stack<(string Id, int Next)> stack = new Stack<(string Id, int Next)>();
            state[start] = 1;
            stack.Push((start, 0));
            while (stack.Count > 0) {
                (string id, int next) = stack.Pop();
                if (next == outgoing[id].Count) {
                    state[id] = 2;
                    order.Add(id);
                    continue;
                }
                string target = outgoing[id][next];
                stack.Push((id, next + 1));
                if (state[target] == 1) {
                    back.Add((id, target));
                }
                if (state[target] == 0) {
                    state[target] = 1;
                    stack.Push((target, 0));
                }
            }
        }
        order.Reverse();
        Dictionary<string, int> rank = ids.ToDictionary((string id) => id, (string _) => 0, StringComparer.Ordinal);
        foreach (string source in order) {
            foreach (string target in outgoing[source]) {
                if (!back.Contains((source, target))) {
                    rank[target] = Math.Max(rank[target], rank[source] + 1);
                }
            }
        }
        List<List<string>> layers = Enumerable.Range(0, rank.Values.Max() + 1)
            .Select((int level) => ids.Where((string id) => rank[id] == level).ToList()).ToList();

        // чередуем проходы по слоям, сближая строки связанных узлов без перестановки столбцов
        // номера строк считаем от середины слоя, чтобы сравнивать соседей из столбцов разной высоты
        Dictionary<string, double> row = new Dictionary<string, double>(StringComparer.Ordinal);
        for (int iteration = 0; iteration < 12; iteration++) {
            foreach (List<string> layer in layers) {
                for (int index = 0; index < layer.Count; index++) {
                    row[layer[index]] = index - (layer.Count - 1) / 2.0;
                }
            }
            IEnumerable<int> levels = iteration % 2 == 0
                ? Enumerable.Range(0, layers.Count) : Enumerable.Range(0, layers.Count).Reverse();
            foreach (int level in levels) {
                Dictionary<string, double> scores = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (string id in layers[level]) {
                    string[] related = neighbors[id].Where((string other) => iteration % 2 == 0
                        ? rank[other] < level : rank[other] > level).ToArray();
                    scores[id] = related.Length == 0 ? row[id] : related.Average((string other) => row[other]);
                }
                layers[level] = layers[level].OrderBy((string id) => scores[id])
                    .ThenBy((string id) => row[id]).ThenBy((string id) => id, StringComparer.Ordinal).ToList();
                for (int index = 0; index < layers[level].Count; index++) {
                    row[layers[level][index]] = index - (layers[level].Count - 1) / 2.0;
                }
            }
        }
        int maxRows = layers.Max((List<string> layer) => layer.Count);
        Dictionary<string, GraphPoint> result = new Dictionary<string, GraphPoint>(StringComparer.Ordinal);
        for (int level = 0; level < layers.Count; level++) {
            for (int index = 0; index < layers[level].Count; index++) {
                result[layers[level][index]] = new GraphPoint(level * ColumnPitch,
                    (index + (maxRows - layers[level].Count) / 2.0) * RowPitch);
            }
        }
        return result;
    }
}
