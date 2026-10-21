using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using SemanticExploverApp.Models;

namespace SemanticExploverApp.Views.Controls;

public sealed partial class NetworkGraphControl { // подготовка и рисование сцены
    private GraphPalette Palette => GraphPalette.For(ActualThemeVariant);
    private sealed record NodeDrawing(GraphLayoutNode Item, FormattedText Title, FormattedText Meta,
        IBrush Fill, IBrush Border); // готовая карточка узла
    // Samples хранит опорные точки маршрута для размещения подписи; саму линию рисуем из StreamGeometry
    private sealed record EdgeDrawing(SemanticEdge Edge, StreamGeometry Line, StreamGeometry Arrow,
        FormattedText Label, Rect LabelBounds, Point[] Samples); // линия факта, стрелка, подпись и опорные точки

    // оформление и геометрию готовим при замене сети или темы, координаты узлов не меняем
    private void BuildDrawings() {
        nodeDrawings.Clear();
        edgeDrawings.Clear();
        sceneBounds = layout.Bounds;
        SemanticNetwork? network = Network;
        if (network is null || layout.Nodes.Count == 0) {
            return;
        }
        Dictionary<string, RelationDefinition> relations = network.Relations.ToDictionary(
            (RelationDefinition relation) => relation.Id, StringComparer.Ordinal);
        foreach (GraphLayoutNode item in layout.Nodes) {
            (IBrush fill, IBrush border, string kind) = Palette.ForKind(item.Node.Kind);
            nodeDrawings.Add(new NodeDrawing(item,
                MakeText(item.Node.Label, 13, GraphLayout.NodeWidth - 16, 32, 2),
                MakeText($"{item.Node.Id} · {kind}", 10, GraphLayout.NodeWidth - 16, 16, 1, Palette.MutedText),
                fill, border));
        }
        foreach (SemanticEdge edge in network.Edges.OrderBy((SemanticEdge edge) => edge.Id, StringComparer.Ordinal)) {
            Point[] points = routes[edge.Id].Select((GraphPoint point) => new Point(point.X, point.Y)).ToArray();
            edgeDrawings.Add(CreateEdge(edge, relations[edge.Relation].Label, points));
        }
        PlaceLabels();
        Rect total = ToRect(layout.Bounds);
        foreach (EdgeDrawing edge in edgeDrawings) {
            total = total.Union(edge.Line.Bounds).Union(edge.Arrow.Bounds).Union(edge.LabelBounds);
        }
        sceneBounds = new GraphRect(total.X - 16, total.Y - 16, total.Width + 32, total.Height + 32);
    }

    // берем готовые точки маршрута от Source до Target, без повторов и нулевых участков
    // каждый угол скругляем радиусом не больше 6 и половины соседних участков, остальные части оставляем прямыми
    // последний участок задает направление наконечника; сами точки сохраняем для размещения подписи
    // возвращаем графические данные, не пересчитывая маршрут и не меняя исходный факт
    private EdgeDrawing CreateEdge(SemanticEdge edge, string label, Point[] points) {
        StreamGeometry line = new StreamGeometry();
        using (StreamGeometryContext path = line.Open()) {
            path.BeginFigure(points[0], false);
            for (int index = 1; index + 1 < points.Length; index++) {
                Point corner = points[index];
                Vector incoming = corner - points[index - 1];
                Vector outgoing = points[index + 1] - corner;
                double radius = Math.Min(6, Math.Min(incoming.Length, outgoing.Length) / 2);
                Point before = corner - incoming / incoming.Length * radius;
                Point after = corner + outgoing / outgoing.Length * radius;
                path.LineTo(before);
                path.QuadraticBezierTo(corner, after);
            }
            path.LineTo(points[^1]);
            path.EndFigure(false);
        }
        Point end = points[^1];
        Vector tangent = end - points[^2];
        Vector unit = tangent / tangent.Length;
        StreamGeometry arrow = new StreamGeometry();
        using (StreamGeometryContext path = arrow.Open()) {
            path.BeginFigure(end, true);
            path.LineTo(new Point(end.X - unit.X * 9 - unit.Y * 4, end.Y - unit.Y * 9 + unit.X * 4));
            path.LineTo(new Point(end.X - unit.X * 9 + unit.Y * 4, end.Y - unit.Y * 9 - unit.X * 4));
            path.EndFigure(true);
        }
        FormattedText text = MakeText(label, 11, 160, 32, 2, Palette.MutedText);
        return new EdgeDrawing(edge, line, arrow, text, new Rect(0, 0, text.Width + 8, text.Height + 4), points);
    }

