using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using SemanticExploverApp.Models;

namespace SemanticExploverApp.Views.Controls;

public sealed partial class NetworkGraphControl { // выбор, перемещение и масштаб
    private IPointer? gesturePointer;
    // от pressPoint считаем порог щелчка, от lastPoint — очередной шаг перемещения
    private Point pressPoint;
    private Point lastPoint;
    private SemanticNode? pressNode;
    private bool dragged;
    // средняя кнопка только перемещает сцену, выбор при отпускании разрешаем для ЛКМ
    private bool selectOnRelease;
    private string? hoverId;

    // запоминаем начало жеста и захватываем указатель до отпускания
    protected override void OnPointerPressed(PointerPressedEventArgs e) {
        base.OnPointerPressed(e);
        PointerPoint point = e.GetCurrentPoint(this);
        if (gesturePointer is not null || layout.Nodes.Count == 0 ||
            (!point.Properties.IsLeftButtonPressed && !point.Properties.IsMiddleButtonPressed)) {
            return;
        }

        Focus();
        gesturePointer = e.Pointer;
        pressPoint = point.Position;
        lastPoint = point.Position;
        pressNode = HitNode(point.Position);
        selectOnRelease = point.Properties.IsLeftButtonPressed;
        dragged = false;
        ToolTip.SetTip(this, null);
        hoverId = null;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    // перемещаем рисунок при жесте, иначе обновляем подсказку узла
    protected override void OnPointerMoved(PointerEventArgs e) {
        base.OnPointerMoved(e);
        Point current = e.GetPosition(this);
        if (gesturePointer == e.Pointer) {
            double dx = current.X - pressPoint.X;
            double dy = current.Y - pressPoint.Y;
            // порог в 4 единицы координат контрола отделяет щелчок от перетаскивания
            if (!dragged && dx * dx + dy * dy > 16) {
                dragged = true;
            }

            if (dragged) {
                PanBy(new Vector(current.X - lastPoint.X, current.Y - lastPoint.Y));
            }
            lastPoint = current;
            e.Handled = true;
            return;
        }

        SemanticNode? node = HitNode(current);
        if (hoverId != node?.Id) {
            hoverId = node?.Id;
            ToolTip.SetTip(this, node is null ? null : $"{node.Label} [{node.Id}]\n{node.Description}");
        }
    }

    // завершаем жест и меняем выбор только после щелчка без перетаскивания
    protected override void OnPointerReleased(PointerReleasedEventArgs e) {
        base.OnPointerReleased(e);
        if (gesturePointer != e.Pointer) {
            return;
        }

        if (e.InitialPressMouseButton != (selectOnRelease ? MouseButton.Left : MouseButton.Middle)) {
            return;
        }

        Point released = e.GetPosition(this);
        SemanticNode? releasedNode = HitNode(released);
        double dx = released.X - pressPoint.X;
        double dy = released.Y - pressPoint.Y;
        bool shouldSelect = selectOnRelease && !dragged && dx * dx + dy * dy <= 16 &&
            pressNode?.Id == releasedNode?.Id;
        CancelGesture();
        if (shouldSelect) {
            // сохраняем привязку TwoWay: обычный SetValue здесь заменил бы ее локальным значением
            SetCurrentValue(SelectedNodeProperty, releasedNode);
        }
        e.Handled = true;
    }

    // прекращаем перетаскивание при потере захвата указателя
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) {
        base.OnPointerCaptureLost(e);
        gesturePointer = null;
        pressNode = null;
        dragged = false;
    }

    // прокруткой перемещаем сцену или масштабируем по выбранному режиму и модификаторам
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e) {
        base.OnPointerWheelChanged(e);
        if (layout.Nodes.Count == 0 || gesturePointer is not null ||
            !double.IsFinite(e.Delta.X) || !double.IsFinite(e.Delta.Y) ||
            (Math.Abs(e.Delta.X) < 0.0001 && Math.Abs(e.Delta.Y) < 0.0001)) {
            return;
        }

        bool zoomRequested = !TouchpadMode ||
            (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        if (zoomRequested) {
            if (Math.Abs(e.Delta.Y) < 0.0001) {
                return;
            }
            ZoomAt(e.GetPosition(this), Math.Pow(1.15, Math.Clamp(e.Delta.Y, -4, 4)));
        } else {
            // macOS передает точную прокрутку в единицах scrollingDelta / 50
            Vector delta = e.Delta * 50;
            if ((e.KeyModifiers & KeyModifiers.Shift) != 0 && Math.Abs(delta.X) < 0.0001) {
                delta = new Vector(delta.Y, 0);
            }
            PanBy(delta);
        }
        e.Handled = true;
    }

    // нативный magnify macOS передает приращение, а не накопленный масштаб жеста
    private void OnTouchpadMagnify(object? sender, PointerDeltaEventArgs e) {
        double factor = 1 + e.Delta.Y;
        if (layout.Nodes.Count == 0 || gesturePointer is not null ||
            !double.IsFinite(factor) || factor <= 0) {
            return;
        }

        ToolTip.SetTip(this, null);
        hoverId = null;
        ZoomAt(e.GetPosition(this), factor);
        e.Handled = true;
    }

    // сдвигаем сцену и убираем подсказку от прежнего положения указателя
    private void PanBy(Vector delta) {
        autoFit = false;
        ToolTip.SetTip(this, null);
        hoverId = null;
        viewport.Pan(delta.X, delta.Y);
        InvalidateVisual();
    }

    // обрабатываем +, минус и F при фокусе на графе
    protected override void OnKeyDown(KeyEventArgs e) {
        base.OnKeyDown(e);
        if (layout.Nodes.Count == 0) {
            return;
        }

        switch (e.Key) {
            case Key.Add:
            case Key.OemPlus:
                Zoom(1.2);
                break;
            case Key.Subtract:
            case Key.OemMinus:
                Zoom(1 / 1.2);
                break;
            case Key.F:
                FitToView();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    // очищаем жест и освобождаем захваченный указатель
    private void CancelGesture() {
        IPointer? pointer = gesturePointer;
        gesturePointer = null;
        pressNode = null;
        dragged = false;
        if (pointer?.Captured == this) {
            pointer.Capture(null);
        }
    }
}
