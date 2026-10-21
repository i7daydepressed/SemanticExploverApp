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
            Margin = new Thickness(12),
            RowDefinitions = new RowDefinitions("Auto,*,6,160")
        };
        root.RowDefinitions[1].MinHeight = 180;
        root.RowDefinitions[3].MinHeight = 96;
        root.Children.Add(CreateHeader());

        Control workspace = CreateWorkspace();
        Grid.SetRow(workspace, 1);
        root.Children.Add(workspace);

        GridSplitter horizontal = CreatePanelSplitter(GridResizeDirection.Rows);
        Grid.SetRow(horizontal, 2);
        root.Children.Add(horizontal);
        Control details = CreateNodePanel();
        Grid.SetRow(details, 3);
        root.Children.Add(details);
        return root;
    }

    // помещаем название и файл в одну строку, сводку и статус в следующую
    private Control CreateHeader() {
        Grid header = new Grid {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto")
        };
        header.Children.Add(new TextBlock {
            Text = "SemanticExploverApp",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        TextBlock fileName = CreateBoundText(nameof(MainViewModel.FileName));
        fileName.Margin = new Thickness(14, 0, 14, 0);
        fileName.VerticalAlignment = VerticalAlignment.Center;
        fileName.TextWrapping = TextWrapping.NoWrap;
        fileName.TextTrimming = TextTrimming.CharacterEllipsis;
        fileName.Bind(ToolTip.TipProperty, new Binding(nameof(MainViewModel.FileName)));
        Grid.SetColumn(fileName, 1);
        header.Children.Add(fileName);

        Button openButton = new Button { Content = "Открыть сеть" };
        openButton.Bind(Button.IsEnabledProperty, new Binding(nameof(MainViewModel.CanOpenNetwork)));
        openButton.Click += OpenNetworkButton_OnClick;
        Grid.SetColumn(openButton, 2);
        header.Children.Add(openButton);

        Grid information = new Grid {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(0, 6, 0, 0)
        };
        TextBlock summary = CreateBoundText(nameof(MainViewModel.NetworkSummary));
        summary.FontSize = 12;
        summary.Margin = new Thickness(0, 0, 14, 0);
        information.Children.Add(summary);
        TextBlock status = CreateBoundText(nameof(MainViewModel.StatusMessage));
        status.FontSize = 12;
        status.TextWrapping = TextWrapping.NoWrap;
        status.TextTrimming = TextTrimming.CharacterEllipsis;
        status.Bind(ToolTip.TipProperty, new Binding(nameof(MainViewModel.StatusMessage)));
        Grid.SetColumn(status, 1);
        information.Children.Add(status);
        Grid.SetRow(information, 1);
        Grid.SetColumnSpan(information, 3);
        header.Children.Add(information);
        return header;
    }

    // ширину боковых панелей можно менять, свободная ширина остается графу
    private Control CreateWorkspace() {
        Grid workspace = new Grid {
            ColumnDefinitions = new ColumnDefinitions("260,6,*,6,280"),
            Margin = new Thickness(0, 8, 0, 0)
        };
        workspace.ColumnDefinitions[0].MinWidth = 240;
        workspace.ColumnDefinitions[2].MinWidth = 260;
        workspace.ColumnDefinitions[4].MinWidth = 240;
        workspace.Children.Add(CreateQueryPanel());
        GridSplitter left = CreatePanelSplitter(GridResizeDirection.Columns);
        Grid.SetColumn(left, 1);
        workspace.Children.Add(left);
        Control graph = CreateGraphPanel();
        Grid.SetColumn(graph, 2);
        workspace.Children.Add(graph);
        GridSplitter right = CreatePanelSplitter(GridResizeDirection.Columns);
        Grid.SetColumn(right, 3);
        workspace.Children.Add(right);
        Control result = CreateResultPanel();
        Grid.SetColumn(result, 4);
        workspace.Children.Add(result);
        return workspace;
    }

    // разделитель меняет соседние строки или колонки и учитывает их минимальные размеры
    private static GridSplitter CreatePanelSplitter(GridResizeDirection direction) {
        return new GridSplitter {
            ResizeDirection = direction,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128)),
            Focusable = true
        };
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
