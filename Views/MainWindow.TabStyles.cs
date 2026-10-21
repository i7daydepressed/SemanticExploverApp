using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace SemanticExploverApp.Views;

public partial class MainWindow { // стили вкладок
    // оформляем вкладки как кнопки
    private static void ApplyButtonTabStyle(TabControl tabs) {
        // меняем только заголовки, выбор вкладки и ее содержимое по-прежнему ведет TabControl
        tabs.ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel {
            Orientation = Orientation.Horizontal,
            Spacing = 4
        });
        tabs.Padding = new Thickness(0, 10, 0, 0);

        Style normal = new Style((Selector? selector) => selector.OfType<TabItem>()) {
            Setters = {
                new Setter(TabItem.FontSizeProperty, 13.0),
                new Setter(TabItem.FontWeightProperty, FontWeight.Normal),
                new Setter(TabItem.HeightProperty, 34.0),
                new Setter(TabItem.MinHeightProperty, 34.0),
                new Setter(TabItem.PaddingProperty, new Thickness(8, 6)),
                new Setter(TabItem.MarginProperty, new Thickness(0)),
                new Setter(TabItem.CornerRadiusProperty, new CornerRadius(5)),
                new Setter(TabItem.BackgroundProperty, new SolidColorBrush(Color.FromArgb(45, 128, 128, 128))),
                new Setter(TabItem.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(80, 128, 128, 128))),
                new Setter(TabItem.BorderThicknessProperty, new Thickness(1)),
                new Setter(TabItem.TemplateProperty, new FuncControlTemplate<TabItem>(CreateButtonTabHeader))
            }
        };
        tabs.Styles.Add(normal);
        tabs.Styles.Add(new Style((Selector? selector) => selector.OfType<TabItem>().Class(":pointerover")) {
            Setters = {
                new Setter(TabItem.BackgroundProperty, new SolidColorBrush(Color.FromArgb(85, 128, 128, 128)))
            }
        });
        tabs.Styles.Add(new Style((Selector? selector) => selector.OfType<TabItem>().Class(":selected")) {
            Setters = {
                new Setter(TabItem.BackgroundProperty, new SolidColorBrush(Color.Parse("#15803D"))),
                new Setter(TabItem.BorderBrushProperty, new SolidColorBrush(Color.Parse("#15803D"))),
                new Setter(TabItem.ForegroundProperty, Brushes.White)
            }
        });
        tabs.Styles.Add(new Style((Selector? selector) => selector.OfType<TabItem>().Class(":selected").Class(":pointerover")) {
            Setters = {
                new Setter(TabItem.BackgroundProperty, new SolidColorBrush(Color.Parse("#166534")))
            }
        });
        tabs.Styles.Add(new Style((Selector? selector) => selector.OfType<TabItem>().Class(":focus-visible")) {
            Setters = {
                new Setter(TabItem.BorderBrushProperty, new SolidColorBrush(Color.Parse("#D97706")))
            }
        });
    }

    // собираем заголовок из текста и оформления вкладки
    private static Control CreateButtonTabHeader(TabItem item, INameScope nameScope) {
        TextBlock label = new TextBlock {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap
        };
        label.Bind(TextBlock.TextProperty, new Binding(nameof(TabItem.Header)) { Source = item });
        label.Bind(TextBlock.FontSizeProperty, new Binding(nameof(TabItem.FontSize)) { Source = item });
        label.Bind(TextBlock.FontWeightProperty, new Binding(nameof(TabItem.FontWeight)) { Source = item });
        label.Bind(TextBlock.ForegroundProperty, new Binding(nameof(TabItem.Foreground)) { Source = item });

        Border face = new Border { Child = label };
        face.Bind(Border.BackgroundProperty, new Binding(nameof(TabItem.Background)) { Source = item });
        face.Bind(Border.BorderBrushProperty, new Binding(nameof(TabItem.BorderBrush)) { Source = item });
        face.Bind(Border.BorderThicknessProperty, new Binding(nameof(TabItem.BorderThickness)) { Source = item });
        face.Bind(Border.CornerRadiusProperty, new Binding(nameof(TabItem.CornerRadius)) { Source = item });
        face.Bind(Border.PaddingProperty, new Binding(nameof(TabItem.Padding)) { Source = item });
        return face;
    }
}
