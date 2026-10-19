using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using SemanticExploverApp.ViewModels;

namespace SemanticExploverApp.Views;

public class MainWindow : Window
{
    public MainWindow()
    {
        Title = "SemanticExploverApp";
        Width = 900;
        Height = 500;
        MinWidth = 600;
        MinHeight = 320;
        Content = CreateLayout();
    }

    private Control CreateLayout()
    {
        Grid root = new Grid
        {
            Margin = new Thickness(24),
            RowDefinitions = new RowDefinitions("Auto,*")
        };
        StackPanel layout = new StackPanel
        {
            Spacing = 16
        };

        layout.Children.Add(new TextBlock
        {
            Text = "Семантическая сеть",
            FontSize = 24
        });

        Button openButton = new Button
        {
            Content = "Открыть сеть",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        openButton.Bind(
            Button.IsEnabledProperty,
            new Binding(nameof(MainViewModel.CanOpenNetwork)));
        openButton.Click += OpenNetworkButton_OnClick;
        layout.Children.Add(openButton);

        layout.Children.Add(CreateBoundText(nameof(MainViewModel.FileName)));
        layout.Children.Add(CreateBoundText(nameof(MainViewModel.NetworkSummary)));
        layout.Children.Add(CreateBoundText(nameof(MainViewModel.StatusMessage)));
        root.Children.Add(layout);

        // список ошибок прокручивается отдельно и не вытесняет кнопку открытия за границы окна
        ScrollViewer errors = new ScrollViewer
        {
            Margin = new Thickness(0, 12, 0, 0),
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = CreateBoundText(nameof(MainViewModel.ValidationMessage))
        };
        errors.Bind(
            ScrollViewer.IsVisibleProperty,
            new Binding(nameof(MainViewModel.HasValidationErrors)));
        Grid.SetRow(errors, 1);
        root.Children.Add(errors);
        return root;
    }

    private static TextBlock CreateBoundText(string propertyName)
    {
        TextBlock text = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap
        };
        text.Bind(TextBlock.TextProperty, new Binding(propertyName));
        return text;
    }

    private async void OpenNetworkButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.IsBusy)
        {
            return;
        }

        if (!StorageProvider.CanOpen)
        {
            viewModel.StatusMessage = "Выбор файлов недоступен в текущем окружении.";
            return;
        }

        // блокируем повторный выбор на все время диалога и чтения файла
        viewModel.IsBusy = true;

        try
        {
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "Открыть семантическую сеть",
                    AllowMultiple = false,
                    FileTypeFilter = new List<FilePickerFileType>
                    {
                        new FilePickerFileType("Сеть JSON")
                        {
                            Patterns = new[] { "*.json" }
                        }
                    }
                });

            if (files.Count == 0)
            {
                viewModel.StatusMessage = "Выбор файла отменен.";
                return;
            }

            IStorageFile selectedFile = files[0];
            await using Stream stream = await selectedFile.OpenReadAsync();
            await viewModel.LoadNetworkAsync(stream, selectedFile.Name);
        }
        catch (OperationCanceledException)
        {
            viewModel.StatusMessage = "Открытие файла отменено.";
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is NotSupportedException)
        {
            viewModel.StatusMessage = $"Не удалось открыть файл: {exception.Message}";
        }
        finally
        {
            viewModel.IsBusy = false;
        }
    }
}
