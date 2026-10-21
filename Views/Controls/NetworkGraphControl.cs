using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using SemanticExploverApp.Models;
using SemanticExploverApp.Services;

namespace SemanticExploverApp.Views.Controls;

public sealed partial class NetworkGraphControl : Control { // состояние графа и привязки
    public static readonly StyledProperty<SemanticNetwork?> NetworkProperty =
        AvaloniaProperty.Register<NetworkGraphControl, SemanticNetwork?>(nameof(Network));
    public static readonly StyledProperty<SemanticNode?> SelectedNodeProperty =
        AvaloniaProperty.Register<NetworkGraphControl, SemanticNode?>(nameof(SelectedNode),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> TouchpadModeProperty =
        AvaloniaProperty.Register<NetworkGraphControl, bool>(nameof(TouchpadMode),
            defaultValue: OperatingSystem.IsMacOS());

    private readonly GraphLayoutService layoutService = new GraphLayoutService();
    private readonly GraphViewport viewport = new GraphViewport();
    // графические данные можно перекрасить, сохранив отдельные layout и routes
    private readonly List<NodeDrawing> nodeDrawings = new List<NodeDrawing>();
    private readonly List<EdgeDrawing> edgeDrawings = new List<EdgeDrawing>();
    private IReadOnlyDictionary<string, IReadOnlyList<GraphPoint>> routes =
        new Dictionary<string, IReadOnlyList<GraphPoint>>(StringComparer.Ordinal);
    private GraphLayout layout = GraphLayout.Empty;
    // для «Вместить» учитываем не только карточки, но и выступающие маршруты, стрелки и подписи
    private GraphRect sceneBounds;
    // после ручной навигации изменение размеров не должно сбрасывать положение просмотра
    private bool autoFit = true;

    public SemanticNetwork? Network {
        get => GetValue(NetworkProperty);
        set => SetValue(NetworkProperty, value);
    }

    public SemanticNode? SelectedNode {
        get => GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    public bool TouchpadMode {
        get => GetValue(TouchpadModeProperty);
        set => SetValue(TouchpadModeProperty, value);
    }

    // включаем фокус и обрезку, завершаем жест при удалении контрола
    public NetworkGraphControl() {
        Focusable = true;
        ClipToBounds = true;
        PointerTouchPadGestureMagnify += OnTouchpadMagnify;
        ActualThemeVariantChanged += (_, _) => {
            // тема меняет оформление, координаты и положение просмотра сохраняются
            BuildDrawings();
            InvalidateVisual();
        };
        DetachedFromVisualTree += (_, _) => CancelGesture();
    }

    // перестраиваем сцену при смене сети, синхронизируем выбор и размеры
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);
        if (change.Property == NetworkProperty) {
            CancelGesture();
            ToolTip.SetTip(this, null);
            hoverId = null;
            layout = Network is null ? GraphLayout.Empty : layoutService.Build(Network);
            routes = Network is null
                ? new Dictionary<string, IReadOnlyList<GraphPoint>>(StringComparer.Ordinal)
                : new GraphRoutingService().Build(Network, layout);
            BuildDrawings();
            viewport.Reset();
            autoFit = true;
            FitToView();
            InvalidateVisual();
        } else if (change.Property == SelectedNodeProperty) {
            EnsureSelectedVisible();
            InvalidateVisual();
        } else if (change.Property == BoundsProperty && autoFit) {
            FitToView();
        } else if (change.Property == IsEnabledProperty && !IsEnabled) {
            CancelGesture();
        }
    }

    // вписываем сцену и включаем автоматическое вписывание при изменении размера
    public void FitToView() {
        autoFit = true;
        if (viewport.Fit(sceneBounds, Bounds.Width, Bounds.Height)) {
            InvalidateVisual();
        }
    }

    // масштабируем относительно центра видимой области
    public void Zoom(double factor) => ZoomAt(new Point(Bounds.Width / 2, Bounds.Height / 2), factor);

    // масштабируем под указателем и прекращаем автоматическое вписывание
    private void ZoomAt(Point anchor, double factor) {
        if (layout.Nodes.Count == 0) {
            return;
        }

        autoFit = false;
        viewport.ZoomAt(new GraphPoint(anchor.X, anchor.Y), factor);
        InvalidateVisual();
    }

    // находим исходный узел под экранной точкой
    private SemanticNode? HitNode(Point screen) {
        GraphPoint world = viewport.ToWorld(new GraphPoint(screen.X, screen.Y));
        for (int index = layout.Nodes.Count - 1; index >= 0; index--) {
            GraphLayoutNode node = layout.Nodes[index];
            if (node.Bounds.Contains(world)) {
                return node.Node;
            }
        }

        return null;
    }

    // внешний выбор не меняет масштаб, но возвращает узел из-за границ видимой области
    private void EnsureSelectedVisible() {
        if (SelectedNode is null || Bounds.Width <= 0 || Bounds.Height <= 0) {
            return;
        }

        foreach (GraphLayoutNode item in layout.Nodes) {
            if (item.Node.Id != SelectedNode.Id) {
                continue;
            }

            GraphPoint topLeft = viewport.ToScreen(new GraphPoint(item.Bounds.X, item.Bounds.Y));
            GraphPoint bottomRight = viewport.ToScreen(new GraphPoint(item.Bounds.Right, item.Bounds.Bottom));
            if (topLeft.X < 12 || topLeft.Y < 12 || bottomRight.X > Bounds.Width - 12 || bottomRight.Y > Bounds.Height - 12) {
                autoFit = false;
                viewport.CenterAt(item.Bounds.Center, Bounds.Width, Bounds.Height);
            }
            return;
        }
    }
}
