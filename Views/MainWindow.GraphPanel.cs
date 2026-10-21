using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using SemanticExploverApp.ViewModels;

namespace SemanticExploverApp.Views;

public partial class MainWindow { // заглушка графа
    // создаём заглушку графа с отключённым управлением
    private static Control CreateGraphPanel() {
        Grid panel = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*") };
        panel.Children.Add(new TextBlock { Text = "Граф сети", FontSize = 17 });
        WrapPanel tools = new WrapPanel { Margin = new Thickness(0, 8, 0, 8) };
        tools.Children.Add(new ComboBox {
            ItemsSource = new string[] { "Вся сеть", "Фрагмент" },
            SelectedIndex = 0,
            IsEnabled = false,
            MinWidth = 120,
            Margin = new Thickness(0, 0, 8, 4)
        });
        tools.Children.Add(new Button { Content = "−", IsEnabled = false, Margin = new Thickness(0, 0, 4, 4) });
        tools.Children.Add(new Button { Content = "+", IsEnabled = false, Margin = new Thickness(0, 0, 4, 4) });
        tools.Children.Add(new Button { Content = "Вместить", IsEnabled = false });
        Grid.SetRow(tools, 1);
        panel.Children.Add(tools);

        TextBlock placeholder = CreateBoundText(nameof(MainViewModel.GraphPlaceholder));
        placeholder.HorizontalAlignment = HorizontalAlignment.Center;
        placeholder.VerticalAlignment = VerticalAlignment.Center;
        placeholder.Margin = new Thickness(12);
        Grid.SetRow(placeholder, 2);
        panel.Children.Add(placeholder);
        return CreatePane(panel);
    }
}
