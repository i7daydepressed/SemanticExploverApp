using System;
using System.Collections.Generic;
using System.Linq;
using SemanticExploverApp.Models;

namespace SemanticExploverApp.Services;

// маршруты относятся к рисунку, факты и направления в сети не меняются
public sealed class GraphRoutingService { // маршруты стрелок вокруг карточек
    private const double Clearance = 24;
    private sealed record Dock(string EdgeId, bool Source, string NodeId, int Side, double Order); // конец факта на стороне карточки
    private readonly record struct Port(GraphPoint Border, GraphPoint Escape, int Direction); // граница, выход в канал и ось

    // берем валидную сеть и готовые карточки; каждому концу факта назначаем отдельный порт
    // Side: 0 справа, 1 слева, 2 снизу, 3 сверху; Escape вынесен на 24 единицы за границу
    // строим общие каналы, затем ищем пути от коротких к длинным, учитывая занятые участки
    // убираем повторы и лишние точки на прямой, возвращаем точки от настоящего Source до Target по ID факта
    // карточки не меняем; общие участки и пересечения между линиями полностью не исключаются
    public IReadOnlyDictionary<string, IReadOnlyList<GraphPoint>> Build(SemanticNetwork network, GraphLayout layout) {
        Dictionary<string, IReadOnlyList<GraphPoint>> result = new Dictionary<string, IReadOnlyList<GraphPoint>>(StringComparer.Ordinal);
        if (network.Edges.Count == 0) {
            return result;
        }
        Dictionary<string, GraphRect> nodes = layout.Nodes.ToDictionary(
            (GraphLayoutNode node) => node.Node.Id, (GraphLayoutNode node) => node.Bounds, StringComparer.Ordinal);
        List<Dock> docks = new List<Dock>();
        foreach (SemanticEdge edge in network.Edges.OrderBy((SemanticEdge edge) => edge.Id, StringComparer.Ordinal)) {
            GraphRect source = nodes[edge.Source];
            GraphRect target = nodes[edge.Target];
            int side = source.X != target.X ? (target.X > source.X ? 0 : 1) : (target.Y > source.Y ? 2 : 3);
            docks.Add(new Dock(edge.Id, true, edge.Source, edge.Source == edge.Target ? 0 : side,
                side < 2 ? target.Center.Y : target.Center.X));
            docks.Add(new Dock(edge.Id, false, edge.Target, edge.Source == edge.Target ? 3 : side ^ 1,
                side < 2 ? source.Center.Y : source.Center.X));
        }
        Dictionary<(string EdgeId, bool Source), Port> ports = new Dictionary<(string EdgeId, bool Source), Port>();
        // делим сторону карточки между концами фактов, чтобы параллельные связи не получили один порт
        foreach (IGrouping<(string NodeId, int Side), Dock> group in docks.GroupBy((Dock dock) => (dock.NodeId, dock.Side))) {
            Dock[] ordered = group.OrderBy((Dock dock) => dock.Order).ThenBy((Dock dock) => dock.EdgeId, StringComparer.Ordinal)
                .ThenBy((Dock dock) => dock.Source).ToArray();
            GraphRect rect = nodes[group.Key.NodeId];
            for (int index = 0; index < ordered.Length; index++) {
                double ratio = (index + 1.0) / (ordered.Length + 1);
                double x = Math.Round(rect.X + 12 + (rect.Width - 24) * ratio, 3);
                double y = Math.Round(rect.Y + 8 + (rect.Height - 16) * ratio, 3);
                Port port = group.Key.Side switch {
                    0 => new Port(new GraphPoint(rect.Right, y), new GraphPoint(rect.Right + Clearance, y), 0),
                    1 => new Port(new GraphPoint(rect.X, y), new GraphPoint(rect.X - Clearance, y), 0),
                    2 => new Port(new GraphPoint(x, rect.Bottom), new GraphPoint(x, rect.Bottom + Clearance), 1),
                    _ => new Port(new GraphPoint(x, rect.Y), new GraphPoint(x, rect.Y - Clearance), 1)
                };
                ports.Add((ordered[index].EdgeId, ordered[index].Source), port);
            }
        }

        // общая сетка каналов позволяет учитывать уже занятые участки и обходить карточки
        SortedSet<double> xs = new SortedSet<double>();
        SortedSet<double> ys = new SortedSet<double>();
        foreach (GraphRect rect in nodes.Values) {
            xs.UnionWith(new double[] { rect.X - Clearance, rect.Right + Clearance, rect.Center.X });
            ys.UnionWith(new double[] { rect.Y - Clearance, rect.Bottom + Clearance, rect.Center.Y });
        }
        foreach (Port port in ports.Values) {
            xs.Add(port.Escape.X);
            ys.Add(port.Escape.Y);
        }
        // дополнительные каналы уменьшают слияние линий между соседними столбцами
        double[] columns = nodes.Values.Select((GraphRect rect) => rect.X).Distinct().Order().ToArray();
        for (int index = 1; index < columns.Length; index++) {
            double left = columns[index - 1] + GraphLayout.NodeWidth + Clearance;
            double right = columns[index] - Clearance;
            for (double x = left + Clearance; x < right; x += Clearance) {
                xs.Add(x);
            }
        }
        Grid grid = new Grid(xs.ToArray(), ys.ToArray(), nodes.Values.ToArray());
        foreach (SemanticEdge edge in network.Edges.OrderBy((SemanticEdge edge) =>
            Distance(ports[(edge.Id, true)].Escape, ports[(edge.Id, false)].Escape))
            .ThenBy((SemanticEdge edge) => edge.Id, StringComparer.Ordinal)) {
            Port source = ports[(edge.Id, true)];
            Port target = ports[(edge.Id, false)];
            List<GraphPoint> points = new List<GraphPoint> { source.Border };
            // сетка ищет обход между выходами в каналы; точки на самих границах добавляем отдельно
            points.AddRange(grid.Route(source.Escape, target.Escape, source.Direction));
            points.Add(target.Border);
            List<GraphPoint> simplified = new List<GraphPoint>();
            foreach (GraphPoint point in points) {
                if (simplified.Count > 0 && simplified[^1] == point) {
                    continue;
                }
                while (simplified.Count > 1 && Collinear(simplified[^2], simplified[^1], point)) {
                    simplified.RemoveAt(simplified.Count - 1);
                }
                simplified.Add(point);
            }
            result.Add(edge.Id, simplified.AsReadOnly());
        }
        return result;
    }