    // подпись остается рядом со своим прямым участком; длинные горизонтальные участки предпочтительнее
    // у каждого сегмента пробуем три позиции и обе стороны
    // placed хранит подписи уже обработанных фактов; штрафуем карточки, прежние подписи и все участки линий
    // сохраняем место с меньшим штрафом рядом со своим сегментом; жадный выбор не исключает все наложения
    private void PlaceLabels() {
        List<Rect> placed = new List<Rect>();
        for (int index = 0; index < edgeDrawings.Count; index++) {
            EdgeDrawing edge = edgeDrawings[index];
            double width = edge.LabelBounds.Width;
            double height = edge.LabelBounds.Height;
            double bestScore = double.PositiveInfinity;
            Rect best = edge.LabelBounds;
            for (int segment = 1; segment < edge.Samples.Length; segment++) {
                Point a = edge.Samples[segment - 1];
                Point b = edge.Samples[segment];
                bool horizontal = a.Y == b.Y;
                Vector segmentVector = b - a;
                double length = segmentVector.Length;
                foreach (double fraction in new double[] { 0.5, 0.25, 0.75 }) {
                    Point anchor = a + (b - a) * fraction;
                    foreach (int side in new int[] { -1, 1 }) {
                        Rect candidate = horizontal
                            ? new Rect(anchor.X - width / 2, anchor.Y + (side < 0 ? -height - 5 : 5), width, height)
                            : new Rect(anchor.X + (side < 0 ? -width - 5 : 5), anchor.Y - height / 2, width, height);
                        double score = (horizontal ? 0 : 12) + Math.Abs(fraction - 0.5) * 4 +
                            (length >= (horizontal ? width : height) + 16 ? 0 : 40) + 1 / Math.Max(1, length);
                        foreach (GraphLayoutNode node in layout.Nodes) {
                            if (candidate.Intersects(ToRect(node.Bounds).Inflate(4))) {
                                score += 10000;
                            }
                        }
                        foreach (Rect other in placed) {
                            if (candidate.Intersects(other.Inflate(3))) {
                                score += 1000;
                            }
                        }
                        foreach (EdgeDrawing other in edgeDrawings) {
                            for (int part = 1; part < other.Samples.Length; part++) {
                                if (Intersects(candidate, other.Samples[part - 1], other.Samples[part])) {
                                    score += 50;
                                }
                            }
                        }
                        if (score < bestScore) { bestScore = score; best = candidate; }
                    }
                }
            }
            edgeDrawings[index] = edge with { LabelBounds = best };
            placed.Add(best);
        }
    }

    // проверяем пересечение всего горизонтального или вертикального участка с внутренностью подписи
    private static bool Intersects(Rect rect, Point a, Point b) => a.Y == b.Y
        ? a.Y > rect.Y && a.Y < rect.Bottom && Math.Max(a.X, b.X) > rect.X && Math.Min(a.X, b.X) < rect.Right
        : a.X > rect.X && a.X < rect.Right && Math.Max(a.Y, b.Y) > rect.Y && Math.Min(a.Y, b.Y) < rect.Bottom;

    // рисуем готовые дуги и карточки с текущим масштабом, выделяем выбранный узел
    public override void Render(DrawingContext context) {
        base.Render(context);
        GraphPalette palette = Palette;
        Rect screen = new Rect(Bounds.Size);
        context.DrawRectangle(palette.Background, null, screen);
        using (context.PushClip(screen)) {
            if (layout.Nodes.Count == 0) {
                string message = Network is null ? "Откройте файл семантической сети." : "В сети нет узлов.";
                if (Bounds.Width > 24 && Bounds.Height > 24) {
                    context.DrawText(MakeText(message, 14, Math.Max(1, Bounds.Width - 24), 60, 3), new Point(12, 12));
                }
                return;
            }

            using (context.PushTransform(new Matrix(viewport.Scale, 0, 0, viewport.Scale,
                viewport.OffsetX, viewport.OffsetY))) {
                foreach (EdgeDrawing edge in edgeDrawings) {
                    bool active = SelectedNode is not null &&
                        (edge.Edge.Source == SelectedNode.Id || edge.Edge.Target == SelectedNode.Id);
                    IBrush brush = active ? palette.Active : palette.Edge;
                    context.DrawGeometry(null, new Pen(brush, active ? 2 : 1.25), edge.Line);
                    context.DrawGeometry(brush, null, edge.Arrow);
                }
                // подписи смещены от линии и не вырезают из нее белые прямоугольники
                foreach (EdgeDrawing edge in edgeDrawings) {
                    context.DrawText(edge.Label, new Point(edge.LabelBounds.X + 4, edge.LabelBounds.Y + 2));
                }
                foreach (NodeDrawing node in nodeDrawings) {
                    Rect rect = ToRect(node.Item.Bounds);
                    bool selected = node.Item.Node.Id == SelectedNode?.Id;
                    Pen border = selected ? new Pen(palette.Selection, 2 / viewport.Scale) : new Pen(node.Border, 1.25);
                    context.DrawRectangle(node.Fill, border, rect, 6, 6);
                    context.DrawText(node.Title, new Point(rect.X + 8, rect.Y + 6));
                    context.DrawText(node.Meta, new Point(rect.X + 8, rect.Bottom - 20));
                }
            }
        }
    }

    // ограничиваем текст размером и числом строк, остаток заменяем многоточием
    private FormattedText MakeText(string text, double size, double width, double height, int lines, IBrush? brush = null) {
        return new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default), size, brush ?? Palette.Text) {
            MaxTextWidth = width,
            MaxTextHeight = height,
            MaxLineCount = lines,
            Trimming = TextTrimming.CharacterEllipsis
        };
    }

    // переводим независимые границы модели в прямоугольник Avalonia
    private static Rect ToRect(GraphRect rect) => new Rect(rect.X, rect.Y, rect.Width, rect.Height);
}
