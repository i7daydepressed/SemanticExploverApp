using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using SemanticExploverApp.Models;
using SemanticExploverApp.ViewModels;

namespace SemanticExploverApp.Views;

public partial class MainWindow { // панели результата и сведений об узле
    // собираем вкладки ответа и ошибок проверки
    private static Control CreateResultPanel() {
        StackPanel validation = new StackPanel { Spacing = 10, Margin = new Thickness(0, 10, 0, 0) };
        validation.Children.Add(CreateBoundText(nameof(MainViewModel.StatusMessage)));
        TextBlock issues = CreateBoundText(nameof(MainViewModel.ValidationMessage));
        issues.Bind(TextBlock.IsVisibleProperty, new Binding(nameof(MainViewModel.HasValidationErrors)));
        validation.Children.Add(issues);

        Grid result = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        TextBlock summary = CreateBoundText(nameof(MainViewModel.QueryResultText));
        summary.Margin = new Thickness(0, 10, 0, 8);
        ScrollViewer summaryScroll = CreateScroll(summary);
        summaryScroll.MaxHeight = 136;
        result.Children.Add(summaryScroll);
        ListBox items = new ListBox {
            ItemTemplate = new FuncDataTemplate<QueryResultItem>((QueryResultItem item, INameScope _) =>
                new TextBlock {
                    Text = item.DisplayText,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4)
                })
        };
        items.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,
            Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);
        items.Styles.Add(new Style((Selector? selector) => selector.OfType<ListBoxItem>()) {
            Setters = { new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch) }
        });
        items.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(MainViewModel.QueryResultItems)));
        items.Bind(ListBox.SelectedItemProperty,
            new Binding(nameof(MainViewModel.SelectedResultItem)) { Mode = BindingMode.TwoWay });
        Grid.SetRow(items, 1);
        result.Children.Add(items);
        // список ошибок прокручивается отдельно и не вытесняет кнопку открытия за границы окна
        TabControl tabs = new TabControl {
            ItemsSource = new TabItem[] {
                new TabItem { Header = "Результат", Content = result },
                new TabItem { Header = "Проверка", Content = CreateScroll(validation) }
            }
        };
        ApplyButtonTabStyle(tabs);
        tabs.Bind(TabControl.SelectedIndexProperty,
            new Binding(nameof(MainViewModel.RightTabIndex)) { Mode = BindingMode.TwoWay });
        return CreatePane(tabs);
    }

    // собираем выбор узла и вкладки его сведений
    private static Control CreateNodePanel() {
        Grid panel = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        Grid heading = new Grid {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,320"),
            Margin = new Thickness(0, 0, 0, 6)
        };
        heading.Children.Add(new TextBlock {
            Text = "Выбранный узел:",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });
        TextBlock title = CreateBoundText("SelectedNodeDetails.Title");
        title.VerticalAlignment = VerticalAlignment.Center;
        title.Margin = new Thickness(0, 0, 12, 0);
        Grid.SetColumn(title, 1);
        heading.Children.Add(title);

        // до появления графа этот список позволяет проверить выбор настоящего узла
        ComboBox selectedNode = CreateNodePicker(nameof(MainViewModel.Nodes), nameof(MainViewModel.SelectedNode));
        Grid.SetColumn(selectedNode, 2);
        heading.Children.Add(selectedNode);
        panel.Children.Add(heading);

        StackPanel description = new StackPanel { Spacing = 4, Margin = new Thickness(0, 6, 0, 0) };
        description.Children.Add(CreateBoundText("SelectedNodeDetails.Title"));
        description.Children.Add(CreateField("ID", CreateBoundText("SelectedNodeDetails.Id")));
        description.Children.Add(CreateField("Тип", CreateBoundText("SelectedNodeDetails.Kind")));
        description.Children.Add(CreateBoundText("SelectedNodeDetails.Description"));
        TabControl tabs = new TabControl {
            ItemsSource = new TabItem[] {
                new TabItem { Header = "Описание", Content = CreateScroll(description) },
                new TabItem { Header = "Связи", Content = CreateScroll(CreateBoundText("SelectedNodeDetails.RelationsText")) },
                new TabItem { Header = "Источники", Content = CreateScroll(CreateBoundText("SelectedNodeDetails.SourcesText")) }
            }
        };
        ApplyButtonTabStyle(tabs);
        tabs.Bind(TabControl.SelectedIndexProperty,
            new Binding(nameof(MainViewModel.NodeTabIndex)) { Mode = BindingMode.TwoWay });
        Grid.SetRow(tabs, 1);
        panel.Children.Add(tabs);
        return CreatePane(panel);
    }
}
