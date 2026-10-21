using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using SemanticExploverApp.Models;
using SemanticExploverApp.ViewModels;

namespace SemanticExploverApp.Views;

public partial class MainWindow { // панель запросов
    // собираем вкладки запросов
    private Control CreateQueryPanel() {
        Grid panel = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*") };
        panel.Children.Add(new TextBlock { Text = "Запрос к сети", FontSize = 17 });
        TextBlock notice = new TextBlock {
            Text = "Выберите параметры и выполните запрос. Ответ появится справа.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 8)
        };
        Grid.SetRow(notice, 1);
        panel.Children.Add(notice);

        TabControl tabs = new TabControl {
            ItemsSource = new TabItem[] {
                new TabItem { Header = "Соседи", Content = CreateNeighborQuery() },
                new TabItem { Header = "Иерархия", Content = CreateHierarchyQuery() },
                new TabItem { Header = "Путь", Content = CreatePathQuery() }
            }
        };
        ApplyButtonTabStyle(tabs);
        tabs.Bind(TabControl.SelectedIndexProperty,
            new Binding(nameof(MainViewModel.QueryModeIndex)) { Mode = BindingMode.TwoWay });
        Grid.SetRow(tabs, 2);
        panel.Children.Add(tabs);
        return CreatePane(panel);
    }

    // собираем параметры и кнопку поиска соседей
    private static Control CreateNeighborQuery() {
        StackPanel fields = new StackPanel { Spacing = 10, Margin = new Thickness(0, 10, 0, 0) };
        fields.Children.Add(CreateField("Узел",
            CreateNodePicker(nameof(MainViewModel.Nodes), nameof(MainViewModel.QuerySource))));
        ComboBox relation = new ComboBox {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemTemplate = new FuncDataTemplate<RelationDefinition>((RelationDefinition item, INameScope _) =>
                new TextBlock { Text = item?.Label ?? string.Empty, TextWrapping = TextWrapping.Wrap })
        };
        relation.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(MainViewModel.Relations)));
        relation.Bind(ComboBox.SelectedItemProperty,
            new Binding(nameof(MainViewModel.QueryRelation)) { Mode = BindingMode.TwoWay });
        relation.Bind(ComboBox.IsEnabledProperty, new Binding(nameof(MainViewModel.CanConfigureQuery)));
        fields.Children.Add(CreateField("Отношение", relation));
        fields.Children.Add(CreateField("Направление", CreateChoicePicker(
            new List<string> { "Исходящие", "Входящие" }, nameof(MainViewModel.NeighborDirectionIndex))));
        Button execute = new Button { Content = "Найти соседей" };
        execute.Bind(Button.CommandProperty, new Binding(nameof(MainViewModel.FindNeighborsCommand)));
        fields.Children.Add(execute);
        return CreateScroll(fields);
    }

    // собираем параметры и кнопку поиска по иерархии
    private static Control CreateHierarchyQuery() {
        StackPanel fields = new StackPanel { Spacing = 10, Margin = new Thickness(0, 10, 0, 0) };
        fields.Children.Add(CreateField("Класс",
            CreateNodePicker(nameof(MainViewModel.ClassNodes), nameof(MainViewModel.HierarchyClass))));
        fields.Children.Add(CreateField("Направление", CreateChoicePicker(
            new List<string> { "Предки", "Потомки" }, nameof(MainViewModel.HierarchyDirectionIndex))));
        Button execute = new Button { Content = "Показать иерархию" };
        execute.Bind(Button.CommandProperty, new Binding(nameof(MainViewModel.FindHierarchyCommand)));
        fields.Children.Add(execute);
        return CreateScroll(fields);
    }

    // собираем параметры и кнопку поиска пути
    private static Control CreatePathQuery() {
        StackPanel fields = new StackPanel { Spacing = 10, Margin = new Thickness(0, 10, 0, 0) };
        fields.Children.Add(CreateField("Начальный узел",
            CreateNodePicker(nameof(MainViewModel.Nodes), nameof(MainViewModel.QuerySource))));
        fields.Children.Add(CreateField("Конечный узел",
            CreateNodePicker(nameof(MainViewModel.Nodes), nameof(MainViewModel.QueryTarget))));
        Button execute = new Button { Content = "Найти путь" };
        execute.Bind(Button.CommandProperty, new Binding(nameof(MainViewModel.FindPathCommand)));
        fields.Children.Add(execute);
        return CreateScroll(fields);
    }

    // добавляем подпись к полю
    private static Control CreateField(string label, Control input) {
        StackPanel field = new StackPanel { Spacing = 4 };
        field.Children.Add(new TextBlock { Text = label });
        field.Children.Add(input);
        return field;
    }

    // создаём список вариантов с привязкой выбора
    private static ComboBox CreateChoicePicker(IReadOnlyList<string> choices, string propertyName) {
        ComboBox picker = new ComboBox {
            ItemsSource = choices,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        picker.Bind(ComboBox.SelectedIndexProperty,
            new Binding(propertyName) { Mode = BindingMode.TwoWay });
        picker.Bind(ComboBox.IsEnabledProperty, new Binding(nameof(MainViewModel.CanConfigureQuery)));
        return picker;
    }
}
