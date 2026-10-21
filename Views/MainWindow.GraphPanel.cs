using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using SemanticExploverApp.ViewModels;
using SemanticExploverApp.Views.Controls;

namespace SemanticExploverApp.Views;

public partial class MainWindow { // граф сети и его управление
    // собираем граф, легенду и кнопки навигации
    private static Control CreateGraphPanel() {
        Grid panel = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };
        NetworkGraphControl graph = new NetworkGraphControl();
        graph.Bind(NetworkGraphControl.NetworkProperty,
            new Binding(nameof(MainViewModel.Network)) { Mode = BindingMode.OneWay });
        graph.Bind(NetworkGraphControl.SelectedNodeProperty,
            new Binding(nameof(MainViewModel.SelectedNode)) { Mode = BindingMode.TwoWay });
        graph.Bind(NetworkGraphControl.IsEnabledProperty, new Binding(nameof(MainViewModel.CanConfigureQuery)));
        Grid.SetRow(graph, 2);
        panel.Children.Add(graph);

        WrapPanel tools = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
        tools.Children.Add(new TextBlock {
            Text = "Граф сети",
            FontSize = 17,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 4)
        });
        Button zoomOut = new Button { Content = "−", Margin = new Thickness(0, 0, 4, 4) };
        Button zoomIn = new Button { Content = "+", Margin = new Thickness(0, 0, 4, 4) };
        Button fit = new Button { Content = "Вместить", Margin = new Thickness(0, 0, 8, 4) };
        zoomOut.Click += (_, _) => graph.Zoom(1 / 1.2);
        zoomIn.Click += (_, _) => graph.Zoom(1.2);
        fit.Click += (_, _) => graph.FitToView();
        foreach (Button button in new Button[] { zoomOut, zoomIn, fit }) {
            button.Bind(Button.IsEnabledProperty, new Binding(nameof(MainViewModel.CanConfigureQuery)));
            tools.Children.Add(button);
        }
        CheckBox touchpad = new CheckBox {
            Content = "Тачпад",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        };
        touchpad.Bind(CheckBox.IsCheckedProperty,
            new Binding(nameof(NetworkGraphControl.TouchpadMode)) { Source = graph, Mode = BindingMode.TwoWay });
        ToolTip.SetTip(touchpad, "Включено: прокрутка двумя пальцами перемещает граф. " +
            "Выключено: колесо масштабирует граф. Ctrl/⌘ + прокрутка — масштаб в обоих режимах. " +
            "На macOS щипок на тачпаде меняет масштаб независимо от переключателя.");
        tools.Children.Add(touchpad);
        panel.Children.Add(tools);

        WrapPanel legend = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        TextBlock classLegend = new TextBlock { Text = "● Класс   " };
        TextBlock instanceLegend = new TextBlock { Text = "● Экземпляр   " };
        TextBlock valueLegend = new TextBlock { Text = "● Значение" };
        legend.Children.Add(classLegend);
        legend.Children.Add(instanceLegend);
        legend.Children.Add(valueLegend);
        // обновляем акценты легенды той же палитрой, что и карточки
        void UpdateLegend() {
            GraphPalette palette = GraphPalette.For(graph.ActualThemeVariant);
            classLegend.Foreground = palette.ClassBorder;
            instanceLegend.Foreground = palette.InstanceBorder;
            valueLegend.Foreground = palette.ValueBorder;
        }
        graph.ActualThemeVariantChanged += (_, _) => UpdateLegend();
        UpdateLegend();
        Grid.SetRow(legend, 1);
        panel.Children.Add(legend);

        TextBlock hint = new TextBlock {
            Text = "ЛКМ — выбор и перемещение · F — вместить · Ctrl/⌘ + прокрутка — масштаб",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            Margin = new Thickness(0, 6, 0, 0)
        };
        Grid.SetRow(hint, 3);
        panel.Children.Add(hint);
        return CreatePane(panel);
    }
}
