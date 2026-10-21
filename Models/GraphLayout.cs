using System;
using System.Collections.Generic;

namespace SemanticExploverApp.Models;

public readonly record struct GraphPoint(double X, double Y); // точка рисунка

public readonly record struct GraphRect(double X, double Y, double Width, double Height) { // границы на рисунке
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public GraphPoint Center => new GraphPoint(X + Width / 2, Y + Height / 2);
    // проверяем попадание точки в прямоугольник
    public bool Contains(GraphPoint point) => point.X >= X && point.X <= Right &&
        point.Y >= Y && point.Y <= Bottom;
}

// координаты относятся к рисунку, исходные узлы и факты не меняются
public sealed record GraphLayoutNode(SemanticNode Node, GraphRect Bounds); // исходный узел и его карточка

// Bounds охватывает карточки; границы маршрутов и подписей добавляем к сцене в контроле
public sealed record GraphLayout(IReadOnlyList<GraphLayoutNode> Nodes, GraphRect Bounds) { // раскладка сети
    public const double NodeWidth = 188;
    public const double NodeHeight = 64;
    public static GraphLayout Empty { get; } = new GraphLayout(
        Array.Empty<GraphLayoutNode>(), new GraphRect(0, 0, 0, 0));
}