    // считаем манхэттенскую длину прямоугольного пути
    private static double Distance(GraphPoint a, GraphPoint b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    // проверяем продолжение прямого участка без разворота назад
    private static bool Collinear(GraphPoint a, GraphPoint b, GraphPoint c) =>
        (a.X == b.X && b.X == c.X && (b.Y - a.Y) * (c.Y - b.Y) >= 0) ||
        (a.Y == b.Y && b.Y == c.Y && (b.X - a.X) * (c.X - b.X) >= 0);

    private sealed class Grid { // свободные каналы и поиск пути
        private readonly double[] xs;
        private readonly double[] ys;
        private readonly bool[] horizontal;
        private readonly bool[] vertical;
        // число уже проведенных маршрутов на каждом участке общей сетки
        private readonly Dictionary<(int A, int B), int> usage = new Dictionary<(int A, int B), int>();
        private int Width => xs.Length;
        private int Count => xs.Length * ys.Length;

        // заранее отмечаем горизонтальные и вертикальные переходы, не задевающие карточки
        public Grid(double[] x, double[] y, GraphRect[] obstacles) {
            xs = x;
            ys = y;
            horizontal = new bool[Count];
            vertical = new bool[Count];
            for (int row = 0; row < ys.Length; row++) {
                for (int column = 0; column < xs.Length; column++) {
                    int index = row * Width + column;
                    if (column + 1 < Width) {
                        horizontal[index] = Clear(Point(index), Point(index + 1), obstacles);
                    }
                    if (row + 1 < ys.Length) {
                        vertical[index] = Clear(Point(index), Point(index + Width), obstacles);
                    }
                }
            }
        }

        // A* хранит направление входа в вершину, чтобы учитывать стоимость поворота
        // состояние — вершина * 2 + ось входа: 0 по горизонтали, 1 по вертикали
        // cost хранит лучшую цену, previous — предыдущее состояние, closed — уже обработанные состояния
        // цена перехода учитывает длину, использование канала и поворот, очередь добавляет нижнюю оценку до finish
        // дойдя до finish, восстанавливаем путь назад и обновляем usage для следующих фактов
        // если свободного пути нет, сообщаем об ошибке вместо линии через карточку
        public List<GraphPoint> Route(GraphPoint start, GraphPoint finish, int direction) {
            int first = Array.BinarySearch(ys, start.Y) * Width + Array.BinarySearch(xs, start.X);
            int last = Array.BinarySearch(ys, finish.Y) * Width + Array.BinarySearch(xs, finish.X);
            double[] cost = new double[Count * 2];
            Array.Fill(cost, double.PositiveInfinity);
            int[] previous = new int[Count * 2];
            Array.Fill(previous, -1);
            // при равной оценке сравниваем номер состояния, чтобы одинаковые варианты выбирались повторяемо
            PriorityQueue<int, (double Cost, int State)> queue = new PriorityQueue<int, (double Cost, int State)>();
            int initial = first * 2 + direction;
            cost[initial] = 0;
            queue.Enqueue(initial, (Distance(start, finish), initial));
            int end = -1;
            bool[] closed = new bool[Count * 2];
            while (queue.Count > 0) {
                int current = queue.Dequeue();
                if (closed[current]) {
                    continue;
                }
                closed[current] = true;
                int vertex = current / 2;
                if (vertex == last) { end = current; break; }
                int column = vertex % Width;
                int row = vertex / Width;
                if (column + 1 < Width && horizontal[vertex]) {
                    Visit(vertex + 1, 0);
                }
                if (column > 0 && horizontal[vertex - 1]) {
                    Visit(vertex - 1, 0);
                }
                if (row + 1 < ys.Length && vertical[vertex]) {
                    Visit(vertex + Width, 1);
                }
                if (row > 0 && vertical[vertex - Width]) {
                    Visit(vertex - Width, 1);
                }

                // улучшаем цену соседнего состояния и запоминаем переход для восстановления пути
                void Visit(int next, int axis) {
                    int state = next * 2 + axis;
                    (int A, int B) segment = (Math.Min(vertex, next), Math.Max(vertex, next));
                    usage.TryGetValue(segment, out int occupied);
                    double length = Distance(Point(vertex), Point(next));
                    double candidate = cost[current] + length * (1 + occupied * 2) + (current % 2 == axis ? 0 : 36);
                    if (candidate >= cost[state]) {
                        return;
                    }
                    cost[state] = candidate;
                    previous[state] = current;
                    queue.Enqueue(state, (candidate + Distance(Point(next), finish), state));
                }
            }
            if (end < 0) {
                throw new InvalidOperationException("Не удалось построить маршрут между карточками графа.");
            }
            List<int> vertices = new List<int>();
            for (int state = end; state >= 0; state = previous[state]) {
                vertices.Add(state / 2);
            }
            vertices.Reverse();
            for (int index = 1; index < vertices.Count; index++) {
                (int A, int B) segment = (Math.Min(vertices[index - 1], vertices[index]), Math.Max(vertices[index - 1], vertices[index]));
                usage.TryGetValue(segment, out int occupied);
                usage[segment] = occupied + 1;
            }
            return vertices.Select(Point).ToList();
        }

        // переводим индекс вершины сетки в координаты рисунка
        private GraphPoint Point(int vertex) => new GraphPoint(xs[vertex % Width], ys[vertex / Width]);

        // запрещаем участки, заходящие в карточку и запас в 10 единиц вокруг нее
        private static bool Clear(GraphPoint a, GraphPoint b, GraphRect[] obstacles) {
            foreach (GraphRect rect in obstacles) {
                // запас оставляет место для небольшого скругления поворотов
                if (a.Y == b.Y) {
                    if (a.Y > rect.Y - 10 && a.Y < rect.Bottom + 10 &&
                        Math.Max(a.X, b.X) > rect.X - 10 && Math.Min(a.X, b.X) < rect.Right + 10) {
                        return false;
                    }
                } else if (a.X > rect.X - 10 && a.X < rect.Right + 10 &&
                    Math.Max(a.Y, b.Y) > rect.Y - 10 && Math.Min(a.Y, b.Y) < rect.Bottom + 10) {
                    return false;
                }
            }
            return true;
        }
    }
}
