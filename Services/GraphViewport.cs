using System;
using SemanticExploverApp.Models;

namespace SemanticExploverApp.Services;

// экранная точка = точка рисунка * масштаб + смещение
public sealed class GraphViewport { // масштаб и смещение рисунка
    private double minScale = 0.02;
    public double Scale { get; private set; } = 1;
    // смещение храним в координатах экрана, поэтому расстояние перемещения не зависит от масштаба
    public double OffsetX { get; private set; }
    public double OffsetY { get; private set; }

    // переводим точку рисунка в координаты контрола
    public GraphPoint ToScreen(GraphPoint point) => new GraphPoint(
        point.X * Scale + OffsetX, point.Y * Scale + OffsetY);

    // возвращаем экранную точку в координаты рисунка
    public GraphPoint ToWorld(GraphPoint point) => new GraphPoint(
        (point.X - OffsetX) / Scale, (point.Y - OffsetY) / Scale);

    // сбрасываем прежнюю навигацию при замене сети
    public void Reset() {
        Scale = 1;
        OffsetX = 0;
        OffsetY = 0;
        minScale = 0.02;
    }

    // вписываем всю сцену с отступами, включая отрицательные координаты
    public bool Fit(GraphRect content, double width, double height) {
        if (content.Width <= 0 || content.Height <= 0 || width <= 48 || height <= 48) {
            return false;
        }

        Scale = Math.Min(1, Math.Min((width - 40) / content.Width, (height - 40) / content.Height));
        // даже большая сеть должна целиком помещаться, нижняя граница не обрезает Fit
        minScale = Math.Min(0.02, Scale);
        CenterAt(content.Center, width, height);
        return true;
    }

    // сохраняем под указателем ту же точку рисунка
    public void ZoomAt(GraphPoint anchor, double factor) {
        if (!double.IsFinite(factor) || factor <= 0) {
            return;
        }

        GraphPoint world = ToWorld(anchor);
        Scale = Math.Clamp(Scale * factor, minScale, 4);
        OffsetX = anchor.X - world.X * Scale;
        OffsetY = anchor.Y - world.Y * Scale;
    }

    // сдвигаем рисунок на экранное расстояние
    public void Pan(double dx, double dy) {
        OffsetX += dx;
        OffsetY += dy;
    }

    // помещаем точку рисунка в центр видимой области
    public void CenterAt(GraphPoint world, double width, double height) {
        OffsetX = width / 2 - world.X * Scale;
        OffsetY = height / 2 - world.Y * Scale;
    }
}
