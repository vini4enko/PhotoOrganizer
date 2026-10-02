using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExifApp.Configuration;
using ExifApp.Models;
using ExifApp.Services;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace ExifApp.ViewModels;
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenInExternalEditorCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCurrentFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConvertToJpegCommand))]
    [NotifyPropertyChangedFor(nameof(PositionText))]
    private int _currentIndex = -1;
    private readonly AppSettingsService _settingsService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private Bitmap? _photo;

    [ObservableProperty]
    private string? _selectedFilePath;

    [ObservableProperty]
    private string _statusMessage = "Выберите JPEG файл для просмотра";

    [ObservableProperty]
    private bool _isFilterEnabled;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenInExternalEditorCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCurrentFileCommand))]  // <-- добавлено
    [NotifyPropertyChangedFor(nameof(PositionText))]

    private int _totalCount;
    
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// Выбранная внешняя программа.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenInExternalEditorCommand))]
    private ExternalEditor? _selectedExternalEditor;
    public AppSettingsService SettingsService => _settingsService;

    /// <summary>
    /// Список доступных внешних программ (из appsettings.json).
    /// </summary>
    public ObservableCollection<ExternalEditor> ExternalEditors { get; } = new();

    public string PositionText =>
        TotalCount > 0 && CurrentIndex >= 0 ? $"{CurrentIndex + 1} / {TotalCount}" : "—";

    public ObservableCollection<PhotoEntry> PhotoList { get; } = new();

    private readonly ObservableCollection<ExifItem> _allMetadata = new();
    public ObservableCollection<ExifItem> MetadataList { get; } = new();
    private readonly ImageConversionService _conversionService = new();

    private int _navVersion;

    public MainWindowViewModel() : this(new AppSettingsService(), new DialogService()) { }

    public MainWindowViewModel(AppSettingsService settingsService, IDialogService dialogService)
    {
        _settingsService = settingsService;
        _dialogService = dialogService;


        ReloadExternalEditors();

        _settingsService.SettingsChanged += _ =>
        {
            ReloadExternalEditors();
            ApplyFilter();
            OpenInExternalEditorCommand.NotifyCanExecuteChanged();
        };
    }

    private void ReloadExternalEditors()
    {
        var previousName = SelectedExternalEditor?.Name;

        ExternalEditors.Clear();
        foreach (var editor in _settingsService.Current.ExternalEditors)
        {
            ExternalEditors.Add(editor);
        }

        // Восстанавливаем выбор по имени, иначе — первый в списке
        SelectedExternalEditor =
            ExternalEditors.FirstOrDefault(e =>
                string.Equals(e.Name, previousName, StringComparison.OrdinalIgnoreCase))
            ?? ExternalEditors.FirstOrDefault();
    }

    partial void OnIsFilterEnabledChanged(bool value) => ApplyFilter();

    partial void OnCurrentIndexChanged(int value)
    {
        if (value >= 0 && value < PhotoList.Count)
            _ = NavigateToAsync(value);
    }

    // ---------- Навигация ----------

    private bool CanGoPrevious => CurrentIndex > 0;
    private bool CanGoNext => CurrentIndex >= 0 && CurrentIndex < PhotoList.Count - 1;

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void Previous()
    {
        if (CurrentIndex > 0) CurrentIndex--;
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next()
    {
        if (CurrentIndex < PhotoList.Count - 1) CurrentIndex++;
    }

    // ---------- Внешний редактор ----------

    private bool CanOpenInExternalEditor =>
        !string.IsNullOrWhiteSpace(SelectedFilePath) &&
        File.Exists(SelectedFilePath) &&
        SelectedExternalEditor != null &&
        !string.IsNullOrWhiteSpace(SelectedExternalEditor.Path) &&
        File.Exists(SelectedExternalEditor.Path);

    [RelayCommand(CanExecute = nameof(CanOpenInExternalEditor))]
    private async Task OpenInExternalEditorAsync()
    {
        var editor = SelectedExternalEditor;
        var filePath = SelectedFilePath;

        if (editor is null || string.IsNullOrWhiteSpace(filePath)) return;

        if (!File.Exists(editor.Path))
        {
            await _dialogService.ShowErrorAsync(
                "Внешняя программа не найдена",
                $"Не найден исполняемый файл:\n{editor.Path}\n\nПроверьте ExternalEditors в appsettings.json.");
            return;
        }

        if (!File.Exists(filePath))
        {
            await _dialogService.ShowErrorAsync(
                "Файл не найден",
                $"Не найден файл:\n{filePath}");
            return;
        }

        // Подставляем путь к файлу в шаблон аргументов
        var arguments = editor.Arguments.Replace("{file}", filePath);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = editor.Path,
                Arguments = arguments,
                UseShellExecute = true
            });

            StatusMessage = $"Открыто в «{editor.Name}»: {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync(
                "Не удалось запустить программу",
                $"Программа: {editor.Name}\n{editor.Path}\n\nОшибка: {ex.Message}");
        }
    }

    // ---------- Удаление файла ----------

    private bool CanDeleteCurrentFile =>
        CurrentIndex >= 0 && CurrentIndex < PhotoList.Count;

    [RelayCommand(CanExecute = nameof(CanDeleteCurrentFile))]
    private async Task DeleteCurrentFileAsync()
    {
        if (CurrentIndex < 0 || CurrentIndex >= PhotoList.Count) return;

        var entry = PhotoList[CurrentIndex];

        // Защита от случайного удаления
        var confirmed = await _dialogService.ShowConfirmAsync(
            "Удаление файла",
            $"Удалить этот файл безвозвратно?\n\n{entry.FilePath}\n\nОтменить это действие нельзя.");

        if (!confirmed)
        {
            StatusMessage = "Удаление отменено";
            return;
        }

        try
        {
            File.Delete(entry.FilePath);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync("Не удалось удалить файл", ex.Message);
            return;
        }

        var deletedName = entry.FileName;

        // Найти ближайший оставшийся файл для открытия
        string? nextPath = null;
        if (PhotoList.Count > 1)
        {
            int nextIndex = CurrentIndex >= PhotoList.Count - 1
                ? CurrentIndex - 1
                : CurrentIndex + 1;
            nextPath = PhotoList[nextIndex].FilePath;
        }

        if (nextPath != null)
        {
            // Полная перезагрузка каталога
            await LoadImageFromPathAsync(nextPath);
            StatusMessage = $"Удалён: {deletedName}. {StatusMessage}";
        }
        else
        {
            // Файлов в каталоге больше не осталось
            Photo = null;
            SelectedFilePath = null;
            MetadataList.Clear();
            _allMetadata.Clear();
            PhotoList.Clear();
            TotalCount = 0;
            CurrentIndex = -1;
            StatusMessage = $"Удалён: {deletedName}. В каталоге не осталось изображений";
        }
    }

    // ---------- Конвертация PNG → JPEG ----------

    private bool CanConvertToJpeg =>
        CurrentIndex >= 0
        && CurrentIndex < PhotoList.Count
        && PhotoList[CurrentIndex].FilePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

    [RelayCommand(CanExecute = nameof(CanConvertToJpeg))]
    private async Task ConvertToJpegAsync()
    {
        if (CurrentIndex < 0 || CurrentIndex >= PhotoList.Count) return;

        var entry = PhotoList[CurrentIndex];
        var settings = _settingsService.Current.ImageConversion;

        IsBusy = true;
        string targetPath;
        try
        {
            targetPath = await Task.Run(() =>
                _conversionService.ConvertPngToJpeg(entry.FilePath, settings));
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync("Ошибка конвертации", ex.Message);
            return;
        }
        finally
        {
            IsBusy = false;
        }

        var created = await _dialogService.ShowConfirmAsync(
            "Конвертация завершена",
            $"Создан файл:\n{targetPath}\n\nПерейти к нему?");

        if (created)
        {
            // Перезагружаем каталог — в нём появился новый .jpg
            await LoadImageFromPathAsync(targetPath);
        }
        else
        {
            StatusMessage = $"Создан: {Path.GetFileName(targetPath)}";
        }
    }

    // ---------- Загрузка ----------

    public async Task LoadImageFromPathAsync(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory)) return;

        IsBusy = true;
        try
        {
            StatusMessage = "Сканирование каталога...";

            List<string> files;
            try
            {
                files = System.IO.Directory
                    .EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
                    .Where(IsSupportedImage)
                    .ToList();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("Ошибка доступа к каталогу", ex.Message);
                return;
            }

            var entries = await Task.Run(() =>
            {
                var result = new PhotoEntry[files.Count];
                Parallel.For(0, files.Count, i =>
                {
                    var file = files[i];
                    result[i] = new PhotoEntry
                    {
                        FilePath = file,
                        FileName = Path.GetFileName(file),
                        DateTaken = TryReadDateTaken(file)
                    };
                });
                return result;
            });

            var sorted = entries.OrderBy(e => e.DateTaken).ThenBy(e => e.FileName).ToList();

            PhotoList.Clear();
            foreach (var e in sorted) PhotoList.Add(e);
            TotalCount = PhotoList.Count;

            int index = 0;
            for (int i = 0; i < PhotoList.Count; i++)
            {
                if (string.Equals(PhotoList[i].FilePath, path, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }

            CurrentIndex = index;

            _ = LoadThumbnailsAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }
    private async Task LoadThumbnailsAsync()
    {
        var snapshot = PhotoList.ToList();

        await Task.Run(() =>
        {
            Parallel.ForEach(snapshot,
                new ParallelOptions { MaxDegreeOfParallelism = 4 },
                entry =>
                {
                    if (entry.Thumbnail != null) return;
                    try
                    {
                        using var stream = File.OpenRead(entry.FilePath);
                        var bmp = Bitmap.DecodeToWidth(stream, 128);
                        Dispatcher.UIThread.Post(() => entry.Thumbnail = bmp);
                    }
                    catch { }
                });
        });
    }

    private async Task NavigateToAsync(int index)
    {
        if (index < 0 || index >= PhotoList.Count) return;

        var version = ++_navVersion;
        var entry = PhotoList[index];

        IsBusy = true;
        try
        {
            Bitmap bmp;
            try
            {
                await using var stream = File.OpenRead(entry.FilePath);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                ms.Position = 0;
                bmp = new Bitmap(ms);
            }
            catch (Exception ex)
            {
                if (version == _navVersion)
                    await _dialogService.ShowErrorAsync("Ошибка загрузки изображения", ex.Message);
                return;
            }

            if (version != _navVersion) return;

            SelectedFilePath = entry.FilePath;
            Photo = bmp;

            LoadExifMetadata(entry.FilePath);
            ApplyFilter();

            OpenInExternalEditorCommand.NotifyCanExecuteChanged();
        }
        finally
        {
            // Снять IsBusy только если это последняя актуальная навигация
            if (version == _navVersion)
                IsBusy = false;
        }
    }

    // ---------- EXIF ----------

    private void LoadExifMetadata(string path)
    {
        _allMetadata.Clear();

        try
        {
            var directories = ImageMetadataReader.ReadMetadata(path);

            foreach (var directory in directories)
            {
                foreach (var tag in directory.Tags)
                {
                    if (string.IsNullOrWhiteSpace(tag.Description)) continue;

                    _allMetadata.Add(new ExifItem
                    {
                        Directory = directory.Name,
                        TagName = tag.Name,
                        Value = tag.Description
                    });
                }
            }

            if (_allMetadata.Count == 0)
            {
                _allMetadata.Add(new ExifItem
                {
                    Directory = "Info",
                    TagName = "Метаданные",
                    Value = "EXIF-данные не найдены в этом файле"
                });
            }
        }
        catch (ImageProcessingException ex)
        {
            _allMetadata.Add(new ExifItem
            {
                Directory = "Error",
                TagName = "Ошибка чтения",
                Value = ex.Message
            });
        }
        catch (Exception ex)
        {
            _allMetadata.Add(new ExifItem
            {
                Directory = "Error",
                TagName = "Общая ошибка",
                Value = ex.Message
            });
        }
    }

    private void ApplyFilter()
    {
        MetadataList.Clear();

        if (IsFilterEnabled)
        {
            var allowedTags = _settingsService.Current.AllowedTags;

            var filtered = _allMetadata.Where(item =>
                allowedTags.Any(allowed =>
                    string.Equals(allowed, item.TagName, StringComparison.OrdinalIgnoreCase)));

            foreach (var item in filtered)
                MetadataList.Add(item);
        }
        else
        {
            foreach (var item in _allMetadata)
                MetadataList.Add(item);
        }

        if (CurrentIndex >= 0 && CurrentIndex < PhotoList.Count)
        {
            var fileName = PhotoList[CurrentIndex].FileName;
            StatusMessage = IsFilterEnabled
                ? $"{fileName} — тегов: {MetadataList.Count} (фильтр)"
                : $"{fileName} — тегов: {MetadataList.Count}";
        }
    }

    // ---------- Вспомогательные ----------

    private static bool IsSupportedImage(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".png", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime TryReadDateTaken(string path)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(path);
            var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

            if (subIfd != null &&
                subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dt))
            {
                return dt;
            }
        }
        catch { }

        try { return File.GetLastWriteTime(path); }
        catch { return DateTime.MinValue; }
    }
}