using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using SemanticExploverApp.Models;
using SemanticExploverApp.ViewModels;

namespace SemanticExploverApp.Views;

public partial class MainWindow : Window { // главное окно
    // задаем размеры окна и создаём содержимое
    public MainWindow() {
        Title = "SemanticExploverApp";
        Width = 1320;
        Height = 860;
        MinWidth = 1100;
        MinHeight = 720;
        Content = CreateLayout();
    }

    // собираем основную разметку окна
    private Control CreateLayout() {
        Grid root = new Grid {
            Margin = new Thickness(16),
            RowDefinitions = new RowDefinitions("Auto,*,240")
        };
        root.Children.Add(CreateHeader());

        Control workspace = CreateWorkspace();
        Grid.SetRow(workspace, 1);
        root.Children.Add(workspace);

        Control details = CreateNodePanel();
        Grid.SetRow(details, 2);
        root.Children.Add(details);
        return root;
    }

    // собираем заголовок, кнопку открытия и статус
    private Control CreateHeader() {
        Grid header = new Grid {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto")
        };
        header.Children.Add(new TextBlock {
            Text = "SemanticExploverApp",
            FontSize = 20,
            FontWeight = FontWeight.SemiBold
        });

        Button openButton = new Button { Content = "Открыть сеть" };
        openButton.Bind(Button.IsEnabledProperty, new Binding(nameof(MainViewModel.CanOpenNetwork)));
        openButton.Click += OpenNetworkButton_OnClick;
        Grid.SetColumn(openButton, 1);
        header.Children.Add(openButton);

        TextBlock fileName = CreateBoundText(nameof(MainViewModel.FileName));
        fileName.Margin = new Thickness(0, 6, 12, 0);
        Grid.SetRow(fileName, 1);
        header.Children.Add(fileName);

        TextBlock summary = CreateBoundText(nameof(MainViewModel.NetworkSummary));
        summary.Margin = new Thickness(0, 4, 12, 0);
        Grid.SetRow(summary, 2);
        header.Children.Add(summary);

        TextBlock status = CreateBoundText(nameof(MainViewModel.StatusMessage));
        status.FontSize = 12;
        status.Margin = new Thickness(0, 4, 0, 0);
        Grid.SetRow(status, 3);
        Grid.SetColumnSpan(status, 2);
        header.Children.Add(status);
        return header;
    }

    // раскладываем запросы, граф и ответ по колонкам
    private Control CreateWorkspace() {
        // две узкие колонки служат промежутками, свободная ширина остается графу
        Grid workspace = new Grid {
            ColumnDefinitions = new ColumnDefinitions("270,12,*,12,300"),
            Margin = new Thickness(0, 12, 0, 12)
        };
        workspace.Children.Add(CreateQueryPanel());
        Control graph = CreateGraphPanel();
        Grid.SetColumn(graph, 2);
        workspace.Children.Add(graph);
        Control result = CreateResultPanel();
        Grid.SetColumn(result, 4);
        workspace.Children.Add(result);
        return workspace;
    }

    // добавляем рамку и отступы вокруг панели
    private static Border CreatePane(Control content) {
        return new Border {
            Padding = new Thickness(12),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 128, 128, 128)),
            CornerRadius = new CornerRadius(6),
            Child = content
        };
    }

    // добавляем вертикальную прокрутку
    private static ScrollViewer CreateScroll(Control content) {
        return new ScrollViewer {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = content
        };
    }

    // создаём текст с привязкой к свойству
    private static TextBlock CreateBoundText(string propertyName) {
        TextBlock text = new TextBlock { TextWrapping = TextWrapping.Wrap };
        text.Bind(TextBlock.TextProperty, new Binding(propertyName));
        return text;
    }

    // создаём список узлов с привязкой выбора
    private static ComboBox CreateNodePicker(string itemsPath, string selectedPath) {
        ComboBox picker = new ComboBox {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxDropDownHeight = 320,
            ItemTemplate = new FuncDataTemplate<SemanticNode>((SemanticNode node, INameScope _) =>
                new TextBlock {
                    Text = node is null ? string.Empty : $"{node.Label} · {node.Id}",
                    TextWrapping = TextWrapping.Wrap
                })
        };
        picker.Bind(ItemsControl.ItemsSourceProperty, new Binding(itemsPath));
        picker.Bind(ComboBox.SelectedItemProperty, new Binding(selectedPath) { Mode = BindingMode.TwoWay });
        picker.Bind(ComboBox.IsEnabledProperty, new Binding(nameof(MainViewModel.CanConfigureQuery)));
        return picker;
    }

    // выбираем файл и передаём его поток на загрузку
    private async void OpenNetworkButton_OnClick(object? sender, RoutedEventArgs e) {
        if (DataContext is not MainViewModel viewModel || viewModel.IsBusy) {
            return;
        }

        if (!StorageProvider.CanOpen) {
            viewModel.StatusMessage = "Выбор файлов недоступен в текущем окружении.";
            viewModel.RightTabIndex = 1;
            return;
        }

        // блокируем повторный выбор на все время диалога и чтения файла
        viewModel.IsBusy = true;

        try {
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions {
                    Title = "Открыть семантическую сеть",
                    AllowMultiple = false,
                    FileTypeFilter = new List<FilePickerFileType> {
                        new FilePickerFileType("Сеть JSON") { Patterns = new[] { "*.json" } }
                    }
                });

            if (files.Count == 0) {
                viewModel.StatusMessage = "Выбор файла отменен.";
                return;
            }

            IStorageFile selectedFile = files[0];
            await using Stream stream = await selectedFile.OpenReadAsync();
            await viewModel.LoadNetworkAsync(stream, selectedFile.Name);
        } catch (OperationCanceledException) {
            viewModel.StatusMessage = "Открытие файла отменено.";
        } catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is NotSupportedException) {
            viewModel.StatusMessage = $"Не удалось открыть файл: {exception.Message}";
            viewModel.RightTabIndex = 1;
        } finally {
            viewModel.IsBusy = false;
        }
    }
}
