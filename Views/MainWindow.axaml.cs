using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ExifApp.ViewModels;

namespace ExifApp.Views;

public partial class MainWindow : Window
{

    // Ссылка на ленту миниатюр. Получаем через FindControl — не зависим
    // от автогенерации полей по x:Name.
    private ListBox? _thumbnailsListBox;
    private Image? _photoImage;

    public MainWindow()
    {
        InitializeComponent();

        _thumbnailsListBox = this.FindControl<ListBox>("ThumbnailsListBox");
        _photoImage = this.FindControl<Image>("PhotoImage");

        DataContext = new MainWindowViewModel();

        AddHandler(KeyDownEvent, OnGlobalKeyDown, RoutingStrategies.Tunnel);

        if (DataContext is MainWindowViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void ApplyBusyCursor(bool busy)
    {
        if (busy)
        {
            Cursor = new Cursor(StandardCursorType.Wait);
            if (_photoImage is not null)
                _photoImage.Cursor = new Cursor(StandardCursorType.Wait);
        }
        else
        {
            Cursor = Cursor.Default;
            if (_photoImage is not null)
                _photoImage.Cursor = new Cursor(StandardCursorType.Hand);
        }
    }
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (e.PropertyName == nameof(MainWindowViewModel.IsBusy))
        {
            ApplyBusyCursor(vm.IsBusy);
            return;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.CurrentIndex))
        {
            if (vm.CurrentIndex < 0 || _thumbnailsListBox is null) return;

            Dispatcher.UIThread.Post(() =>
            {
                var index = vm.CurrentIndex;
                if (index < 0 || index >= _thumbnailsListBox.ItemCount) return;

                var container = _thumbnailsListBox.ContainerFromIndex(index);
                if (container is Control control)
                    control.BringIntoView();
                else
                    _thumbnailsListBox.ScrollIntoView(index);
            }, DispatcherPriority.Background);
        }
    }
    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        switch (e.Key)
        {
            case Key.Left:
                if (vm.PreviousCommand.CanExecute(null))
                {
                    vm.PreviousCommand.Execute(null);
                    e.Handled = true;
                }
                break;

            case Key.Right:
                if (vm.NextCommand.CanExecute(null))
                {
                    vm.NextCommand.Execute(null);
                    e.Handled = true;
                }
                break;

            case Key.Delete:
                if (vm.DeleteCurrentFileCommand.CanExecute(null))
                {
                    vm.DeleteCurrentFileCommand.Execute(null);
                    e.Handled = true;
                }
                break;
        }
    }
    private void OnPhotoDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (vm.OpenInExternalEditorCommand.CanExecute(null))
        {
            vm.OpenInExternalEditorCommand.Execute(null);
            e.Handled = true;
        }
        else
        {
            vm.StatusMessage = "Внешняя программа недоступна. Проверьте ExternalEditors в appsettings.json.";
        }
    }
    private async void OnLoadImageClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите файл изображения",
            AllowMultiple = false,
            FileTypeFilter = new[]
    {
        new FilePickerFileType("Изображения (JPEG, PNG)")
        {
            Patterns = new[] { "*.jpg", "*.jpeg", "*.png" },
            MimeTypes = new[] { "image/jpeg", "image/png" }
        },
        new FilePickerFileType("JPEG")
        {
            Patterns = new[] { "*.jpg", "*.jpeg" },
            MimeTypes = new[] { "image/jpeg" }
        },
        new FilePickerFileType("PNG")
        {
            Patterns = new[] { "*.png" },
            MimeTypes = new[] { "image/png" }
        },
        FilePickerFileTypes.All
    }
        });

        if (files.Count == 0) return;

        var localPath = files[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(localPath)) return;

        if (DataContext is MainWindowViewModel vm)
        {
            await vm.LoadImageFromPathAsync(localPath);
        }
    }
    private async void OnOpenSettingsClick(object? sender, RoutedEventArgs e)
    {
        // Получить AppSettingsService из ViewModel
        if (DataContext is not MainWindowViewModel vm) return;

        var window = new SettingsWindow(vm.SettingsService);
        await window.ShowDialog(this);
    }
}